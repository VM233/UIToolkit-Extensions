# Control API and styling

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

## RotatingVisualElement

`RotatingVisualElement` continuously rotates itself at a configurable angular velocity while it is
attached to a panel. The `degrees-per-second` UXML attribute uses unscaled real time: positive values
rotate clockwise and negative values rotate counterclockwise. The control owns its `rotate` style;
consumers provide its size, background, and other visual styling.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <VM233.UIElements.RotatingVisualElement
        class="rotating-decoration"
        degrees-per-second="15"/>
</ui:UXML>
```

```csharp
using VM233.UIElements;

var decoration = new RotatingVisualElement
{
    DegreesPerSecond = 15f
};
```

## Dropdown placement and skins

The overlay copies the control's classes when it detaches from the page. Scope skins to a class on the dropdown itself, so descendant selectors still match the detached menu. Ancestor page IDs cannot style the overlay. The control positions the menu using native panel coordinates, preferring below when it fits and above when more room is available. It constrains the menu to the viewport when its natural size fits; a menu taller than the viewport stays aligned to its top and requires a consumer-provided scrolling entry presentation. Closing restores authored offsets.
