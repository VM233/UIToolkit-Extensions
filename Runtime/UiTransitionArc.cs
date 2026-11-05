namespace VM233.UIElements
{
    public readonly struct UiTransitionArc
    {
        public UiTransitionPhase From { get; }
        public UiTransitionPhase To { get; }
        public UiTransitionArc(UiTransitionPhase from, UiTransitionPhase to) { From = from; To = to; }
    }
}
