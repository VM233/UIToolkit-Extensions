# Retained UI transitions

`UiTransitionScope` owns one real document root. An `IUiTransitionHost` exposes
it to `UiPresentation.Display` and `Replace`. Supply the presentation clock,
duration, distance and an `IUiTransitionRenderer`; call Publish after the actual
first layout, and Retire at root release. Caller coordinates plus the explicit
target expression identify each source site.

Display requests coalesce within the producer's synchronous update. A retiring
region retains its current pixels in an absolute copy of its original layout
until opacity reaches zero; no duplicate element/document or image is created.
The caller reads `RequestedDisplay`, since the outgoing element can still be
rendered. Existing pointer capture/focus is released in that region; pointer,
keyboard and navigation input cannot activate old content while it retires.
Repeated requests do not restart an unchanged transition. Reversals continue
from the current displayed opacity and restore authored style/flow at completion.

## Coherent content publication

Replace accepts the identity of the complete visible state and its publication
callback. Initial or unpresented content publishes immediately. A later identity
change retains the live source until the renderer captures its actual pixels.
The scope then publishes the latest callback under the opaque source image,
waits for a real rendered target layout, and crossfades that image over the live
target for one complete configured duration. There is no empty outgoing interval.
Keep related titles, content and actions inside the same region and callback.
Same-identity live values can update immediately. Returning to the published
identity before capture cancels the unpublished replacement.

One document batches pending regions into one image. CaptureBounds is evaluated
by the renderer at actual capture time, selects outermost pending regions and
unions their old rectangles. A parent's callback must publish its complete
subtree; pending descendant callbacks are subsumed by that owner. Requests after
publication wait for a new capture after the active image retires. Publication
actions are latched before observers run, so new requests cannot overwrite the
admitted action.

The renderer owns the platform frame-end/screenshot details. CaptureAsync must
return an attached `IUiTransitionImage` with genuine source pixels and its frame
identity. WaitForRenderedFrameAsync completes after the new live layout actually
renders. Both methods must synchronously complete their owned main-thread waits
when their cancellation token is canceled on that thread, carrying that token.
This lets native synchronous root release cancel and join those exact waits
before returning. The integrating renderer must prove this contract on its
supported engine. Failure is exposed; there is no live-content or timing fallback.
Disposing an image retires its visual and GPU resource. Retirement attempts all
owned resources and regions even if an observer fails, then reports all errors.

This rendering model follows the separation of captured old pixels and live new
content in [W3C View Transitions](https://www.w3.org/TR/css-view-transitions-1/#rendering-model).
The previous sequential half-duration content fade was rejected by natural game
frames: a frame interval could skip a half segment and expose incoherent/blank
content. Increasing durations is not its replacement.

`UiTransitionDriver` records an existing specialized owner's phase and retirement
members for source inventory. It does not certify intermediate frames or allow
an ordinary state change to bypass runtime review.

The root owner calls Retire at native root release. Actual child detach also
retires the exact child's work and drops pending callbacks. Observe/Observed
publish source coordinates, actual element, frame, clock, phase, target weight,
source-pixel retention and capture frame. HasRetainedImage exposes the actual
owned image. PendingFrameWait identifies the exact incomplete renderer Task,
separately from pending intent, for native cancellation tests. Registration and
intermediate values alone are not pixel acceptance.

Static bounds:4096 regions per scope, one commit/animation schedule per region,
one content schedule and one retained image per batch. Source selection performs
at most4096 ×64 ancestor lookups and4096 rectangle unions. Capture/publication/
retirement arrays and sets each hold at most4096 regions. Input settlement is
bounded by Unity's pointer domain. Observe allocates one bounded snapshot.
The integrating renderer separately owns screen dimensions, GPU memory and
capture costs; no CPU image readback is required by this API. Root publication
and source/host inventory belong to the integrating framework/project.

Native Editor fixtures verify visibility/reversal and the resource/publication
protocol using a controlled renderer. They do not claim screenshot quality.
The game must prove actual frame-end capture/cancellation, coherent intermediate
pixels, input, root replacement, languages and viewports through its production
controls.
