using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace VM233.UIElements
{
    public static class UiPresentation
    {
        public static DisplayStyle Display(VisualElement element, DisplayStyle display, string targetExpression,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Application.isPlaying || element.panel == null)
            {
                element.style.display = display;
                return display;
            }
            Scope(element).Display(element, display, new UiTransitionSite(file, member, line, targetExpression));
            return display;
        }

        public static DisplayStyle RequestedDisplay(VisualElement element)
        {
            if (!Application.isPlaying || element.panel == null) return element.style.display.value;
            return Scope(element).RequestedDisplay(element);
        }

        public static void Replace(VisualElement element, object identity, Action publish, string targetExpression,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Application.isPlaying || element.panel == null) { publish(); return; }
            Scope(element).Replace(element, identity, publish, new UiTransitionSite(file, member, line, targetExpression));
        }

        public static UiTransitionScope Scope(VisualElement element)
        {
            for (var ancestor = element; ancestor != null; ancestor = ancestor.parent)
                if (ancestor is IUiTransitionHost host) return host.Transitions;
            throw new InvalidOperationException($"Attached UI region '{element.name}' has no IUiTransitionHost.");
        }
    }
}
