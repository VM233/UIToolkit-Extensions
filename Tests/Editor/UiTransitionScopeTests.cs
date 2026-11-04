using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace VM233.UIElements.Editor.Tests
{
    public sealed class UiTransitionScopeTests
    {
        private EditorWindow window;
        private VisualElement region;
        private UiTransitionScope scope;
        private readonly UiTransitionSite site = new("Fixture.cs", "Stimulus", 1);
        private readonly List<UiTransitionFrame> frames = new();
        private EditorWindow previousFocus;

        [SetUp]
        public void SetUp()
        {
            previousFocus = EditorWindow.focusedWindow;
            window = ScriptableObject.CreateInstance<EditorWindow>();
            window.position = new Rect(40, 40, 320, 240); window.Show();
            region = new VisualElement(); region.style.height = 80; region.style.width = 160;
            window.rootVisualElement.Add(region);
            scope = new UiTransitionScope(window.rootVisualElement, () => .16f, () => 10,
                () => EditorApplication.timeSinceStartup);
            scope.Observed += frames.Add;
        }

        [TearDown]
        public void TearDown()
        {
            scope.Retire(); window.Close(); UnityEngine.Object.DestroyImmediate(window);
            if (previousFocus != null) previousFocus.Focus();
            frames.Clear();
        }

        [UnityTest]
        public IEnumerator VisibilityRetainsOutgoingPixelsAndReleasesInput()
        {
            yield return null; scope.Publish();
            var button = new Button(); region.Add(button);
            int clicks = 0; button.clicked += () => clicks++;
            scope.Display(region, DisplayStyle.None, site);
            Assert.That(scope.RequestedDisplay(region), Is.EqualTo(DisplayStyle.None));
            yield return Wait(.04);
            Assert.That(region.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(region.resolvedStyle.opacity, Is.InRange(.001f, .999f));
            using (var click = ClickEvent.GetPooled()) { click.target = button; button.SendEvent(click); }
            Assert.That(clicks, Is.Zero);
            yield return Wait(.2);
            Assert.That(region.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(scope.Observe()[0].Moving, Is.False);
            scope.Display(region, DisplayStyle.Flex, site); yield return Wait(.22);
            Assert.That(region.resolvedStyle.opacity, Is.EqualTo(1).Within(.001));
            Assert.That(scope.Observe()[0].RequestedVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator SynchronousHideShowDoesNotRestartOrPublishHiddenState()
        {
            yield return null; scope.Publish();
            scope.Display(region, DisplayStyle.None, site);
            scope.Display(region, DisplayStyle.Flex, site);
            yield return Wait(.04);
            Assert.That(scope.Observe()[0].Moving, Is.False);
            Assert.That(region.resolvedStyle.opacity, Is.EqualTo(1).Within(.001));
            Assert.That(frames.FindAll(frame => frame.Moving), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ReversalStartsAtTheActuallyPresentedOpacity()
        {
            yield return null; scope.Publish();
            scope.Display(region, DisplayStyle.None, site); yield return Wait(.04);
            float before = scope.Observe()[0].Opacity;
            scope.Display(region, DisplayStyle.Flex, site); yield return null;
            Assert.That(scope.Observe()[0].Opacity, Is.LessThan(1));
            Assert.That(before, Is.InRange(.001f, .999f));
            yield return Wait(.22);
            Assert.That(region.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(region.resolvedStyle.opacity, Is.EqualTo(1).Within(.001));
        }

        [UnityTest]
        public IEnumerator ContentReplacementKeepsOldContentUntilItsExitAndUsesLatestPublication()
        {
            yield return null; scope.Publish();
            string content = "first";
            scope.Replace(region, 1, () => content = "first", site);
            scope.Replace(region, 2, () => content = "superseded", site);
            scope.Replace(region, 3, () => content = "latest", site);
            Assert.That(content, Is.EqualTo("first"));
            yield return Wait(.04);
            Assert.That(content, Is.EqualTo("first"));
            yield return Wait(.24);
            Assert.That(content, Is.EqualTo("latest"));
            Assert.That(scope.Observe()[0].Moving, Is.False);
            Assert.That(region.resolvedStyle.opacity, Is.EqualTo(1).Within(.001));
        }

        [UnityTest]
        public IEnumerator ContentReentryCancelsTheUnpublishedReplacement()
        {
            yield return null; scope.Publish();
            string content = "first";
            scope.Replace(region, 1, () => content = "first", site);
            scope.Replace(region, 2, () => content = "wrong", site); yield return Wait(.04);
            scope.Replace(region, 1, () => content = "first", site); yield return Wait(.24);
            Assert.That(content, Is.EqualTo("first"));
            Assert.That(scope.Observe()[0].Moving, Is.False);
            Assert.That(region.resolvedStyle.opacity, Is.EqualTo(1).Within(.001));
        }

        [UnityTest]
        public IEnumerator DetachRetiresPendingPublicationAndSupportsReuse()
        {
            yield return null; scope.Publish();
            bool late = false;
            scope.Replace(region, 1, () => { }, site);
            scope.Replace(region, 2, () => late = true, site);
            region.RemoveFromHierarchy(); yield return Wait(.24);
            Assert.That(late, Is.False); Assert.That(scope.RegionCount, Is.Zero);
            window.rootVisualElement.Add(region); yield return null;
            scope.Display(region, DisplayStyle.None, site); yield return Wait(.22);
            Assert.That(region.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator AuthoredOpacityTransformAndFlowAreRestored()
        {
            region.style.opacity = .7f; region.style.translate = new Translate(5, 7, 0);
            yield return null; scope.Publish();
            scope.Display(region, DisplayStyle.None, site); yield return Wait(.22);
            scope.Display(region, DisplayStyle.Flex, site); yield return Wait(.22);
            Assert.That(region.style.opacity.value, Is.EqualTo(.7f));
            Assert.That(region.style.translate.value.x.value, Is.EqualTo(5));
            Assert.That(region.style.translate.value.y.value, Is.EqualTo(7));
            Assert.That(region.style.position.keyword, Is.EqualTo(StyleKeyword.Null));
            Assert.That(region.layout.height, Is.EqualTo(80).Within(.1));
        }

        private static IEnumerator Wait(double seconds)
        {
            double deadline = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < deadline) yield return null;
        }
    }
}
