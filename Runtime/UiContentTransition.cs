using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace VM233.UIElements
{
    internal sealed class UiContentTransition
    {
        private readonly VisualElement root;
        private readonly IReadOnlyDictionary<VisualElement, UiVisibilityTransition> regions;
        private readonly IUiTransitionRenderer renderer;
        private readonly Func<float> seconds;
        private readonly Func<double> clock;
        private readonly Action<UiTransitionPhase, Task> waitStarted;
        private readonly IVisualElementScheduledItem schedule;
        private readonly HashSet<UiVisibilityTransition> pending = new();
        private readonly HashSet<VisualElement> publicationRoots = new();
        private UiVisibilityTransition[] active = Array.Empty<UiVisibilityTransition>();
        private CancellationTokenSource lifetime;
        private Task<IUiTransitionImage> capture;
        private Task rendered;
        private IUiTransitionImage image;
        private Rect imageBounds;
        private SourceSelection selection;
        private UiTransitionSegment segment;
        private float duration;
        private UiTransitionPhase phase = UiTransitionPhase.Shown;
        private bool IsPublishing => phase == UiTransitionPhase.PublishingReplacement;
        internal bool Retired => phase == UiTransitionPhase.Retired;
        internal bool HasImage => image != null;
        internal UiTransitionPhase? PendingFrameWait => capture != null && !capture.IsCompleted
            ? UiTransitionPhase.CapturingReplacement : rendered != null && !rendered.IsCompleted
                ? UiTransitionPhase.WaitingReplacementLayout : null;

        internal UiContentTransition(VisualElement root, IReadOnlyDictionary<VisualElement, UiVisibilityTransition> regions,
            IUiTransitionRenderer renderer, Func<float> seconds, Func<double> clock, Action<UiTransitionPhase, Task> waitStarted)
        {
            this.root = root; this.regions = regions; this.renderer = renderer; this.seconds = seconds; this.clock = clock;
            this.waitStarted = waitStarted;
            schedule = root.schedule.Execute(Tick).Every(16); schedule.Pause();
        }

        internal bool Publishes(VisualElement element)
        {
            if (!IsPublishing) return false;
            int depth = 0;
            for (var current = element; current != null && !ReferenceEquals(current, root); current = current.parent)
            {
                if (++depth > 64) throw new InvalidOperationException("Content publication exceeds64 ancestor links.");
                if (publicationRoots.Contains(current)) return true;
            }
            return false;
        }

        internal bool PublishesAncestor(VisualElement element) => Publishes(element.parent);

        internal void Request(UiVisibilityTransition region)
        {
            if (region.HasPendingContent) pending.Add(region); else pending.Remove(region);
            if (pending.Count == 0 && phase == UiTransitionPhase.CapturingReplacement && image == null) AbortBatch();
            if (pending.Count > 0 && phase is UiTransitionPhase.Replacing or UiTransitionPhase.WaitingReplacementLayout)
                BeginCapture();
            if (pending.Count > 0 || active.Length > 0) schedule.Resume();
        }

        internal void Remove(UiVisibilityTransition region)
        {
            pending.Remove(region);
            if (active.Contains(region) || selection?.Regions.Contains(region) == true ||
                pending.Count == 0 && phase == UiTransitionPhase.CapturingReplacement && image == null) AbortBatch();
        }

        private void Tick()
        {
            try
            {
                if (phase == UiTransitionPhase.Shown)
                {
                    if (pending.Count == 0) { schedule.Pause(); return; }
                    BeginCapture();
                }
                if (phase == UiTransitionPhase.CapturingReplacement && capture.IsCompleted)
                {
                    var operation = lifetime;
                    var previousImage = image;
                    var previousRegions = active;
                    image = capture.GetAwaiter().GetResult();
                    if (!image.ContainsSourcePixels) throw new InvalidOperationException("The admitted content image contains no actual source pixels.");
                    imageBounds = selection.Bounds; active = selection.Regions;
                    previousImage?.Dispose();
                    if (!ReferenceEquals(operation, lifetime)) return;
                    var retained = new HashSet<UiVisibilityTransition>(active);
                    foreach (var region in previousRegions)
                    {
                        if (!retained.Contains(region) && regions.TryGetValue(region.Element, out var current) && ReferenceEquals(current, region))
                            region.ClearContentPhase();
                        if (!ReferenceEquals(operation, lifetime)) return;
                    }
                    publicationRoots.Clear();
                    var publications = new Action[active.Length];
                    for (int index = 0; index < active.Length; index++)
                    {
                        var region = active[index];
                        if (!region.HasPendingContent) continue;
                        pending.Remove(region); publicationRoots.Add(region.Element);
                        publications[index] = region.TakeContentPublication();
                    }
                    phase = UiTransitionProtocol.NextContent(phase);
                    for (int index = 0; index < active.Length; index++)
                    {
                        var region = active[index];
                        region.SetContentPhase(phase, 0, true, image.CapturedFrame);
                        if (!ReferenceEquals(operation, lifetime)) return;
                        publications[index]?.Invoke();
                        if (!ReferenceEquals(operation, lifetime)) return;
                    }
                    phase = UiTransitionProtocol.NextContent(phase);
                    if (!Publish(0)) return;
                    if (pending.Count > 0) { BeginCapture(); return; }
                    rendered = renderer.WaitForRenderedFrameAsync(lifetime.Token);
                    waitStarted(phase, rendered);
                }
                if (phase == UiTransitionPhase.WaitingReplacementLayout && rendered.IsCompleted)
                {
                    rendered.GetAwaiter().GetResult(); phase = UiTransitionProtocol.NextContent(phase);
                    segment = new UiTransitionSegment(phase, 0, 1, clock(), duration);
                }
                if (phase == UiTransitionPhase.Replacing)
                {
                    double now = clock(); float progress = segment.Sample(now);
                    image.SetSourceOpacity(1 - progress);
                    if (!Publish(progress)) return;
                    if (segment.Complete(now)) CompleteBatch();
                }
            }
            catch (Exception primary)
            {
                var retirement = new UiTransitionRetirement(primary);
                retirement.Attempt(Retire);
                retirement.ThrowIfFailed();
            }
        }

        private void BeginCapture()
        {
            UiTransitionProtocol.RequireContent(phase, UiTransitionPhase.CapturingReplacement);
            duration = seconds();
            if (!float.IsFinite(duration) || duration <= 0) throw new InvalidOperationException("Content presentation requires a finite positive duration.");
            var previousLifetime = lifetime;
            var previousCapture = capture;
            var previousRendered = rendered;
            var operation = new CancellationTokenSource();
            lifetime = operation; capture = null; rendered = null; selection = null; segment = null;
            phase = UiTransitionPhase.CapturingReplacement;
            try
            {
                if (previousLifetime != null) ReleaseWaits(previousLifetime, previousCapture, previousRendered, image, true);
                if (!ReferenceEquals(operation, lifetime)) return;
                capture = renderer.CaptureAsync(root, CaptureBounds, operation.Token);
                waitStarted(phase, capture);
            }
            catch (Exception primary)
            {
                var retirement = new UiTransitionRetirement(primary);
                retirement.Attempt(Retire); retirement.ThrowIfFailed();
            }
        }

        private Rect CaptureBounds()
        {
            var candidates = new HashSet<UiVisibilityTransition>(active);
            candidates.UnionWith(pending);
            var selected = new List<UiVisibilityTransition>(candidates.Count);
            var coveredRegions = new List<UiVisibilityTransition>();
            foreach (var region in candidates)
            {
                bool covered = false; int depth = 0;
                for (var ancestor = region.Element.parent; ancestor != null && !ReferenceEquals(ancestor, root); ancestor = ancestor.parent)
                {
                    if (++depth > 64) throw new InvalidOperationException("Content capture exceeds 64 ancestor links.");
                    if (regions.TryGetValue(ancestor, out var parent) && pending.Contains(parent)) { covered = true; break; }
                }
                if (!covered) selected.Add(region); else if (pending.Contains(region)) coveredRegions.Add(region);
            }
            foreach (var region in coveredRegions) { pending.Remove(region); region.CancelPendingContent(); }
            if (selected.Count == 0) throw new InvalidOperationException("The content capture has no admitted pending or retained region.");
            Rect bounds = image != null ? imageBounds : selected[0].Element.worldBound;
            foreach (var region in selected)
            {
                Rect current = region.Element.worldBound;
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, current.xMin), Mathf.Min(bounds.yMin, current.yMin),
                    Mathf.Max(bounds.xMax, current.xMax), Mathf.Max(bounds.yMax, current.yMax));
            }
            selection = new SourceSelection(selected.ToArray(), bounds);
            return bounds;
        }

        private bool Publish(float progress)
        {
            var operation = lifetime;
            foreach (var region in active)
            {
                region.SetContentPhase(phase, progress, image.ContainsSourcePixels, image.CapturedFrame);
                if (!ReferenceEquals(operation, lifetime)) return false;
            }
            return true;
        }

        private void CompleteBatch() => ReleaseBatch(false);
        private void AbortBatch() => ReleaseBatch(true);

        private void ReleaseBatch(bool cancel)
        {
            var ownedImage = image;
            var ownedCapture = capture;
            var ownedRendered = rendered;
            var ownedRegions = active;
            var ownedLifetime = lifetime;
            image = null; capture = null; rendered = null; segment = null;
            selection = null;
            active = Array.Empty<UiVisibilityTransition>(); lifetime = null;
            publicationRoots.Clear(); phase = UiTransitionPhase.Shown;
            var release = new UiTransitionRetirement();
            release.Attempt(() => ReleaseWaits(ownedLifetime, ownedCapture, ownedRendered, ownedImage, cancel));
            if (ownedImage != null) release.Attempt(ownedImage.Dispose);
            foreach (var region in ownedRegions)
                if (regions.TryGetValue(region.Element, out var current) && ReferenceEquals(current, region))
                    release.Attempt(region.ClearContentPhase);
            release.ThrowIfFailed();
        }

        private static void ReleaseWaits(CancellationTokenSource ownedLifetime, Task<IUiTransitionImage> ownedCapture,
            Task ownedRendered, IUiTransitionImage retainedImage, bool cancel)
        {
            var token = ownedLifetime.Token;
            var release = new UiTransitionRetirement();
            if (cancel) release.Attempt(ownedLifetime.Cancel);
            if (ownedCapture != null) release.Attempt(() =>
            {
                RequireRetiredWait(ownedCapture);
                try
                {
                    var captured = ownedCapture.GetAwaiter().GetResult();
                    if (!ReferenceEquals(captured, retainedImage)) captured.Dispose();
                }
                catch (OperationCanceledException error) when (error.CancellationToken == token && token.IsCancellationRequested) { }
            });
            if (ownedRendered != null) release.Attempt(() =>
            {
                RequireRetiredWait(ownedRendered);
                try { ownedRendered.GetAwaiter().GetResult(); }
                catch (OperationCanceledException error) when (error.CancellationToken == token && token.IsCancellationRequested) { }
            });
            release.Attempt(ownedLifetime.Dispose);
            release.ThrowIfFailed();
        }

        private sealed class SourceSelection
        {
            internal readonly UiVisibilityTransition[] Regions;
            internal readonly Rect Bounds;
            internal SourceSelection(UiVisibilityTransition[] regions, Rect bounds)
            { Regions = regions; Bounds = bounds; }
        }

        private static void RequireRetiredWait(Task wait)
        {
            if (!wait.IsCompleted) throw new InvalidOperationException("The renderer did not complete its owned main-thread frame wait on cancellation.");
        }

        internal void Retire()
        {
            schedule.Pause(); pending.Clear();
            try { if (lifetime != null) AbortBatch(); }
            finally { phase = UiTransitionPhase.Retired; }
        }
    }
}
