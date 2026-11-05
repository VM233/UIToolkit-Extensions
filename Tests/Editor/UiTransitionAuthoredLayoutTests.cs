using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace VM233.UIElements.Editor.Tests
{
    public sealed class UiTransitionAuthoredLayoutTests
    {
        private EditorWindow window, previousFocus;
        private VisualElement root, region;
        private UiTransitionScope scope;
        private readonly UiTransitionSite site = new("AuthoredFixture.cs", "Visibility", 1, "region");

        [SetUp]
        public void SetUp()
        {
            previousFocus = EditorWindow.focusedWindow;
            window = ScriptableObject.CreateInstance<EditorWindow>();
            window.position = new Rect(40, 40, 320, 260); window.Show();
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.vm233.ui-toolkit-extensions/Tests/Editor/Fixtures/Transition Authored Layout.uxml");
            Assert.That(asset, Is.Not.Null);
            root = asset.CloneTree(); window.rootVisualElement.Add(root);
            region = root.Q("AuthoredRegion");
            scope = new UiTransitionScope(root, () => .16f, () => 10,
                () => EditorApplication.timeSinceStartup, new ControlledTransitionRenderer());
        }

        [TearDown]
        public void TearDown()
        {
            try { scope?.Retire(); }
            finally { window.Close(); UnityEngine.Object.DestroyImmediate(window); }
            if (previousFocus != null) previousFocus.Focus();
        }

        [UnityTest]
        public IEnumerator UxmlGeometryIsDistinctFromProgrammaticInlineGetters()
        {
            yield return null;
            Assert.That(region.style.position.keyword, Is.EqualTo(StyleKeyword.Null));
            Assert.That(region.style.width.keyword, Is.EqualTo(StyleKeyword.Null));
            RequireAuthoredGeometry(region.layout, ReadPadding());
        }

        [UnityTest]
        public IEnumerator AuthoredGeometrySurvivesVisibilityAndSameRootRetirement()
        {
            yield return null;
            Rect original = region.layout; Vector2 padding = ReadPadding();
            RequireAuthoredGeometry(original, padding);
            TestContext.WriteLine($"Native authored fixture pixelsPerPoint={EditorGUIUtility.pixelsPerPoint:R}, padding={padding.x:R},{padding.y:R}.");
            scope.Publish();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                scope.Display(region, DisplayStyle.None, site);
                yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Hidden);
                scope.Display(region, DisplayStyle.Flex, site);
                yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Shown);
                RequireAuthoredGeometry(original, padding);
                scope.Retire(); scope.Publish();
                RequireAuthoredGeometry(original, padding);
            }
        }

        [UnityTest]
        public IEnumerator OutgoingFlowRemainsUntilItsTerminalHiddenState()
        {
            region.style.position = Position.Relative;
            var sibling = new VisualElement(); sibling.style.height = 20; region.parent.Add(sibling);
            yield return null;
            float original = sibling.layout.y;
            scope.Publish(); bool observed = false;
            scope.Observed += frame =>
            {
                if (frame.Phase != UiTransitionPhase.Exiting || frame.Opacity is <= 0 or >= 1) return;
                Assert.That(region.resolvedStyle.position, Is.EqualTo(Position.Relative));
                Assert.That(sibling.layout.y, Is.EqualTo(original).Within(.1)); observed = true;
            };
            scope.Display(region, DisplayStyle.None, site);
            yield return Until(() => scope.Observe()[0].Phase == UiTransitionPhase.Hidden);
            Assert.That(observed, Is.True);
            Assert.That(sibling.layout.y, Is.LessThan(original));
        }

        private Vector2 ReadPadding() => new(region.resolvedStyle.paddingLeft, region.resolvedStyle.paddingTop);

        private void RequireAuthoredGeometry(Rect original, Vector2 padding)
        {
            Assert.That(region.resolvedStyle.position, Is.EqualTo(Position.Absolute));
            Assert.That(region.layout.x, Is.EqualTo(original.x).Within(.1));
            Assert.That(region.layout.y, Is.EqualTo(original.y).Within(.1));
            Assert.That(region.layout.width, Is.EqualTo(160).Within(.1));
            Assert.That(region.layout.height, Is.EqualTo(80).Within(.1));
            Assert.That(padding.x, Is.GreaterThan(0)); Assert.That(padding.y, Is.GreaterThan(0));
            Assert.That(region.resolvedStyle.paddingLeft, Is.EqualTo(padding.x).Within(.1));
            Assert.That(region.resolvedStyle.paddingTop, Is.EqualTo(padding.y).Within(.1));
        }

        private static IEnumerator Until(Func<bool> completed)
        {
            double deadline = EditorApplication.timeSinceStartup + 2;
            while (!completed() && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(completed(), Is.True); yield return null;
        }
    }
}
