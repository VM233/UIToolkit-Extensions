using NUnit.Framework;
using UnityEngine.UIElements;

namespace VM233.UIElements.Editor.Tests
{
    public sealed class UiTransitionInputTests
    {
        private sealed class GestureRegion : VisualElement, IUiTransitionInputRegion
        {
            public bool AcceptsTransitionInput(EventBase input) => input is PointerDownEvent or PointerMoveEvent or PointerUpEvent or WheelEvent;
        }
        [Test]
        public void DeclaredGestureSurvivesOnlyItsActualTransitionAncestry()
        {
            var transition = new VisualElement(); var gesture = new GestureRegion(); var child = new VisualElement();
            transition.Add(gesture); gesture.Add(child);
            using var pointer = PointerDownEvent.GetPooled(); pointer.target = child;
            Assert.That(UiPresentation.PreservesInput(transition, pointer), Is.True);
            Assert.That(UiPresentation.PreservesInput(new VisualElement(), pointer), Is.False);
        }
        [Test]
        public void ClicksAndOrdinaryControlsRemainBlocked()
        {
            var transition = new VisualElement(); var gesture = new GestureRegion(); var button = new Button();
            transition.Add(gesture); transition.Add(button);
            using var click = ClickEvent.GetPooled(); click.target = gesture;
            using var pointer = PointerDownEvent.GetPooled(); pointer.target = button;
            Assert.That(UiPresentation.PreservesInput(transition, click), Is.False);
            Assert.That(UiPresentation.PreservesInput(transition, pointer), Is.False);
        }
    }
}
