using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace VM233.UIElements
{
    public static class UiPresentation
    {
        public static void Initialize(VisualElement element, Action initialize, string targetExpression,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Application.isPlaying || element.panel == null) { initialize(); return; }
            Scope(element).Initialize(element, initialize);
        }

        public static void FocusAfter(VisualElement target, VisualElement barrier)
        {
            if (!Application.isPlaying || target.panel == null) { target.Focus(); return; }
            Scope(target).FocusAfter(target, barrier);
        }

        public static void Clear(VisualElement element, string targetExpression,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Application.isPlaying || element.panel == null) { element.Clear(); return; }
            Scope(element).ClearChildren(element);
        }

        public static void AttachHidden(VisualElement parent, VisualElement child, string targetExpression,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Application.isPlaying || parent.panel == null)
            {
                UiTransitionScope.AppendHidden(parent, child);
                return;
            }
            Scope(parent).AttachHidden(parent, child);
        }

        public static void Remove(VisualElement element, string targetExpression,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Application.isPlaying || element.panel == null) { element.RemoveFromHierarchy(); return; }
            Scope(element).RemoveElement(element);
        }

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

        public static void Visibility(VisualElement element, StyleEnum<Visibility> visibility, string targetExpression,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Application.isPlaying || element.panel == null) { element.style.visibility = visibility; return; }
            Scope(element).Visibility(element, visibility, new UiTransitionSite(file, member, line, targetExpression));
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
