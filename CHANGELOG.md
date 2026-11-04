# Changelog

All notable changes to this package are documented in this file.

## [0.5.0] - 2026-11-05

### Changed

- Replace sequential half-duration content fades with one root-owned retained
  source image, coherent publication, actual target frame readiness and one full
  crossfade. Natural game frames rejected the former method's blank interval.
- UiTransitionScope now requires an IUiTransitionRenderer. Replace hosts must be
  descendants of the document host. Integrations must implement actual capture,
  rendered-frame readiness and synchronous cancellation/join at native root release.
- Expose capture/publication/layout/replacement phases, source-pixel/frame evidence
  and actual retained-image ownership. Remove the former outgoing/incoming phases.
- Preserve reentrant publication requests and complete all resource/region
  retirement before reporting observer failures. Protocol fixtures use an explicit
  controlled renderer; game pixel acceptance remains separate.

## [0.4.2] - 2026-11-05

### Fixed

- Qualify native visibility/reversal stimuli by the actual moving phase and
  wait for the terminal phase before checking exact authored style restoration.
  The previous natural run completed six of eight cases; one fixed-wait assertion
  sampled a still-moving frame and one arbitrary opacity window never sent its
  reversal stimulus. Production duration, terminal values and input checks stay unchanged.

## [0.4.1] - 2026-11-05

### Fixed

- Sample and finish each immutable animation segment before reconciling newer
  requests. Content reentry and publication at a completion boundary cannot
  overwrite an incoming phase with a stale target.
- Source sites include the exact target expression to distinguish multiple
  state producers on one line. Native motion owners can declare their phase
  and retirement member for source inventory; runtime evidence remains required.
- Natural phase observations replace external short-wait assumptions in native
  regression stimuli. Original terminal-state checks remain, with a new boundary case.

## [0.4.0] - 2026-11-05

### Added

- Root-owned retained UI visibility and content transitions with source sites,
  coalescing, reversal, input settlement, authored style restoration and native
  detach retirement. See Documentation~/transitions.md for the integration contract.
- Native regressions for visibility, content replacement, reentry and reuse.

## [0.3.1] - 2026-10-31

### Fixed

- Dropdown overlays choose above or below from measured menu and panel bounds, and constrain
  horizontal placement to the viewport. Large UI scales no longer hide the final options below
  the screen. Menus taller than the entire viewport retain their natural size, aligned to its top.
- Restore authored menu offsets on close; update placement after native menu geometry changes.
- Added native panel regressions for top, bottom, right-edge, scaled and reopen placement.

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
