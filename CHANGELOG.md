# Changelog

All notable changes to this package are documented in this file.

## [0.3.0] - 2026-08-15

### Added

- Added `RotatingVisualElement`, a UXML-enabled visual element that rotates at a configurable number
  of degrees per second using unscaled real time while attached to a panel.

## [0.2.0] - 2026-08-12

### Added

- Added `BoolStateVisualElement`, a non-interactive native boolean field whose `value` drives the
  `:checked` pseudo-state without exposing pointer, keyboard, or `:active` interaction.

## [0.1.0] - 2026-07-15

### Added

- Initial UPM package structure for reusable UI Toolkit control extensions.
- `CustomDropdown` with UXML attributes, value-change notifications, custom entries, outside-click closing, and unclipped overlay presentation.
- Editor regression tests for options, selection, value events, and open state.
