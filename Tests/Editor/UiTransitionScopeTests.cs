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
        private readonly UiTransitionSite site = new("Fixture.cs", "Stimulus", 1, "region");
        private readonly List<UiTransitionFrame> frames = new();
        private EditorWindow previousFocus;
        private ControlledTransitionRenderer renderer;

        [SetUp]
        public void SetUp()
        {
            previousFocus = EditorWindow.focusedWindow;
            window = ScriptableObject.CreateInstance<EditorWindow>();
            window.position = new Rect(40, 40, 320, 240); window.Show();
            region = new VisualElement(); region.style.height = 80; region.style.width = 160;
            window.rootVisualElement.Add(region);
            renderer = new ControlledTransitionRenderer();
            scope = new UiTransitionScope(window.rootVisualElement, () => .16f, () => 10,
                () => EditorApplication.timeSinceStartup, renderer);
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
            bool retained = false;
            scope.Observed += frame =>
            {
                if (frame.Phase != UiTransitionPhase.Exiting || frame.Opacity <= 0 || frame.Opacity >= 1) return;
                Assert.That(region.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                using (var click = ClickEvent.GetPooled()) { click.target = button; button.SendEvent(click); }
                Assert.That(clicks, Is.Zero); retained = true;
            };
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Hidden);
            Assert.That(retained, Is.True, History());
            Assert.That(region.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(scope.Observe()[0].Moving, Is.False);
            scope.Display(region, DisplayStyle.Flex, site);
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Shown);
            Assert.That(region.resolvedStyle.opacity, Is.EqualTo(1).Within(.001));
            Assert.That(scope.Observe()[0].RequestedVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator LiveHierarchyMutationIsRejectedUntilActualScopeRetirement()
        {
            yield return null;
            var child = new VisualElement(); region.Add(child);
            scope.Publish();
            Assert.Throws<InvalidOperationException>(() => scope.ClearChildren(region));
            Assert.Throws<InvalidOperationException>(() => scope.RemoveElement(region));
            Assert.That(region.Contains(child), Is.True);
            Assert.That(region.parent, Is.SameAs(window.rootVisualElement));
            scope.Retire();
            scope.ClearChildren(region);
            Assert.That(region.childCount, Is.Zero);
            scope.RemoveElement(region);
            Assert.That(region.parent, Is.Null);
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
            bool reversed = false, continuous = false;
            scope.Observed += frame =>
            {
                if (!reversed && frame.Phase == UiTransitionPhase.Exiting && frame.Opacity > 0 && frame.Opacity < 1)
                { reversed = true; scope.Display(region, DisplayStyle.Flex, site); }
                else if (reversed && frame.Phase == UiTransitionPhase.Entering && frame.Opacity < 1) continuous = true;
            };
            scope.Display(region, DisplayStyle.None, site);
            yield return Until(() => reversed && !scope.Observe()[0].Moving);
            Assert.That(continuous, Is.True, History());
            Assert.That(region.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(region.resolvedStyle.opacity, Is.EqualTo(1).Within(.001));
        }

        [UnityTest]
        public IEnumerator ContentReplacementRetainsSourceUntilCaptureAndNewLayoutAndUsesLatestPublication()
        {
            yield return null; scope.Publish();
            string content = "first";
            scope.Replace(region, 1, () => content = "first", site);
            scope.Replace(region, 2, () => content = "superseded", site);
            scope.Replace(region, 3, () => content = "latest", site);
            Assert.That(content, Is.EqualTo("first"));
            yield return Until(() => renderer.CapturePending);
            Assert.That(content, Is.EqualTo("first"));
            Assert.That(scope.PendingFrameWait, Is.EqualTo(UiTransitionPhase.CapturingReplacement));
            renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending);
            Assert.That(content, Is.EqualTo("latest"));
            Assert.That(renderer.Image.Opacity, Is.EqualTo(1));
            Assert.That(scope.HasRetainedImage, Is.True);
            Assert.That(scope.Observe()[0].Phase, Is.EqualTo(UiTransitionPhase.WaitingReplacementLayout));
            Assert.That(scope.PendingFrameWait, Is.EqualTo(UiTransitionPhase.WaitingReplacementLayout));
            bool compositeMidframe = false;
            scope.Observed += frame =>
            {
                if (frame.Phase == UiTransitionPhase.Replacing && frame.Opacity > 0 && frame.Opacity < 1)
                {
                    Assert.That(content, Is.EqualTo("latest"), History());
                    Assert.That(frame.SourcePixelsRetained, Is.True);
                    Assert.That(renderer.Image.Opacity, Is.EqualTo(1 - frame.Opacity).Within(.001));
                    compositeMidframe = true;
                }
            };
            renderer.CompleteRender();
            yield return Until(() => content == "latest" && !scope.Observe()[0].Moving);
            Assert.That(compositeMidframe, Is.True, History());
            Assert.That(content, Is.EqualTo("latest"));
            Assert.That(scope.Observe()[0].Moving, Is.False);
            Assert.That(scope.HasRetainedImage, Is.False);
            Assert.That(renderer.Image.Disposals, Is.EqualTo(1));
            Assert.That(region.resolvedStyle.opacity, Is.EqualTo(1).Within(.001));
        }

        [UnityTest]
        public IEnumerator ContentReentryCancelsTheUnpublishedReplacement()
        {
            yield return null; scope.Publish();
            string content = "first";
            scope.Replace(region, 1, () => content = "first", site);
            scope.Replace(region, 2, () => content = "wrong", site);
            yield return Until(() => renderer.CapturePending);
            var capture = renderer.CaptureTask;
            scope.Replace(region, 1, () => content = "first", site);
            Assert.That(capture.IsCanceled, Is.True);
            yield return Until(() => !scope.Observe()[0].Moving);
            Assert.That(content, Is.EqualTo("first"));
            Assert.That(scope.Observe()[0].Moving, Is.False);
            Assert.That(region.resolvedStyle.opacity, Is.EqualTo(1).Within(.001), History());
        }

        [UnityTest]
        public IEnumerator LatestRequestAtPublicationBoundaryWaitsForItsOwnSourceCapture()
        {
            yield return null; scope.Publish();
            string content = "first"; bool boundary = false;
            scope.Replace(region, 1, () => content = "first", site);
            scope.Observed += frame =>
            {
                if (!boundary && frame.Phase == UiTransitionPhase.WaitingReplacementLayout)
                { boundary = true; scope.Replace(region, 3, () => content = "boundary", site); }
            };
            scope.Replace(region, 2, () => content = "second", site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending);
            Assert.That(content, Is.EqualTo("second")); renderer.CompleteRender();
            yield return Until(() => renderer.CapturePending);
            Assert.That(content, Is.EqualTo("second"));
            Assert.That(renderer.Image.Disposals, Is.EqualTo(1)); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending); renderer.CompleteRender();
            yield return Until(() => boundary && content == "boundary" && !scope.Observe()[0].Moving);
            Assert.That(content, Is.EqualTo("boundary"), History());
            Assert.That(region.resolvedStyle.opacity, Is.EqualTo(1).Within(.001), History());
        }

        [UnityTest]
        public IEnumerator SourceWaitAdmissionCanRetireItsExactIncompleteTask()
        {
            yield return null; scope.Publish();
            bool observed = false;
            scope.FrameWaitStarted += (phase, wait) =>
            {
                if (phase != UiTransitionPhase.CapturingReplacement) return;
                Assert.That(wait.IsCompleted, Is.False);
                Assert.That(wait, Is.SameAs(renderer.CaptureTask));
                scope.Retire();
                Assert.That(wait.IsCanceled, Is.True); observed = true;
            };
            scope.Replace(region, 1, () => { }, site);
            scope.Replace(region, 2, () => Assert.Fail("Retired content published late."), site);
            yield return Until(() => observed);
            Assert.That(scope.RegionCount, Is.Zero);
            Assert.That(scope.HasRetainedImage, Is.False);
        }

        [UnityTest]
        public IEnumerator TargetWaitAdmissionCanRetireItsExactIncompleteTaskAndImage()
        {
            yield return null; scope.Publish();
            bool observed = false;
            scope.FrameWaitStarted += (phase, wait) =>
            {
                if (phase != UiTransitionPhase.WaitingReplacementLayout) return;
                Assert.That(wait.IsCompleted, Is.False);
                Assert.That(wait, Is.SameAs(renderer.RenderTask));
                scope.Retire();
                Assert.That(wait.IsCanceled, Is.True); observed = true;
            };
            scope.Replace(region, 1, () => { }, site);
            scope.Replace(region, 2, () => { }, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => observed);
            Assert.That(renderer.Image.Disposals, Is.EqualTo(1));
            Assert.That(scope.RegionCount, Is.Zero);
            Assert.That(scope.HasRetainedImage, Is.False);
        }

        [UnityTest]
        public IEnumerator RootRetirementJoinsTheOwnedFrameWaitAndReleasesTheRetainedImage()
        {
            yield return null; scope.Publish();
            scope.Replace(region, 1, () => { }, site);
            scope.Replace(region, 2, () => { }, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending);
            var wait = renderer.RenderTask;
            scope.Retire();
            Assert.That(wait.IsCanceled, Is.True);
            Assert.That(scope.PendingFrameWait, Is.Null);
            Assert.That(scope.HasRetainedImage, Is.False);
            Assert.That(renderer.Image.Disposals, Is.EqualTo(1));
            Assert.That(scope.RegionCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ObserverFailureCannotSkipImageWaitOrRegionRetirement()
        {
            yield return null; scope.Publish();
            scope.Replace(region, 1, () => { }, site);
            scope.Replace(region, 2, () => { }, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending);
            var wait = renderer.RenderTask;
            void FailingObserver(UiTransitionFrame frame) => throw new InvalidOperationException("observer witness");
            scope.Observed += FailingObserver;
            try { Assert.Throws<AggregateException>(scope.Retire); }
            finally { scope.Observed -= FailingObserver; }
            Assert.That(wait.IsCanceled, Is.True);
            Assert.That(scope.HasRetainedImage, Is.False);
            Assert.That(renderer.Image.Disposals, Is.EqualTo(1));
            Assert.That(scope.RegionCount, Is.Zero);
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
            scope.Display(region, DisplayStyle.None, site);
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Hidden);
            scope.Display(region, DisplayStyle.Flex, site);
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Shown);
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

        private IEnumerator Until(Func<bool> completed)
        {
            double deadline = EditorApplication.timeSinceStartup + 2;
            while (!completed() && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(completed(), Is.True, History());
            yield return null;
        }

        private string History() => string.Join("\n", frames.ConvertAll(frame =>
            $"{frame.Seconds:R} {frame.Phase} visible={frame.RequestedVisible} opacity={frame.Opacity:R}"));
    }
}
