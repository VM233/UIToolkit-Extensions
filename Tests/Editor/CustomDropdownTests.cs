using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace VM233.UIElements.Editor.Tests
{
    public class CustomDropdownTests
    {
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
            string selectionValue = null;
            string previousValue = null;
            string newValue = null;

            dropdown.SelectionChanged += (index, value) =>
            {
                selectionIndex = index;
                selectionValue = value;
            };
            dropdown.RegisterValueChangedCallback(evt =>
            {
                previousValue = evt.previousValue;
                newValue = evt.newValue;
            });

            dropdown.SelectedIndex = 1;

            Assert.That(selectionIndex, Is.EqualTo(1));
            Assert.That(selectionValue, Is.EqualTo("Two"));
            Assert.That(previousValue, Is.Null);
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
    }
}
