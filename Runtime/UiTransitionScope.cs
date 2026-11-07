using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace VM233.UIElements
{
    public sealed class UiTransitionScope
    {
        private readonly VisualElement root;
        private readonly Dictionary<VisualElement, UiVisibilityTransition> regions = new();
        private readonly Func<float> seconds, distance;
        private readonly Func<double> clock;
        private readonly IUiTransitionRenderer renderer;
        private UiContentTransition content;
        private VisualElement focusTarget, focusBarrier;
        public bool Presented { get; private set; }
        public int RegionCount => regions.Count;
        public bool HasRetainedImage => content.HasImage;
        public UiTransitionPhase? PendingFrameWait => content.PendingFrameWait;
        public event Action<UiTransitionFrame> Observed;
        public event Action<UiTransitionPhase, Task> FrameWaitStarted;

        public UiTransitionScope(VisualElement root, Func<float> seconds, Func<float> distance, Func<double> clock,
            IUiTransitionRenderer renderer)
        {
            this.root = root; this.seconds = seconds; this.distance = distance; this.clock = clock;
            this.renderer = renderer;
            content = CreateContent();
            root.RegisterCallback<DetachFromPanelEvent>(Detached);
        }

        public void Publish()
        {
            if (content.Retired) content = CreateContent();
            Presented = true;
            SettleFocus();
        }

        public void FocusAfter(VisualElement target, VisualElement barrier)
        {
            if (!root.Contains(target) || !root.Contains(barrier))
                throw new ArgumentException("Focus target and settlement barrier must belong to this document scope.");
            focusTarget = target; focusBarrier = barrier;
            SettleFocus();
        }

        private bool Settled(VisualElement element)
        {
            int depth = 0;
            for (var current = element; current != null && !ReferenceEquals(current, root); current = current.parent)
            {
                if (++depth > 64) throw new InvalidOperationException("Focus settlement exceeds64 ancestor links.");
                if (regions.TryGetValue(current, out var region) && !region.InputSettled) return false;
            }
            return true;
        }

        private void SettleFocus()
        {
            if (!Presented || focusTarget == null) return;
            var target = focusTarget; var barrier = focusBarrier;
            if (!root.Contains(target) || !root.Contains(barrier)) { focusTarget = focusBarrier = null; return; }
            if (!Settled(target) || !Settled(barrier)) return;
            focusTarget = focusBarrier = null;
            if (target.enabledInHierarchy && target.visible && target.resolvedStyle.display != DisplayStyle.None) target.Focus();
        }

        public void Display(VisualElement element, DisplayStyle display, UiTransitionSite site)
        {
            Region(element).Request(display, site, Presented && !content.Publishes(element) && AncestorsPresented(element));
        }

        public void Visibility(VisualElement element, StyleEnum<Visibility> visibility, UiTransitionSite site)
        {
            Region(element).RequestVisibility(visibility, site, Presented && !content.Publishes(element) && AncestorsPresented(element));
        }

        public void Initialize(VisualElement element, Action initialize)
        {
            RequireUnpublishedMutation(element);
            initialize();
        }

        public void ClearChildren(VisualElement element)
        {
            RequireUnpublishedMutation(element);
            element.Clear();
        }

        public void AttachHidden(VisualElement parent, VisualElement child)
        {
            RequireOwned(parent);
            AppendHidden(parent, child);
        }

        internal static void AppendHidden(VisualElement parent, VisualElement child)
        {
            if (child.parent != null || child.panel != null || child.style.display.keyword != StyleKeyword.Undefined ||
                child.style.display.value != DisplayStyle.None)
                throw new InvalidOperationException("Hidden attachment requires an unparented, unattached child with explicit inline display:none.");
            parent.Add(child);
        }

        public void RemoveElement(VisualElement element)
        {
            RequireUnpublishedMutation(element);
            element.RemoveFromHierarchy();
        }

        private void RequireUnpublishedMutation(VisualElement element)
        {
            if (Presented) throw new InvalidOperationException($"Presented UI '{element.name}' requires coherent replacement or completed retirement before hierarchy mutation.");
            RequireOwned(element);
        }

        private void RequireOwned(VisualElement element)
        {
            if (!ReferenceEquals(element, root) && !root.Contains(element))
                throw new ArgumentException("Hierarchy mutation must belong to this actual document scope.");
        }

        private UiVisibilityTransition Region(VisualElement element)
        {
            if (!regions.TryGetValue(element, out var region))
            {
                if (regions.Count == 4096) throw new InvalidOperationException("UI transition scope exceeds its 4096-region contract.");
                region = new UiVisibilityTransition(element, seconds, distance, clock, PublishFrame, Release,
                    value => content.Request(value), value => content.Remove(value));
                regions.Add(element, region);
            }
            return region;
        }

        public void Replace(VisualElement element, object identity, Action publish, UiTransitionSite site)
        {
            if (ReferenceEquals(element, root)) throw new ArgumentException("Local replacement must be inside its retained document host.");
            Region(element).Replace(identity, publish, site,
                Presented && !content.PublishesAncestor(element) && AncestorsPresented(element) && Region(element).RequestedVisible &&
                element.resolvedStyle.display != DisplayStyle.None && element.resolvedStyle.visibility == UnityEngine.UIElements.Visibility.Visible && element.resolvedStyle.opacity > 0);
        }

        private bool AncestorsPresented(VisualElement element)
        {
            for (var ancestor = element.parent; ancestor != null; ancestor = ancestor.parent)
            {
                if (regions.TryGetValue(ancestor, out var region) && region.Requested == DisplayStyle.None) return false;
                if (ancestor.resolvedStyle.display == DisplayStyle.None) return false;
                if (ancestor.resolvedStyle.visibility != UnityEngine.UIElements.Visibility.Visible || ancestor.resolvedStyle.opacity == 0) return false;
                if (ReferenceEquals(ancestor, root)) break;
            }
            return true;
        }

        public DisplayStyle RequestedDisplay(VisualElement element) =>
            regions.TryGetValue(element, out var region) ? region.Requested : element.style.display.value;

        public IReadOnlyList<UiTransitionFrame> Observe()
        {
            var result = new List<UiTransitionFrame>(regions.Count);
            foreach (var region in regions.Values) result.Add(region.Observe());
            return result;
        }

        private void PublishFrame(UiTransitionFrame frame)
        {
            Observed?.Invoke(frame);
            SettleFocus();
        }

        private UiContentTransition CreateContent() => new(root, regions, renderer, seconds, clock,
            (phase, task) => FrameWaitStarted?.Invoke(phase, task));

        private void Release(VisualElement element)
        {
            if (!regions.Remove(element, out var region)) return;
            region.Dispose();
        }

        public void Retire()
        {
            Presented = false;
            focusTarget = focusBarrier = null;
            var owned = new List<UiVisibilityTransition>(regions.Values);
            regions.Clear();
            var retirement = new UiTransitionRetirement(); retirement.Attempt(content.Retire);
            foreach (var region in owned) retirement.Attempt(region.Dispose);
            retirement.ThrowIfFailed();
        }

        private void Detached(DetachFromPanelEvent evt)
        {
            if (ReferenceEquals(evt.target, root)) Retire();
        }
    }
}
