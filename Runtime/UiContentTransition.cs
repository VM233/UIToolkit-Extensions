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
        private UiVisibilityTransition[] active = Array.Empty<UiVisibilityTransition>();
        private CancellationTokenSource lifetime;
        private Task<IUiTransitionImage> capture;
        private Task rendered;
        private IUiTransitionImage image;
        private UiTransitionSegment segment;
        private float duration;
        private UiTransitionPhase phase = UiTransitionPhase.Shown;
        internal bool IsPublishing => phase == UiTransitionPhase.PublishingReplacement;
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
                    lifetime = new CancellationTokenSource(); phase = UiTransitionPhase.CapturingReplacement;
                    capture = renderer.CaptureAsync(root, CaptureBounds, lifetime.Token);
                    waitStarted(phase, capture);
                }
                if (phase == UiTransitionPhase.CapturingReplacement && capture.IsCompleted)
                {
                    image = capture.GetAwaiter().GetResult();
                    if (!image.ContainsSourcePixels) throw new InvalidOperationException("The admitted content image contains no actual source pixels.");
                    active = active.Where(region => region.HasPendingContent).ToArray();
                    if (active.Length == 0) { CompleteBatch(); return; }
                    phase = UiTransitionPhase.PublishingReplacement;
                    foreach (var region in active)
                    {
                        pending.Remove(region);
                        var publish = region.TakeContentPublication();
                        region.SetContentPhase(phase, 0, true, image.CapturedFrame);
                        publish();
                    }
                    phase = UiTransitionPhase.WaitingReplacementLayout;
                    Publish(0); rendered = renderer.WaitForRenderedFrameAsync(lifetime.Token);
                    waitStarted(phase, rendered);
                }
                if (phase == UiTransitionPhase.WaitingReplacementLayout && rendered.IsCompleted)
                {
                    rendered.GetAwaiter().GetResult(); phase = UiTransitionPhase.Replacing;
                    segment = new UiTransitionSegment(phase, 0, 1, clock(), duration);
                }
                if (phase == UiTransitionPhase.Replacing)
                {
                    double now = clock(); float progress = segment.Sample(now);
                    image.SetSourceOpacity(1 - progress); Publish(progress);
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

        private void Publish(float progress)
        {
            foreach (var region in active) region.SetContentPhase(phase, progress, image.ContainsSourcePixels, image.CapturedFrame);
        }

        private void CompleteBatch()
        {
            var release = new UiTransitionRetirement();
            release.Attempt(image.Dispose); image = null;
            foreach (var region in active) release.Attempt(region.ClearContentPhase);
            active = Array.Empty<UiVisibilityTransition>(); capture = null; rendered = null; segment = null;
            lifetime.Dispose(); lifetime = null; phase = UiTransitionPhase.Shown;
            release.ThrowIfFailed();
        }

        private void AbortBatch()
        {
            var release = new UiTransitionRetirement();
            var ownedImage = image; image = null;
            release.Attempt(lifetime.Cancel);
            if (capture != null) release.Attempt(() =>
            {
                RequireRetiredWait(capture);
                try
                {
                    var captured = capture.GetAwaiter().GetResult();
                    if (!ReferenceEquals(captured, ownedImage)) captured.Dispose();
                }
                catch (OperationCanceledException error) when (error.CancellationToken == lifetime.Token && lifetime.IsCancellationRequested) { }
            });
            if (rendered != null) release.Attempt(() =>
            {
                RequireRetiredWait(rendered);
                try { rendered.GetAwaiter().GetResult(); }
                catch (OperationCanceledException error) when (error.CancellationToken == lifetime.Token && lifetime.IsCancellationRequested) { }
            });
            if (ownedImage != null) release.Attempt(ownedImage.Dispose);
            foreach (var region in active) release.Attempt(region.ClearContentPhase);
            active = Array.Empty<UiVisibilityTransition>(); capture = null; rendered = null; segment = null;
            lifetime.Dispose(); lifetime = null; phase = UiTransitionPhase.Shown;
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
