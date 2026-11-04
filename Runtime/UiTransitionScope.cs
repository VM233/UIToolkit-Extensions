using System;
using System.Collections.Generic;
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
        public bool Presented { get; private set; }
        public int RegionCount => regions.Count;
        public bool HasRetainedImage => content.HasImage;
        public event Action<UiTransitionFrame> Observed;

        public UiTransitionScope(VisualElement root, Func<float> seconds, Func<float> distance, Func<double> clock,
            IUiTransitionRenderer renderer)
        {
            this.root = root; this.seconds = seconds; this.distance = distance; this.clock = clock;
            this.renderer = renderer;
            content = new UiContentTransition(root, regions, renderer, seconds, clock);
            root.RegisterCallback<DetachFromPanelEvent>(Detached);
        }

        public void Publish()
        {
            if (content.Retired) content = new UiContentTransition(root, regions, renderer, seconds, clock);
            Presented = true;
        }

        public void Display(VisualElement element, DisplayStyle display, UiTransitionSite site)
        {
            Region(element).Request(display, site, Presented && AncestorsPresented(element));
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
                Presented && !content.IsPublishing && AncestorsPresented(element) && RequestedDisplay(element) != DisplayStyle.None &&
                element.resolvedStyle.display != DisplayStyle.None && element.resolvedStyle.visibility == Visibility.Visible && element.resolvedStyle.opacity > 0);
        }

        private bool AncestorsPresented(VisualElement element)
        {
            if (content.IsPublishing) return false;
            for (var ancestor = element.parent; ancestor != null; ancestor = ancestor.parent)
            {
                if (regions.TryGetValue(ancestor, out var region) && region.Requested == DisplayStyle.None) return false;
                if (ancestor.resolvedStyle.display == DisplayStyle.None) return false;
                if (ancestor.resolvedStyle.visibility != Visibility.Visible || ancestor.resolvedStyle.opacity == 0) return false;
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

        private void PublishFrame(UiTransitionFrame frame) => Observed?.Invoke(frame);

        private void Release(VisualElement element)
        {
            var region = regions[element];
            regions.Remove(element);
            region.Dispose();
        }

        public void Retire()
        {
            Presented = false;
            var retirement = new UiTransitionRetirement(); retirement.Attempt(content.Retire);
            foreach (var region in regions.Values) retirement.Attempt(region.Dispose);
            regions.Clear();
            retirement.ThrowIfFailed();
        }

        private void Detached(DetachFromPanelEvent evt)
        {
            if (ReferenceEquals(evt.target, root)) Retire();
        }
    }
}
