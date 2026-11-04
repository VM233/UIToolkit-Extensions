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
        public bool Presented { get; private set; }
        public int RegionCount => regions.Count;
        public event Action<UiTransitionFrame> Observed;

        public UiTransitionScope(VisualElement root, Func<float> seconds, Func<float> distance, Func<double> clock)
        {
            this.root = root; this.seconds = seconds; this.distance = distance; this.clock = clock;
            root.RegisterCallback<DetachFromPanelEvent>(Detached);
        }

        public void Publish() => Presented = true;

        public void Display(VisualElement element, DisplayStyle display, UiTransitionSite site)
        {
            Region(element).Request(display, site, Presented && AncestorsPresented(element));
        }

        private UiVisibilityTransition Region(VisualElement element)
        {
            if (!regions.TryGetValue(element, out var region))
            {
                if (regions.Count == 4096) throw new InvalidOperationException("UI transition scope exceeds its 4096-region contract.");
                region = new UiVisibilityTransition(element, seconds, distance, clock, PublishFrame, Release);
                regions.Add(element, region);
            }
            return region;
        }

        public void Replace(VisualElement element, object identity, Action publish, UiTransitionSite site) =>
            Region(element).Replace(identity, publish, site,
                Presented && AncestorsPresented(element) && RequestedDisplay(element) != DisplayStyle.None && element.resolvedStyle.display != DisplayStyle.None);

        private bool AncestorsPresented(VisualElement element)
        {
            for (var ancestor = element.parent; ancestor != null; ancestor = ancestor.parent)
            {
                if (regions.TryGetValue(ancestor, out var region) && region.Requested == DisplayStyle.None) return false;
                if (ancestor.resolvedStyle.display == DisplayStyle.None) return false;
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
            foreach (var region in regions.Values) region.Dispose();
            regions.Clear();
        }

        private void Detached(DetachFromPanelEvent evt)
        {
            if (ReferenceEquals(evt.target, root)) Retire();
        }
    }
}
