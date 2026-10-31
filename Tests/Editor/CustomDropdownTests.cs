using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace VM233.UIElements.Editor.Tests
{
    public class CustomDropdownTests
    {
        private EditorWindow window;

        [SetUp]
        public void SetUp()
        {
            window = ScriptableObject.CreateInstance<EditorWindow>();
            window.Show();
        }

        [TearDown]
        public void TearDown()
        {
            window.Close();
            Object.DestroyImmediate(window);
        }

        [Test]
        public void ConstructorCreatesExpectedHierarchyAndClasses()
        {
            var dropdown = new CustomDropdown();

            Assert.That(dropdown.ClassListContains(CustomDropdown.CLASS_NAME), Is.True);
            Assert.That(dropdown.Toggle, Is.Not.Null);
            Assert.That(dropdown.DropdownContainerWrapper, Is.Not.Null);
            Assert.That(dropdown.DropdownContainer, Is.Not.Null);
            Assert.That(dropdown.Toggle.ClassListContains(CustomDropdown.TOGGLE_CLASS_NAME), Is.True);
            Assert.That(dropdown.DropdownContainerWrapper.ClassListContains(
                CustomDropdown.DROPDOWN_CONTAINER_WRAPPER_CLASS_NAME), Is.True);
            Assert.That(dropdown.DropdownContainer.ClassListContains(
                CustomDropdown.DROPDOWN_CONTAINER_CLASS_NAME), Is.True);
        }

        [Test]
        public void SetOptionsBuildsEntriesAndPreservesSelectedValue()
        {
            var dropdown = new CustomDropdown();
            dropdown.SetOptions(new[] { "One", "Two", "Three" });
            dropdown.SetSelectedIndexWithoutNotify(1);

            dropdown.SetOptions(new[] { "Two", "Four" });

            Assert.That(dropdown.Options, Is.EqualTo(new[] { "Two", "Four" }));
            Assert.That(dropdown.Entries.Count, Is.EqualTo(2));
            Assert.That(dropdown.SelectedIndex, Is.EqualTo(0));
            Assert.That(dropdown.value, Is.EqualTo("Two"));
            Assert.That(dropdown.Toggle.text, Is.EqualTo("Two"));
        }

        [Test]
        public void SelectedIndexNotifiesSelectionAndValueChange()
        {
            var dropdown = new CustomDropdown
            {
                Options = new List<string> { "One", "Two" }
            };
            var selectionIndex = -1;
            var changeCount = 0;
            string selectionValue = null;
            string previousValue = null;
            string newValue = null;

            window.rootVisualElement.Add(dropdown);

            dropdown.SelectionChanged += (index, value) =>
            {
                selectionIndex = index;
                selectionValue = value;
            };
            dropdown.RegisterValueChangedCallback(evt =>
            {
                changeCount++;
                previousValue = evt.previousValue;
                newValue = evt.newValue;
            });

            dropdown.SelectedIndex = 1;

            Assert.That(selectionIndex, Is.EqualTo(1));
            Assert.That(selectionValue, Is.EqualTo("Two"));
            Assert.That(changeCount, Is.EqualTo(1));
            Assert.That(previousValue, Is.EqualTo("One"));
            Assert.That(newValue, Is.EqualTo("Two"));
        }

        [Test]
        public void SetValueWithoutNotifyDoesNotRaiseChangeEvent()
        {
            var dropdown = new CustomDropdown
            {
                Options = new List<string> { "One", "Two" }
            };
            var changeCount = 0;

            window.rootVisualElement.Add(dropdown);
            dropdown.RegisterValueChangedCallback(_ => changeCount++);

            dropdown.SetValueWithoutNotify("Two");

            Assert.That(changeCount, Is.Zero);
            Assert.That(dropdown.SelectedIndex, Is.EqualTo(1));
            Assert.That(dropdown.value, Is.EqualTo("Two"));
        }

        [Test]
        public void OpenStateControlsContainerVisibility()
        {
            var dropdown = new CustomDropdown();

            Assert.That(dropdown.IsOpen, Is.False);
            Assert.That(dropdown.DropdownContainerWrapper.style.display.value, Is.EqualTo(DisplayStyle.None));

            dropdown.IsOpen = true;

            Assert.That(dropdown.IsOpen, Is.True);
            Assert.That(dropdown.ClassListContains(CustomDropdown.OPEN_CLASS_NAME), Is.True);
            Assert.That(dropdown.DropdownContainerWrapper.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            dropdown.IsOpen = false;

            Assert.That(dropdown.IsOpen, Is.False);
            Assert.That(dropdown.ClassListContains(CustomDropdown.OPEN_CLASS_NAME), Is.False);
            Assert.That(dropdown.DropdownContainerWrapper.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator MenuUsesSpaceBelowNearTop() => CheckOverlay(new Vector2(30, 20), 1, false);

        [UnityTest]
        public IEnumerator MenuUsesSpaceAboveNearBottomAndAfterReopen() =>
            CheckOverlay(new Vector2(30, 330), 1, true);

        [UnityTest]
        public IEnumerator MenuStaysInsideRightEdge() => CheckOverlay(new Vector2(460, 20), 1, false);

        [UnityTest]
        public IEnumerator TransformedAnchorUsesNativeViewportGeometry() =>
            CheckOverlay(new Vector2(20, 180), 2, true);

        private IEnumerator CheckOverlay(Vector2 position, float scale, bool above)
        {
            window.position = new Rect(100, 100, 500, 400);
            var host = new VisualElement();
            host.style.position = Position.Absolute;
            host.style.left = 0;
            host.style.top = 0;
            host.style.width = 500 / scale;
            host.style.height = 400 / scale;
            host.style.transformOrigin = new TransformOrigin(0, 0);
            host.style.scale = new Scale(new Vector3(scale, scale, 1));
            window.rootVisualElement.Add(host);
            var dropdown = new CustomDropdown { Options = new List<string> { "One", "Two", "Three" } };
            dropdown.style.position = Position.Absolute;
            dropdown.style.left = position.x;
            dropdown.style.top = position.y;
            dropdown.style.width = 30;
            dropdown.style.height = 15;
            dropdown.DropdownContainerWrapper.style.position = Position.Absolute;
            dropdown.DropdownContainerWrapper.style.bottom = 0;
            dropdown.DropdownContainer.style.position = Position.Absolute;
            dropdown.DropdownContainer.style.width = 140;
            dropdown.DropdownContainer.style.height = 160;
            host.Add(dropdown);
            yield return null;

            for (var opening = 0; opening < 2; opening++)
            {
                dropdown.IsOpen = true;
                for (var frame = 0; frame < 120; frame++)
                {
                    yield return null;
                    var bounds = dropdown.DropdownContainer.worldBound;
                    var viewport = dropdown.panel.visualTree.worldBound;
                    var anchor = dropdown.worldBound;
                    var edge = above ? bounds.yMax - anchor.yMin : bounds.yMin - anchor.yMax;
                    if (dropdown.DropdownContainerWrapper.parent != dropdown && bounds.height > 0 &&
                        bounds.xMin >= viewport.xMin - 1 && bounds.xMax <= viewport.xMax + 1 &&
                        bounds.yMin >= viewport.yMin - 1 && bounds.yMax <= viewport.yMax + 1 &&
                        Mathf.Abs(edge) <= 1)
                        break;
                }
                var menu = dropdown.DropdownContainer.worldBound;
                var panelBounds = dropdown.panel.visualTree.worldBound;
                Assert.That(menu.width, Is.EqualTo(140).Within(1));
                Assert.That(menu.height, Is.EqualTo(160).Within(1));
                Assert.That(menu.xMin, Is.GreaterThanOrEqualTo(panelBounds.xMin - 1));
                Assert.That(menu.xMax, Is.LessThanOrEqualTo(panelBounds.xMax + 1));
                Assert.That(menu.yMin, Is.GreaterThanOrEqualTo(panelBounds.yMin - 1));
                Assert.That(menu.yMax, Is.LessThanOrEqualTo(panelBounds.yMax + 1));
                if (above)
                    Assert.That(menu.yMax, Is.EqualTo(dropdown.worldBound.yMin).Within(1));
                else
                    Assert.That(menu.yMin, Is.EqualTo(dropdown.worldBound.yMax).Within(1));
                dropdown.IsOpen = false;
                Assert.That(dropdown.DropdownContainerWrapper.parent, Is.SameAs(dropdown));
                Assert.That(dropdown.DropdownContainer.style.top.keyword, Is.EqualTo(StyleKeyword.Null));
                yield return null;
            }
        }
    }
}
