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

## Requirements and usage

Unity 6000.0 or newer. Project-specific USS, sprites, localization and page layout remain in the consumer.

See [control API and styling](Documentation~/Controls.md) and [changes](CHANGELOG.md).

## Package structure

- `Runtime/VisualElements`: reusable controls and VisualElement extensions.
- `Tests/Editor`: package regression tests.

USS, sprites, colors, localization, and page layout remain in each consuming project for now.
