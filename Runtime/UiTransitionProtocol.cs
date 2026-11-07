using System;
using System.Collections.Generic;

namespace VM233.UIElements
{
    public static class UiTransitionProtocol
    {
        private static readonly UiTransitionPhase[] content =
        {
            UiTransitionPhase.Shown, UiTransitionPhase.CapturingReplacement,
            UiTransitionPhase.PublishingReplacement, UiTransitionPhase.WaitingReplacementLayout,
            UiTransitionPhase.Replacing
        };
        public static IReadOnlyList<UiTransitionArc> Content { get; } = ContentArcs();
        public static IReadOnlyList<UiTransitionArc> Visibility { get; } = Array.AsReadOnly(new[]
        {
            new UiTransitionArc(UiTransitionPhase.Hidden, UiTransitionPhase.Entering),
            new UiTransitionArc(UiTransitionPhase.Entering, UiTransitionPhase.Shown),
            new UiTransitionArc(UiTransitionPhase.Shown, UiTransitionPhase.Exiting),
            new UiTransitionArc(UiTransitionPhase.Exiting, UiTransitionPhase.Hidden),
            new UiTransitionArc(UiTransitionPhase.Entering, UiTransitionPhase.Exiting),
            new UiTransitionArc(UiTransitionPhase.Exiting, UiTransitionPhase.Entering),
            new UiTransitionArc(UiTransitionPhase.Hidden, UiTransitionPhase.Retired),
            new UiTransitionArc(UiTransitionPhase.Shown, UiTransitionPhase.Retired),
            new UiTransitionArc(UiTransitionPhase.Entering, UiTransitionPhase.Retired),
            new UiTransitionArc(UiTransitionPhase.Exiting, UiTransitionPhase.Retired)
        });

        internal static UiTransitionPhase NextContent(UiTransitionPhase phase)
        {
            int index = Array.IndexOf(content, phase);
            if (index < 0) throw new InvalidOperationException("Only an admitted content phase can advance.");
            return content[(index + 1) % content.Length];
        }

        internal static void RequireVisibility(UiTransitionPhase from, UiTransitionPhase to)
        {
            foreach (var arc in Visibility) if (arc.From == from && arc.To == to) return;
            throw new InvalidOperationException($"UI visibility cannot advance from {from} to {to}.");
        }

        internal static void RequireContent(UiTransitionPhase from, UiTransitionPhase to)
        {
            foreach (var arc in Content) if (arc.From == from && arc.To == to) return;
            throw new InvalidOperationException($"UI content cannot advance from {from} to {to}.");
        }

        private static IReadOnlyList<UiTransitionArc> ContentArcs()
        {
            var arcs = new List<UiTransitionArc>();
            for (int i = 0; i < content.Length; i++)
            {
                var phase = content[i];
                arcs.Add(new UiTransitionArc(phase, content[(i + 1) % content.Length]));
                if (phase is UiTransitionPhase.Replacing or UiTransitionPhase.WaitingReplacementLayout)
                    arcs.Add(new UiTransitionArc(phase, UiTransitionPhase.CapturingReplacement));
                if (phase != UiTransitionPhase.Shown && phase != UiTransitionPhase.Replacing)
                    arcs.Add(new UiTransitionArc(phase, UiTransitionPhase.Shown));
                arcs.Add(new UiTransitionArc(phase, UiTransitionPhase.Retired));
            }
            return arcs.AsReadOnly();
        }
    }
}
