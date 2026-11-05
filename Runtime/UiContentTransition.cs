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

        internal void Request(UiVisibilityTransition region)
        {
            if (region.HasPendingContent) pending.Add(region); else pending.Remove(region);
            if (pending.Count == 0 && phase == UiTransitionPhase.CapturingReplacement) AbortBatch();
            if (pending.Count > 0 || active.Length > 0) schedule.Resume();
        }

        internal void Remove(UiVisibilityTransition region)
        {
            pending.Remove(region);
            if (active.Contains(region) || pending.Count == 0 && phase == UiTransitionPhase.CapturingReplacement) AbortBatch();
        }

        private void Tick()
        {
            try
            {
                if (phase == UiTransitionPhase.Shown)
                {
                    if (pending.Count == 0) { schedule.Pause(); return; }
                    duration = seconds();
                    if (!float.IsFinite(duration) || duration <= 0) throw new InvalidOperationException("Content presentation requires a finite positive duration.");
                    lifetime = new CancellationTokenSource(); phase = UiTransitionProtocol.NextContent(phase);
                    capture = renderer.CaptureAsync(root, CaptureBounds, lifetime.Token);
                    waitStarted(phase, capture);
                }
                if (phase == UiTransitionPhase.CapturingReplacement && capture.IsCompleted)
                {
                    image = capture.GetAwaiter().GetResult();
                    if (!image.ContainsSourcePixels) throw new InvalidOperationException("The admitted content image contains no actual source pixels.");
                    active = active.Where(region => region.HasPendingContent).ToArray();
                    if (active.Length == 0) { CompleteBatch(); return; }
                    publicationRoots.Clear();
                    foreach (var region in active) publicationRoots.Add(region.Element);
                    phase = UiTransitionProtocol.NextContent(phase);
                    var operation = lifetime;
                    foreach (var region in active)
                    {
                        pending.Remove(region);
                        var publish = region.TakeContentPublication();
                        region.SetContentPhase(phase, 0, true, image.CapturedFrame);
                        if (!ReferenceEquals(operation, lifetime)) return;
                        publish();
                        if (!ReferenceEquals(operation, lifetime)) return;
                    }
                    phase = UiTransitionProtocol.NextContent(phase);
                    if (!Publish(0)) return;
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

        private Rect CaptureBounds()
        {
            var selected = new List<UiVisibilityTransition>(pending.Count);
            var coveredRegions = new List<UiVisibilityTransition>();
            foreach (var region in pending)
            {
                bool covered = false; int depth = 0;
                for (var ancestor = region.Element.parent; ancestor != null && !ReferenceEquals(ancestor, root); ancestor = ancestor.parent)
                {
                    if (++depth > 64) throw new InvalidOperationException("Content capture exceeds 64 ancestor links.");
                    if (regions.TryGetValue(ancestor, out var parent) && pending.Contains(parent)) { covered = true; break; }
                }
                if (!covered) selected.Add(region); else coveredRegions.Add(region);
            }
            foreach (var region in coveredRegions) { pending.Remove(region); region.CancelPendingContent(); }
            active = selected.ToArray();
            if (active.Length == 0) throw new InvalidOperationException("The content capture has no admitted pending region.");
            Rect bounds = active[0].Element.worldBound;
            foreach (var region in active)
            {
                Rect current = region.Element.worldBound;
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, current.xMin), Mathf.Min(bounds.yMin, current.yMin),
                    Mathf.Max(bounds.xMax, current.xMax), Mathf.Max(bounds.yMax, current.yMax));
            }
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
            active = Array.Empty<UiVisibilityTransition>(); lifetime = null;
            publicationRoots.Clear(); phase = UiTransitionPhase.Shown;
            var token = ownedLifetime.Token;
            var release = new UiTransitionRetirement();
            if (cancel) release.Attempt(ownedLifetime.Cancel);
            if (ownedCapture != null) release.Attempt(() =>
            {
                RequireRetiredWait(ownedCapture);
                try
                {
                    var captured = ownedCapture.GetAwaiter().GetResult();
                    if (!ReferenceEquals(captured, ownedImage)) captured.Dispose();
                }
                catch (OperationCanceledException error) when (error.CancellationToken == token && token.IsCancellationRequested) { }
            });
            if (ownedRendered != null) release.Attempt(() =>
            {
                RequireRetiredWait(ownedRendered);
                try { ownedRendered.GetAwaiter().GetResult(); }
                catch (OperationCanceledException error) when (error.CancellationToken == token && token.IsCancellationRequested) { }
            });
            if (ownedImage != null) release.Attempt(ownedImage.Dispose);
            release.Attempt(ownedLifetime.Dispose);
            foreach (var region in ownedRegions)
                if (regions.TryGetValue(region.Element, out var current) && ReferenceEquals(current, region))
                    release.Attempt(region.ClearContentPhase);
            release.ThrowIfFailed();
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
