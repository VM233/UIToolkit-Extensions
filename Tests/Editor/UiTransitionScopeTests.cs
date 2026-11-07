using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        private Func<double> clock;
        private double controlledSeconds;

        [SetUp]
        public void SetUp()
        {
            previousFocus = EditorWindow.focusedWindow;
            window = ScriptableObject.CreateInstance<EditorWindow>();
            window.position = new Rect(40, 40, 320, 240); window.Show();
            region = new VisualElement(); region.style.height = 80; region.style.width = 160;
            window.rootVisualElement.Add(region);
            renderer = new ControlledTransitionRenderer();
            clock = () => EditorApplication.timeSinceStartup;
            scope = new UiTransitionScope(window.rootVisualElement, () => .16f, () => 10,
                () => clock(), renderer);
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
        public IEnumerator NativeFocusWaitsForEnterAndExitSettlement()
        {
            var target = new Button(); region.Add(target);
            var opener = new Button(); window.rootVisualElement.Add(opener);
            region.style.display = DisplayStyle.None;
            yield return null; scope.Publish(); opener.Focus();
            scope.Display(region, DisplayStyle.Flex, site); scope.FocusAfter(target, region);
            Assert.That(target.focusController.focusedElement, Is.Not.SameAs(target));
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Shown);
            Assert.That(target.focusController.focusedElement, Is.SameAs(target));
            scope.Display(region, DisplayStyle.None, site); scope.FocusAfter(opener, region);
            Assert.That(opener.focusController.focusedElement, Is.Not.SameAs(opener));
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Hidden);
            Assert.That(opener.focusController.focusedElement, Is.SameAs(opener));
        }

        [UnityTest]
        public IEnumerator LayoutVisibilityRetainsSpaceAndBlocksItsOutgoingInput()
        {
            var button = new Button(); region.Add(button);
            int clicks = 0; button.clicked += () => clicks++;
            yield return null; scope.Publish();
            Rect original = region.layout;
            scope.Visibility(region, Visibility.Hidden, site);
            bool intermediate = false;
            scope.Observed += frame =>
            {
                if (frame.Phase != UiTransitionPhase.Exiting || frame.Opacity <= 0 || frame.Opacity >= 1) return;
                Assert.That(region.resolvedStyle.visibility, Is.EqualTo(Visibility.Visible));
                using (var click = ClickEvent.GetPooled()) { click.target = button; button.SendEvent(click); }
                intermediate = true;
            };
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Hidden);
            Assert.That(intermediate, Is.True, History()); Assert.That(clicks, Is.Zero);
            Assert.That(region.layout, Is.EqualTo(original));
            Assert.That(region.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(region.resolvedStyle.visibility, Is.EqualTo(Visibility.Hidden));
            scope.Visibility(region, StyleKeyword.Null, site);
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Shown);
            Assert.That(region.style.visibility.keyword, Is.EqualTo(StyleKeyword.Null));
            Assert.That(region.layout, Is.EqualTo(original));
        }

        [UnityTest]
        public IEnumerator DisplayAndLayoutVisibilityShareOneSegmentAndBothMustAdmitRendering()
        {
            yield return null; scope.Publish();
            scope.Visibility(region, Visibility.Hidden, site);
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Hidden);
            scope.Display(region, DisplayStyle.None, site); scope.Visibility(region, StyleKeyword.Null, site);
            yield return Until(() => region.style.display.value == DisplayStyle.None);
            Assert.That(scope.Observe()[0].RequestedVisible, Is.False);
            scope.Display(region, DisplayStyle.Flex, site);
            yield return Until(() => frames.Any(frame => frame.Phase == UiTransitionPhase.Entering && frame.Opacity > 0 && frame.Opacity < 1));
            scope.Visibility(region, Visibility.Hidden, site);
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Hidden);
            Assert.That(scope.RegionCount, Is.EqualTo(1));
            Assert.That(region.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(region.resolvedStyle.visibility, Is.EqualTo(Visibility.Hidden));
            scope.Retire(); Assert.That(scope.RegionCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ProjectionRequestsCoalesceAndRevealPreservesInheritedVisibility()
        {
            var child = new VisualElement(); region.Add(child);
            yield return null; scope.Publish();
            scope.Visibility(child, Visibility.Hidden, site); scope.Visibility(child, StyleKeyword.Null, site);
            yield return null;
            Assert.That(scope.Observe().Any(frame => frame.Moving), Is.False);
            Assert.That(child.style.visibility.keyword, Is.EqualTo(StyleKeyword.Null));
            scope.Visibility(region, Visibility.Hidden, site);
            yield return Until(() => scope.Observe().Any(frame => ReferenceEquals(frame.Element, region) && frame.Phase == UiTransitionPhase.Hidden));
            yield return null;
            Assert.That(child.resolvedStyle.visibility, Is.EqualTo(Visibility.Hidden));
            scope.Visibility(child, StyleKeyword.Null, site);
            Assert.That(child.style.visibility.keyword, Is.EqualTo(StyleKeyword.Null));
            Assert.That(scope.Observe().Single(frame => ReferenceEquals(frame.Element, child)).Moving, Is.False);
        }

        [UnityTest]
        public IEnumerator PublicationFocusWaitsAndRetirementConsumesItsIntent()
        {
            var target = new Button(); region.Add(target);
            var opener = new Button(); window.rootVisualElement.Add(opener);
            yield return null; scope.Publish(); opener.Focus();
            scope.Replace(region, 1, () => { }, site);
            scope.Replace(region, 2, () => scope.FocusAfter(target, region), site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending);
            Assert.That(target.focusController.focusedElement, Is.Not.SameAs(target));
            renderer.CompleteRender();
            yield return Until(() => !scope.Observe()[0].Moving);
            Assert.That(target.focusController.focusedElement, Is.SameAs(target));
            opener.Focus();
            scope.Replace(region, 3, () => { }, site); scope.FocusAfter(target, region);
            yield return Until(() => renderer.CapturePending);
            scope.Retire(); scope.Publish();
            yield return null;
            Assert.That(target.focusController.focusedElement, Is.Not.SameAs(target));
        }

        [UnityTest]
        public IEnumerator ReversalToHiddenCannotFocusOrKeepAnObsoleteIntent()
        {
            var target = new Button(); region.Add(target);
            var opener = new Button(); window.rootVisualElement.Add(opener);
            region.style.display = DisplayStyle.None;
            yield return null; scope.Publish(); opener.Focus();
            scope.Display(region, DisplayStyle.Flex, site); scope.FocusAfter(target, region);
            yield return Until(() => frames.Any(frame => frame.Phase == UiTransitionPhase.Entering));
            scope.Display(region, DisplayStyle.None, site);
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Hidden);
            Assert.That(target.focusController.focusedElement, Is.Not.SameAs(target));
            scope.Display(region, DisplayStyle.Flex, site);
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Shown);
            Assert.That(target.focusController.focusedElement, Is.Not.SameAs(target));
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
        public IEnumerator HiddenAttachmentRetainsNativeLayoutPickingAndLaterEntranceOwnership()
        {
            yield return null; scope.Publish();
            var child = new Button(); child.style.width = 40; child.style.height = 20;
            child.style.display = DisplayStyle.None;
            scope.AttachHidden(region, child);
            yield return null;
            Assert.That(child.parent, Is.SameAs(region));
            Assert.That(child.panel, Is.SameAs(region.panel));
            Assert.That(child.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(child.layout.width, Is.Zero); Assert.That(child.layout.height, Is.Zero);
            Assert.That(scope.RegionCount, Is.Zero);
            Assert.That(renderer.CapturePending, Is.False);
            Assert.That(scope.PendingFrameWait, Is.Null);
            scope.Display(child, DisplayStyle.Flex, new UiTransitionSite("Fixture.cs", "Attach", 1, "child"));
            yield return Until(() => scope.Observe().Single().Phase == UiTransitionPhase.Shown);
            Assert.That(frames.Any(frame => frame.Element == child && frame.Phase == UiTransitionPhase.Entering &&
                frame.Opacity is > 0 and < 1), Is.True, History());
            Assert.That(child.layout.width, Is.GreaterThan(0)); Assert.That(child.layout.height, Is.GreaterThan(0));
            var picked = child.panel.Pick(child.worldBound.center);
            Assert.That(ReferenceEquals(picked, child) || child.Contains(picked), Is.True);
            Assert.That(renderer.CapturePending, Is.False);
            scope.Retire(); Assert.That(scope.RegionCount, Is.Zero); Assert.That(scope.HasRetainedImage, Is.False);
        }

        [UnityTest]
        public IEnumerator HiddenAttachmentRejectsVisibleOwnedAndForeignElementsWithoutReparenting()
        {
            yield return null; scope.Publish();
            var visible = new VisualElement(); visible.style.display = DisplayStyle.Flex;
            Assert.Throws<InvalidOperationException>(() => scope.AttachHidden(region, visible));
            Assert.That(visible.parent, Is.Null);
            var child = new VisualElement(); child.style.display = DisplayStyle.None;
            var foreign = new VisualElement();
            Assert.Throws<ArgumentException>(() => scope.AttachHidden(foreign, child));
            Assert.That(child.parent, Is.Null);
            scope.AttachHidden(region, child);
            Assert.Throws<InvalidOperationException>(() => scope.AttachHidden(window.rootVisualElement, child));
            Assert.That(child.parent, Is.SameAs(region));
            Assert.Throws<InvalidOperationException>(() => scope.Initialize(region, () => visible.Add(child)));
            Assert.That(child.parent, Is.SameAs(region));
        }

        [UnityTest]
        public IEnumerator InitializationRequiresTheActualUnpublishedDocument()
        {
            yield return null;
            int calls = 0;
            scope.Initialize(region, () => { calls++; region.Add(new Label("initial")); });
            Assert.That(calls, Is.EqualTo(1));
            scope.Publish();
            Assert.Throws<InvalidOperationException>(() => scope.Initialize(region, () => calls++));
            Assert.That(calls, Is.EqualTo(1));
            scope.Retire();
            scope.Initialize(region, () => { calls++; region.Clear(); region.Add(new Label("rebound")); });
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(region.Q<Label>().text, Is.EqualTo("rebound"));
            Assert.Throws<ArgumentException>(() => scope.Initialize(new VisualElement(), () => calls++));
            Assert.That(calls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator TheActualDocumentRootBelongsToItsOwnUnpublishedScope()
        {
            yield return null;
            var root = window.rootVisualElement;
            Assert.That(root.Contains(root), Is.False, "Unity Contains tests strict descendants.");
            int calls = 0;
            scope.Initialize(root, () => calls++);
            Assert.That(calls, Is.EqualTo(1));
            scope.Publish();
            Assert.Throws<InvalidOperationException>(() => scope.Initialize(root, () => calls++));
            Assert.That(calls, Is.EqualTo(1));
            scope.Retire(); scope.Initialize(root, () => calls++);
            Assert.That(calls, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => scope.Initialize(new VisualElement(), () => calls++));
            Assert.That(calls, Is.EqualTo(2));
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
            var firstImage = renderer.Image;
            yield return Until(() => boundary && renderer.CapturePending);
            Assert.That(content, Is.EqualTo("second"));
            Assert.That(renderer.RenderPending, Is.False);
            Assert.That(firstImage.Disposals, Is.Zero); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending); renderer.CompleteRender();
            Assert.That(firstImage.Disposals, Is.EqualTo(1));
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
        public IEnumerator UncapturedSiblingWaitsForItsOwnPublicationImage()
        {
            var sibling = new VisualElement(); sibling.style.width = 160; sibling.style.height = 80;
            window.rootVisualElement.Add(sibling);
            yield return null; scope.Publish();
            string first = "old", second = "old";
            scope.Replace(region, 1, () => { }, site);
            scope.Replace(sibling, 1, () => { }, site);
            scope.Replace(region, 2, () =>
            {
                first = "new";
                scope.Replace(sibling, 2, () => second = "new", site);
            }, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            var firstImage = renderer.Image;
            yield return Until(() => first == "new" && renderer.CapturePending);
            Assert.That(first, Is.EqualTo("new"));
            Assert.That(second, Is.EqualTo("old"), "A sibling outside this captured group published immediately.");
            Assert.That(firstImage.Disposals, Is.Zero);
            Assert.That(second, Is.EqualTo("old")); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending); renderer.CompleteRender();
            yield return Until(() => !scope.Observe().Any(frame => frame.Moving));
            Assert.That(second, Is.EqualTo("new"));
            Assert.That(firstImage.Disposals, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContentRedirectionFreezesItsCompositeBeforeCapturingTheSuccessor()
        {
            int content = 0;
            yield return BeginControlledReplacement(value => content = value);
            var original = renderer.Image; float opacity = original.Opacity;
            scope.Replace(region, 3, () => content = 3, site);
            Assert.That(renderer.CapturePending, Is.True);
            Assert.That(original.Disposals, Is.Zero);
            yield return null;
            Assert.That(original.Opacity, Is.EqualTo(opacity));
            Assert.That(content, Is.EqualTo(2));
            renderer.CompleteCapture();
            Assert.That(renderer.Image, Is.Not.SameAs(original));
            yield return FinishControlledReplacement();
            Assert.That(content, Is.EqualTo(3));
            Assert.That(original.Disposals, Is.EqualTo(1));
            Assert.That(renderer.Image.Disposals, Is.EqualTo(1));
            Assert.That(original.Opacity, Is.EqualTo(opacity), "The obsolete source must stay frozen through successor settlement.");
        }

        [UnityTest]
        public IEnumerator ContentRedirectionCancelsTheExactTargetWaitAndRetainsItsOpaqueSource()
        {
            controlledSeconds = 0; clock = () => controlledSeconds;
            yield return null; scope.Publish();
            int content = 1;
            scope.Replace(region, 1, () => content = 1, site);
            scope.Replace(region, 2, () => content = 2, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending);
            var wait = renderer.RenderTask; var original = renderer.Image;
            scope.Replace(region, 3, () => content = 3, site);
            Assert.That(wait.IsCanceled, Is.True);
            Assert.That(renderer.CapturePending, Is.True);
            Assert.That(original.Disposals, Is.Zero);
            Assert.That(original.Opacity, Is.EqualTo(1));
            Assert.That(content, Is.EqualTo(2));
            renderer.CompleteCapture(); yield return FinishControlledReplacement();
            Assert.That(content, Is.EqualTo(3));
            Assert.That(original.Disposals, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContentRedirectionCoalescesAndReselectingThePublishedTargetKeepsTheComposite()
        {
            int content = 0, obsolete = 0;
            yield return BeginControlledReplacement(value => content = value);
            var original = renderer.Image; float opacity = original.Opacity;
            scope.Replace(region, 3, () => obsolete++, site);
            var wait = renderer.CaptureTask;
            scope.Replace(region, 4, () => obsolete++, site);
            scope.Replace(region, 2, () => content = 2, site);
            Assert.That(renderer.CaptureTask, Is.SameAs(wait));
            Assert.That(wait.IsCompleted, Is.False);
            Assert.That(original.Opacity, Is.EqualTo(opacity));
            Assert.That(original.Disposals, Is.Zero);
            renderer.CompleteCapture(); yield return FinishControlledReplacement();
            Assert.That(content, Is.EqualTo(2)); Assert.That(obsolete, Is.Zero);
            Assert.That(original.Disposals, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContentRedirectionPreservesTheActualImageRectangleAfterLiveGeometryChanges()
        {
            yield return BeginControlledReplacement(_ => { });
            var original = renderer.Image;
            scope.Replace(region, 3, () => { }, site);
            region.style.width = 40; region.style.height = 20;
            yield return null;
            Assert.That(region.worldBound.width, Is.LessThan(original.Bounds.width));
            renderer.CompleteCapture();
            var captured = renderer.Image.Bounds;
            Assert.That(captured.xMin, Is.LessThanOrEqualTo(original.Bounds.xMin));
            Assert.That(captured.yMin, Is.LessThanOrEqualTo(original.Bounds.yMin));
            Assert.That(captured.xMax, Is.GreaterThanOrEqualTo(original.Bounds.xMax));
            Assert.That(captured.yMax, Is.GreaterThanOrEqualTo(original.Bounds.yMax));
            yield return FinishControlledReplacement();
        }

        [UnityTest]
        public IEnumerator PendingAncestorMayReplaceItsFrozenDescendantWithoutAbortingTheNewImage()
        {
            controlledSeconds = 0; clock = () => controlledSeconds;
            var child = new VisualElement(); child.style.width = 80; child.style.height = 40; region.Add(child);
            yield return null; scope.Publish();
            scope.Replace(region, 1, () => { }, site); scope.Replace(child, 1, () => { }, site);
            scope.Replace(child, 2, () => { }, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending); renderer.CompleteRender();
            yield return Until(() => scope.Observe().Any(frame => frame.Phase == UiTransitionPhase.Replacing));
            controlledSeconds = .08;
            yield return Until(() => scope.Observe().Any(frame => frame.Phase == UiTransitionPhase.Replacing && frame.Opacity is > 0 and < 1));
            var original = renderer.Image;
            scope.Replace(region, 2, region.Clear, site);
            renderer.CompleteCapture();
            yield return FinishControlledReplacement();
            Assert.That(child.parent, Is.Null);
            Assert.That(scope.RegionCount, Is.EqualTo(1));
            Assert.That(original.Disposals, Is.EqualTo(1));
            Assert.That(renderer.Image.Disposals, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContentRedirectionCarriesItsAlreadyPublishedSiblingThroughTheNewFade()
        {
            controlledSeconds = 0; clock = () => controlledSeconds;
            var sibling = new VisualElement(); sibling.style.width = 160; sibling.style.height = 80;
            window.rootVisualElement.Add(sibling);
            yield return null; scope.Publish();
            int siblingPublications = 0;
            scope.Replace(region, 1, () => { }, site); scope.Replace(sibling, 1, () => { }, site);
            scope.Replace(region, 2, () => { }, site); scope.Replace(sibling, 2, () => siblingPublications++, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending); renderer.CompleteRender();
            yield return Until(() => scope.Observe().All(frame => frame.Phase == UiTransitionPhase.Replacing));
            controlledSeconds = .08;
            yield return Until(() => scope.Observe().All(frame => frame.Phase == UiTransitionPhase.Replacing && frame.Opacity is > 0 and < 1));
            scope.Replace(region, 3, () => { }, site); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending);
            Assert.That(scope.Observe().All(frame => frame.SourceCaptureFrame == renderer.Image.CapturedFrame && frame.Moving), Is.True);
            Assert.That(siblingPublications, Is.EqualTo(1));
            renderer.CompleteRender();
            yield return Until(() => scope.Observe().All(frame => frame.Phase == UiTransitionPhase.Replacing));
            controlledSeconds += 1;
            yield return Until(() => scope.Observe().All(frame => !frame.Moving));
            Assert.That(siblingPublications, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContentRedirectionRetirementCancelsItsNewCaptureAndDisposesItsFrozenImage()
        {
            yield return BeginControlledReplacement(_ => { });
            var original = renderer.Image;
            scope.Replace(region, 3, () => Assert.Fail("Retired successor published."), site);
            var wait = renderer.CaptureTask;
            scope.Retire();
            Assert.That(wait.IsCanceled, Is.True);
            Assert.That(original.Disposals, Is.EqualTo(1));
            Assert.That(scope.PendingFrameWait, Is.Null);
            Assert.That(scope.HasRetainedImage, Is.False);
            Assert.That(scope.RegionCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SameOwnerPublicationReentryQueuesItsLatestIntentInsteadOfOverwritingTheAdmittedAction()
        {
            controlledSeconds = 0; clock = () => controlledSeconds;
            yield return null; scope.Publish();
            int content = 1, second = 0, third = 0; bool requested = false;
            scope.Replace(region, 1, () => content = 1, site);
            scope.Observed += frame =>
            {
                if (requested || frame.Phase != UiTransitionPhase.PublishingReplacement) return;
                requested = true;
                scope.Replace(region, 3, () => { content = 3; third++; }, site);
                Assert.That(content, Is.EqualTo(1));
            };
            scope.Replace(region, 2, () => { content = 2; second++; }, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => requested && renderer.CapturePending);
            Assert.That(content, Is.EqualTo(2)); Assert.That(second, Is.EqualTo(1)); Assert.That(third, Is.Zero);
            Assert.That(renderer.Image.Disposals, Is.Zero);
            renderer.CompleteCapture(); yield return FinishControlledReplacement();
            Assert.That(content, Is.EqualTo(3)); Assert.That(second, Is.EqualTo(1)); Assert.That(third, Is.EqualTo(1));
        }

        private IEnumerator BeginControlledReplacement(Action<int> publish)
        {
            controlledSeconds = 0; clock = () => controlledSeconds;
            yield return null; scope.Publish();
            scope.Replace(region, 1, () => publish(1), site); scope.Replace(region, 2, () => publish(2), site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending); renderer.CompleteRender();
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Replacing);
            controlledSeconds = .08;
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Replacing && scope.Observe()[0].Opacity is > 0 and < 1);
        }

        [UnityTest]
        public IEnumerator AnAdmittedPublicationStillOwnsItsSameRegionDisplayProjection()
        {
            yield return null; scope.Publish();
            scope.Replace(region, 1, () => { }, site);
            scope.Replace(region, 2, () =>
            {
                scope.Display(region, DisplayStyle.None, site);
                Assert.That(region.style.display.value, Is.EqualTo(DisplayStyle.None));
            }, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending); renderer.CompleteRender();
            yield return Until(() => !scope.HasRetainedImage);
            Assert.That(region.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        private IEnumerator FinishControlledReplacement()
        {
            yield return Until(() => renderer.RenderPending); renderer.CompleteRender();
            yield return Until(() => scope.Observe().Any(frame => frame.Phase == UiTransitionPhase.Replacing));
            controlledSeconds += 1;
            yield return Until(() => !scope.Observe().Any(frame => frame.Moving));
            Assert.That(scope.HasRetainedImage, Is.False);
        }

        [UnityTest]
        public IEnumerator PublicationObserverRetirementCannotRunRetiredContentOrRenderWait()
        {
            yield return null; scope.Publish();
            scope.Replace(region, 1, () => { }, site);
            bool retired = false, published = false;
            scope.Observed += frame =>
            {
                if (frame.Phase != UiTransitionPhase.PublishingReplacement) return;
                scope.Retire(); retired = true;
            };
            scope.Replace(region, 2, () => published = true, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => retired);
            Assert.That(published, Is.False);
            Assert.That(renderer.RenderPending, Is.False);
            Assert.That(scope.RegionCount, Is.Zero);
            Assert.That(scope.HasRetainedImage, Is.False);
            Assert.That(renderer.Image.Disposals, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PublicationMayRemoveItsActualRegionWithoutStartingALateRenderWait()
        {
            yield return null; scope.Publish();
            scope.Replace(region, 1, () => { }, site);
            scope.Replace(region, 2, region.RemoveFromHierarchy, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => scope.RegionCount == 0);
            Assert.That(scope.Presented, Is.True);
            Assert.That(renderer.RenderPending, Is.False);
            Assert.That(scope.PendingFrameWait, Is.Null);
            Assert.That(scope.HasRetainedImage, Is.False);
            Assert.That(renderer.Image.Disposals, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CompletionObserverRetiresAlreadyConsumedBatchExactlyOnce()
        {
            yield return null; scope.Publish();
            scope.Replace(region, 1, () => { }, site);
            bool retired = false;
            scope.Observed += frame =>
            {
                if (retired || frame.Phase != UiTransitionPhase.Shown || renderer.Image == null) return;
                retired = true; scope.Retire();
            };
            scope.Replace(region, 2, () => { }, site);
            yield return Until(() => renderer.CapturePending); renderer.CompleteCapture();
            yield return Until(() => renderer.RenderPending); renderer.CompleteRender();
            yield return Until(() => retired);
            Assert.That(scope.RegionCount, Is.Zero);
            Assert.That(scope.HasRetainedImage, Is.False);
            Assert.That(scope.PendingFrameWait, Is.Null);
            Assert.That(renderer.Image.Disposals, Is.EqualTo(1));
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
            int remainingFrames = 256;
            while (!completed() && EditorApplication.timeSinceStartup < deadline && remainingFrames-- > 0) yield return null;
            Assert.That(completed(), Is.True, History());
            yield return null;
        }

        private string History() => string.Join("\n", frames.ConvertAll(frame =>
            $"{frame.Seconds:R} {frame.Phase} visible={frame.RequestedVisible} opacity={frame.Opacity:R}"));
    }
}
