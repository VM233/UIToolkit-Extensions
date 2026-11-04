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
        public UiTransitionPhase Phase { get; }
        public bool Moving => Phase is UiTransitionPhase.Entering or UiTransitionPhase.Exiting or UiTransitionPhase.ReplacingOutgoing or UiTransitionPhase.ReplacingIncoming;
        public float Opacity { get; }
        public int IntermediateFrames { get; }
        public bool Retired => Phase == UiTransitionPhase.Retired;

        internal UiTransitionFrame(UiTransitionSite site, VisualElement element, bool requestedVisible,
            UiTransitionPhase phase, float opacity, int intermediateFrames, double seconds)
        {
            Site = site; Element = element; Frame = Time.frameCount;
            Seconds = seconds; RequestedVisible = requestedVisible;
            Phase = phase; Opacity = opacity; IntermediateFrames = intermediateFrames;
        }
    }
}
