# UI Toolkit Extensions

Reusable UI Toolkit controls and control extensions shared across VM233 Unity projects.

## Installation

Add the Git repository to a Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.vm233.ui-toolkit-extensions": "https://github.com/VM233/UIToolkit-Extensions.git#<commit-or-tag>"
  }
}
```

Pin production projects to a full commit SHA.

## CustomDropdown

`CustomDropdown` is a UXML-enabled dropdown control with:

- string options and selected-index/value APIs;
- `INotifyValueChanged<string>` support;
- keyboard-submit and pointer selection;
- custom entry templates;
- outside-click closing;
- an overlay host that prevents the menu from being clipped by neighboring UI;
- stable USS class names for project-specific skins.

Add the control in UXML, then style the exposed classes in the consuming project:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <VM233.UIElements.CustomDropdown
        name="ExampleDropdown"
        options="One,Two,Three"
        selected-index="0"/>
</ui:UXML>
```

C# usage:

```csharp
using VM233.UIElements;

var dropdown = root.Q<CustomDropdown>("ExampleDropdown");
dropdown.SelectionChanged += (index, value) =>
{
    // Apply the selected value.
};
```

## BoolStateVisualElement

`BoolStateVisualElement` owns a programmatic boolean state without accepting pointer or keyboard
input. Set `value` or call `SetValueWithoutNotify`; USS can consume the state through `:checked`.
The control intentionally has no interactive `:active` contract.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <VM233.UIElements.BoolStateVisualElement
        name="ExampleState"
        value="true"
        class="example-state"/>
</ui:UXML>
```

```csharp
using VM233.UIElements;

var state = root.Q<BoolStateVisualElement>("ExampleState");
state.SetValueWithoutNotify(true);
```

## Package structure

- `Runtime/VisualElements`: reusable controls and VisualElement extensions.
- `Tests/Editor`: package regression tests.

USS, sprites, colors, localization, and page layout remain in each consuming project for now.
