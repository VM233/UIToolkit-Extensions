namespace VM233.UIElements
{
    internal sealed class UiTransitionSegment
    {
        internal UiTransitionPhase Phase { get; }
        internal float Origin { get; }
        internal float Destination { get; }
        internal double Started { get; }
        internal float Duration { get; }

        internal UiTransitionSegment(UiTransitionPhase phase, float origin, float destination, double started, float duration)
        {
            Phase = phase; Origin = origin; Destination = destination; Started = started; Duration = duration;
        }

        internal float Sample(double now)
        {
            float fraction = (float)((now - Started) / Duration);
            if (fraction >= 1) return Destination;
            float remaining = 1 - fraction;
            return Origin + (Destination - Origin) * (1 - remaining * remaining * remaining);
        }

        internal bool Complete(double now) => now - Started >= Duration;
    }
}
