using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace VM233.UIElements
{
    [UxmlElement]
    public partial class CustomDropdown : VisualElement, INotifyValueChanged<string>
    {
        public const string CLASS_NAME = "custom-dropdown";
        public const string OPEN_CLASS_NAME = "custom-dropdown--open";
        public const string TOGGLE_CLASS_NAME = "custom-dropdown__toggle";
        public const string DROPDOWN_OVERLAY_CLASS_NAME = "custom-dropdown__overlay";
        public const string DROPDOWN_CONTAINER_WRAPPER_CLASS_NAME = "custom-dropdown__container-wrapper";
        public const string DROPDOWN_CONTAINER_CLASS_NAME = "custom-dropdown__container";
        public const string ENTRY_CLASS_NAME = "custom-dropdown__entry";
        public const string SELECTED_ENTRY_CLASS_NAME = "custom-dropdown__entry--selected";
        public const string ENTRY_LABEL_CLASS_NAME = "custom-dropdown__entry-label";
        public const string GAP_CLASS_NAME = "custom-dropdown__gap";
        public const string OPTION_LABEL_NAME = "option-label";

        private readonly List<string> options = new();
        private readonly List<VisualElement> entries = new();
        private readonly VisualElement dropdownOverlay;

        private VisualTreeAsset entryTemplate;
        private VisualElement outsideClickRoot;
        private IVisualElementScheduledItem overlayPositionItem;
        private int selectedIndex = -1;
        private int requestedSelectedIndex;
        private bool isOpen;

        [UxmlAttribute]
        public List<string> Options
        {
            get => options;
            set => SetOptions(value);
        }

        [UxmlAttribute]
        public VisualTreeAsset EntryTemplate
        {
            get => entryTemplate;
            set
            {
                if (entryTemplate == value)
                {
                    return;
                }

                entryTemplate = value;
                RefreshEntries();
            }
        }

        [UxmlAttribute]
        public int SelectedIndex
        {
            get => selectedIndex;
            set => SetSelectedIndex(value, notify: true);
        }

        public string value
        {
            get => GetSelectedValue();
            set
            {
                var index = options.IndexOf(value);
                SetSelectedIndex(index, notify: true);
            }
        }

        public bool IsOpen
        {
            get => isOpen;
            set => SetOpen(value);
        }

        public Toggle Toggle { get; }
        public VisualElement DropdownContainerWrapper { get; }
        public VisualElement DropdownContainer { get; }
        public IReadOnlyList<VisualElement> Entries => entries;

        public event Action<int, string> SelectionChanged;
        public event Action<VisualElement, int, string> EntryCreated;

        public CustomDropdown()
        {
            AddToClassList(CLASS_NAME);

            Toggle = new Toggle
            {
                name = "Toggle"
            };
            Toggle.AddToClassList(TOGGLE_CLASS_NAME);
            hierarchy.Add(Toggle);

            DropdownContainerWrapper = new VisualElement
            {
                name = "Dropdown Container Wrapper"
            };
            DropdownContainerWrapper.AddToClassList(DROPDOWN_CONTAINER_WRAPPER_CLASS_NAME);
            hierarchy.Add(DropdownContainerWrapper);

            dropdownOverlay = new VisualElement
            {
                name = "Dropdown Overlay",
                pickingMode = PickingMode.Ignore
            };
            dropdownOverlay.AddToClassList(DROPDOWN_OVERLAY_CLASS_NAME);
            dropdownOverlay.style.position = Position.Absolute;
            dropdownOverlay.style.overflow = Overflow.Visible;

            DropdownContainer = new VisualElement
            {
                name = "Dropdown Container"
            };
            DropdownContainer.AddToClassList(DROPDOWN_CONTAINER_CLASS_NAME);
            DropdownContainerWrapper.Add(DropdownContainer);

            Toggle.RegisterValueChangedCallback(OnToggleValueChanged);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            SetOpen(false);
            RefreshSelectionDisplay();
        }

        public void SetOptions(IEnumerable<string> newOptions)
        {
            var copiedOptions = newOptions == null ? new List<string>() : new List<string>(newOptions);
            var previousValue = GetSelectedValue();

            options.Clear();
            options.AddRange(copiedOptions);

            RefreshEntries();

            var newIndex = previousValue == null ? -1 : options.IndexOf(previousValue);
            if (newIndex < 0)
            {
                newIndex = NormalizeIndex(requestedSelectedIndex);
            }

            requestedSelectedIndex = newIndex;
            ApplySelectedIndex(newIndex, notify: false);
        }

        public void SetSelectedIndexWithoutNotify(int newIndex)
        {
            requestedSelectedIndex = newIndex;
            ApplySelectedIndex(NormalizeIndex(newIndex), notify: false);
        }

        public void SetValueWithoutNotify(string newValue)
        {
            var index = options.IndexOf(newValue);
            SetSelectedIndexWithoutNotify(index);
        }

        public void RefreshEntries()
        {
            DropdownContainer.Clear();
            entries.Clear();

            for (var index = 0; index < options.Count; index++)
            {
                if (index > 0)
                {
                    var gap = new VisualElement
                    {
                        name = $"Gap {index - 1}"
                    };
                    gap.AddToClassList(GAP_CLASS_NAME);
                    DropdownContainer.Add(gap);
                }

                var currentIndex = index;
                var option = options[index];
                var entry = CreateEntry(currentIndex, option);

                entry.RegisterCallback<ClickEvent>(_ => SelectEntry(currentIndex));
                entry.RegisterCallback<NavigationSubmitEvent>(_ => SelectEntry(currentIndex));

                entries.Add(entry);
                DropdownContainer.Add(entry);
                EntryCreated?.Invoke(entry, currentIndex, option);
            }

            RefreshSelectionDisplay();
        }

        private VisualElement CreateEntry(int index, string option)
        {
            var entry = new VisualElement
            {
                name = $"Entry {index}",
                userData = option,
                focusable = true,
                tabIndex = 0
            };
            entry.AddToClassList(ENTRY_CLASS_NAME);

            if (entryTemplate == null)
            {
                var label = new Label(option)
                {
                    name = OPTION_LABEL_NAME
                };
                label.AddToClassList(ENTRY_LABEL_CLASS_NAME);
                entry.Add(label);
                return entry;
            }

            entryTemplate.CloneTree(entry);

            var optionLabel = entry.Q<Label>(OPTION_LABEL_NAME) ??
                              entry.Q<Label>(className: ENTRY_LABEL_CLASS_NAME) ??
                              entry.Q<Label>();
            if (optionLabel != null)
            {
                optionLabel.text = option;
                optionLabel.AddToClassList(ENTRY_LABEL_CLASS_NAME);
            }

            return entry;
        }

        private void SelectEntry(int index)
        {
            SetSelectedIndex(index, notify: true);
            SetOpen(false);
        }

        private void SetSelectedIndex(int newIndex, bool notify)
        {
            requestedSelectedIndex = newIndex;
            ApplySelectedIndex(NormalizeIndex(newIndex), notify);
        }

        private void ApplySelectedIndex(int newIndex, bool notify)
        {
            if (selectedIndex == newIndex)
            {
                RefreshSelectionDisplay();
                return;
            }

            var previousValue = GetSelectedValue();
            selectedIndex = newIndex;
            var newValue = GetSelectedValue();

            RefreshSelectionDisplay();

            if (notify == false)
            {
                return;
            }

            SelectionChanged?.Invoke(selectedIndex, newValue);

            if (EqualityComparer<string>.Default.Equals(previousValue, newValue))
            {
                return;
            }

            using var changeEvent = ChangeEvent<string>.GetPooled(previousValue, newValue);
            changeEvent.target = this;
            SendEvent(changeEvent);
        }

        private int NormalizeIndex(int index)
        {
            return index >= 0 && index < options.Count ? index : -1;
        }

        private string GetSelectedValue()
        {
            return selectedIndex >= 0 && selectedIndex < options.Count ? options[selectedIndex] : null;
        }

        private void RefreshSelectionDisplay()
        {
            Toggle.text = GetSelectedValue() ?? string.Empty;

            for (var index = 0; index < entries.Count; index++)
            {
                entries[index].EnableInClassList(SELECTED_ENTRY_CLASS_NAME, index == selectedIndex);
            }
        }

        private void OnToggleValueChanged(ChangeEvent<bool> evt)
        {
            SetOpen(evt.newValue);
        }

        private void SetOpen(bool open)
        {
            isOpen = open;
            Toggle.SetValueWithoutNotify(open);
            DropdownContainerWrapper.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList(OPEN_CLASS_NAME, open);

            if (open)
            {
                RegisterOutsideClick();
                DropdownContainerWrapper.schedule.Execute(AttachDropdownToOverlay);
            }
            else
            {
                RestoreDropdownContainerWrapper();
                UnregisterOutsideClick();
            }
        }

        private void AttachDropdownToOverlay()
        {
            if (isOpen == false || panel == null || dropdownOverlay.parent != null)
            {
                return;
            }

            var overlayHost = FindOverlayHost();
            if (overlayHost == null)
            {
                return;
            }

            SyncOverlayClasses();
            if (overlayHost == panel.visualTree)
            {
                SyncOverlayStyleSheets(overlayHost);
            }
            else
            {
                dropdownOverlay.styleSheets.Clear();
            }

            dropdownOverlay.style.position = Position.Absolute;
            dropdownOverlay.style.overflow = Overflow.Visible;
            overlayHost.Add(dropdownOverlay);
            DropdownContainerWrapper.RemoveFromHierarchy();
            dropdownOverlay.Add(DropdownContainerWrapper);

            UpdateOverlayPosition();
            overlayPositionItem?.Pause();
            overlayPositionItem = dropdownOverlay.schedule.Execute(UpdateOverlayPosition).Every(16);
        }

        private VisualElement FindOverlayHost()
        {
            var panelRoot = panel?.visualTree;
            if (panelRoot == null)
            {
                return null;
            }

            VisualElement documentRoot = this;
            var current = parent;
            while (current != null && current != panelRoot)
            {
                if (current is TemplateContainer)
                {
                    return documentRoot == this ? current : documentRoot;
                }

                documentRoot = current;
                current = current.parent;
            }

            return documentRoot == this ? panelRoot : documentRoot;
        }

        private void SyncOverlayClasses()
        {
            dropdownOverlay.ClearClassList();
            dropdownOverlay.AddToClassList(DROPDOWN_OVERLAY_CLASS_NAME);

            foreach (var className in GetClasses())
            {
                dropdownOverlay.AddToClassList(className);
            }
        }

        private void SyncOverlayStyleSheets(VisualElement overlayHost)
        {
            dropdownOverlay.styleSheets.Clear();

            var styleAncestors = new Stack<VisualElement>();
            VisualElement current = this;
            while (current != null && current != overlayHost)
            {
                styleAncestors.Push(current);
                current = current.parent;
            }

            while (styleAncestors.Count > 0)
            {
                var styleAncestor = styleAncestors.Pop();
                for (var index = 0; index < styleAncestor.styleSheets.count; index++)
                {
                    var styleSheet = styleAncestor.styleSheets[index];
                    if (dropdownOverlay.styleSheets.Contains(styleSheet) == false)
                    {
                        dropdownOverlay.styleSheets.Add(styleSheet);
                    }
                }
            }
        }

        private void UpdateOverlayPosition()
        {
            var overlayHost = dropdownOverlay.parent;
            if (isOpen == false || overlayHost == null || panel == null)
            {
                return;
            }

            var topLeft = overlayHost.WorldToLocal(worldBound.min);
            var bottomRight = overlayHost.WorldToLocal(worldBound.max);

            dropdownOverlay.style.position = Position.Absolute;
            dropdownOverlay.style.left = topLeft.x;
            dropdownOverlay.style.top = topLeft.y;
            dropdownOverlay.style.right = StyleKeyword.Auto;
            dropdownOverlay.style.bottom = StyleKeyword.Auto;
            dropdownOverlay.style.width = bottomRight.x - topLeft.x;
            dropdownOverlay.style.height = bottomRight.y - topLeft.y;
        }

        private void RestoreDropdownContainerWrapper()
        {
            overlayPositionItem?.Pause();
            overlayPositionItem = null;

            if (DropdownContainerWrapper.parent != this)
            {
                DropdownContainerWrapper.RemoveFromHierarchy();
                hierarchy.Add(DropdownContainerWrapper);
            }

            dropdownOverlay.RemoveFromHierarchy();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (isOpen)
            {
                RegisterOutsideClick();
            }
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            UnregisterOutsideClick();
            SetOpen(false);
        }

        private void RegisterOutsideClick()
        {
            var root = panel?.visualTree;
            if (root == outsideClickRoot)
            {
                return;
            }

            UnregisterOutsideClick();
            outsideClickRoot = root;
            outsideClickRoot?.RegisterCallback<PointerDownEvent>(OnPanelPointerDown, TrickleDown.TrickleDown);
        }

        private void UnregisterOutsideClick()
        {
            outsideClickRoot?.UnregisterCallback<PointerDownEvent>(OnPanelPointerDown, TrickleDown.TrickleDown);
            outsideClickRoot = null;
        }

        private void OnPanelPointerDown(PointerDownEvent evt)
        {
            if (evt.target is VisualElement target &&
                (Contains(target) || DropdownContainerWrapper.Contains(target)))
            {
                return;
            }

            SetOpen(false);
        }
    }
}
