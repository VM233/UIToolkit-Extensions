using UnityEngine;
using UnityEngine.UIElements;

namespace VM233.UIElements
{
    public readonly struct UiTransitionFrame
    {
        public UiTransitionSite Site { get; }
        public VisualElement Element { get; }
        public int Frame { get; }
        public double Seconds { get; }
        public bool RequestedVisible { get; }
        public bool Moving { get; }
        public float Opacity { get; }
        public int IntermediateFrames { get; }
        public bool Retired { get; }

        internal UiTransitionFrame(UiTransitionSite site, VisualElement element, bool requestedVisible,
            bool moving, float opacity, int intermediateFrames, bool retired)
        {
            Site = site; Element = element; Frame = Time.frameCount;
            Seconds = Time.unscaledTimeAsDouble; RequestedVisible = requestedVisible;
            Moving = moving; Opacity = opacity; IntermediateFrames = intermediateFrames; Retired = retired;
        }
    }
}
