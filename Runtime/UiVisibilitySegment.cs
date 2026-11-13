namespace VM233.UIElements
{
    internal sealed class UiVisibilitySegment
    {
        private readonly UiTransitionSegment opacity;
        private readonly float translationOrigin, translationDestination;
        internal UiTransitionPhase Phase => opacity.Phase;

        internal UiVisibilitySegment(UiTransitionPhase phase, float opacityOrigin, float opacityDestination,
            float translationOrigin, float translationDestination, double started, float duration)
        {
            opacity = new UiTransitionSegment(phase, opacityOrigin, opacityDestination, started, duration);
            this.translationOrigin = translationOrigin;
            this.translationDestination = translationDestination;
        }

        internal float SampleOpacity(double now) => opacity.Sample(now);
        internal float SampleTranslation(double now) => translationOrigin +
            (translationDestination - translationOrigin) * opacity.Progress(now);
        internal bool Complete(double now) => opacity.Complete(now);
    }
}
