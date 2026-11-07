# Changelog

All notable changes to this package are documented in this file.

## [0.5.14] - 2026-11-07

- Redirect active content from its frozen visible composite. Cancel its exact
  previous frame wait and retain the image until a successor capture is attached.
- Preserve captured bounds and already-published siblings, coalesce repeated
  targets and published-identity reselection, and latch actions before reentry.
  Keep nested content and same-region display projection ownership distinct.
- Cover interruption, geometry, ancestor/sibling, reentry and retirement in the
  controlled-renderer fixture. Actual pixels remain the integrating game's gate.

## [0.5.13] - 2026-11-06

- Add owned hidden-child attachment for lazy pools in presented documents.
  Require an unparented, unattached element with explicit inline display:none;
  preserve ordinary entrance/input ownership and unpublished-only initialization.
- Cover native layout/picking, later entrance, foreign-parent and existing-child
  rejection, unchanged initialization checks and resource retirement.

## [0.5.12] - 2026-11-05

- Add layout-preserving Visibility intent to the existing document region owner.
  Compose it with Display in one reversible segment and one input lifetime.
- Preserve inherited reveal semantics and synchronous projection coalescing.
- Cover retained geometry, outgoing input, mixed intents, reversal and retirement
  with actual native elements.

## [0.5.11] - 2026-11-05

- Treat the scope's exact root as owned alongside its strict descendants.
  Unity Contains excludes the receiver itself. Keep presented initialization
  and foreign-root rejection in the hierarchy mutation owner.
- Add the native actual-root binding/retirement counterexample missed by the
  earlier child-only initialization control.

## [0.5.10] - 2026-11-05

- Add synchronous Initialize for attached-root binding before publication and
  after real retirement. It uses the unchanged unpublished mutation condition
  and rejects an already presented document before running the callback.
- Cover initialization, published rejection, retired rebind and foreign-root
  rejection in the native scope suite.

## [0.5.9] - 2026-11-05

- Compare native authored padding with its initial resolved values in layout
  retirement tests. Unity's pixel rounding can differ from the source USS value;
  position, dimensions and the original equality tolerance remain asserted.

## [0.5.8] - 2026-11-05

- Correct the authored-layout UXML fixture's malformed GUID so Unity imports the
  actual tree before native checks. The visibility implementation is unchanged.

## [0.5.7] - 2026-11-05

- Preserve authored geometry throughout visibility exit, reversal and scope
  reuse. Remove the exit position/size override and IStyle getter restoration,
  which cannot represent UXML inline rules and displaced a real reused menu.
- Add native UXML authoring and in-flow exit controls, including repeated
  visibility and actual scope retirement/republication.

## [0.5.6] - 2026-11-05

- Add scope-owned native focus intent with an explicit transition settlement
  barrier. Enter/replacement completion and exit restoration consume the latest
  intent; root retirement and a hidden reversal cannot leave a late focus.
- Cover actual Unity focus during enter, exit, publication and retirement in
  three native controls without another schedule or deadline.

## [0.5.5] - 2026-11-05

- Suppress nested animation only inside captured publication groups. Independent
  siblings requested by a publication callback wait for their own source image.
- Stop publication/notification work when callbacks consume that actual batch's
  lifetime through document retirement or source-region removal.
- Consume batch resources and scope region ownership before cleanup callbacks.
  Reentrant completion retirement cannot dispose the old image twice.
- Cover sibling capture, publication/completion retirement and source removal in
  four native controlled-renderer lifetime controls.

## [0.5.4] - 2026-11-05

- Publish immutable content/visibility phase arcs from the runtime protocol that
  advances content and validates visibility changes. Source graph consumers can
  use current capture/publication/render/crossfade phases instead of stale copies.
- Add explicit Clear/Remove operations for initialization or actual retirement.
  A presented document rejects both, preserving live content for coherent Replace.
- Add a native control proving live mutation rejection and post-retirement release.

## [0.5.3] - 2026-11-05

- Publish FrameWaitStarted immediately after assigning the actual renderer Task.
  Observers can qualify native cancellation before the frame-end wait completes,
  rather than attempting to sample that short lifetime at window-capture cadence.
- Preserve synchronous source/target wait-admission retirement in two controls.
  No presentation clock, duration, capture cadence or rendering path changes.

## [0.5.2] - 2026-11-05

- Expose the exact incomplete source-capture or target-render Task as a computed
  PendingFrameWait phase for native cancellation qualification. This does not
  change timing, publication, animation or retirement behavior.

## [0.5.1] - 2026-11-05

- Use the supported TrySetCanceled(token) API in controlled Editor fixtures,
  retaining exact cancellation identity and rejecting duplicate completion.

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
