# Retained UI transitions

`UiTransitionScope` owns requested visibility, actual outgoing content, animation,
input settlement and retirement for one real retained root. An `IUiTransitionHost`
exposes that scope to `UiPresentation.Display` and `UiPresentation.Replace`.
Supply the owner's presentation clock and timing/distance configuration. Publish
the scope only after the actual document's first visible layout publication.

Display requests coalesce within the producer's synchronous update. A retiring
region retains its current pixels in an absolute copy of its original layout
until opacity reaches zero; no duplicate element/document or image is created.
The caller reads `RequestedDisplay`, since the outgoing element can still be
rendered. Existing pointer capture/focus is released in that region; pointer,
keyboard and navigation input cannot activate old content while it retires.
Repeated requests do not restart an unchanged transition. Reversals continue
from the current displayed opacity and restore authored style/flow at completion.

Replace accepts the actual content identity and the publication callback. It
retains old content through a half-duration exit, publishes the latest requested
identity, and presents the new content. While a publication is pending, repeated
requests replace its callback; returning to the published identity cancels the
unpublished change. Keep all of that region's content and input identity in the
callback. Independent live values need not become content identities.

The root owner calls Retire at native root release. Actual child detach also
retires the exact child's work and drops pending callbacks. Observe/Observed
publish source coordinates, actual element, frame, clock and presentation phase;
registration and measured intermediate values alone are not pixel acceptance.

Static cost: at most4096 regions per scope, one commit and animation schedule per
region; one native pointer-bound32 input settlement and one ancestry chain for
focus per admitted change. No asset scans, textures, documents or per-frame
managed arrays. Observe explicitly allocates one bounded snapshot. Root publication
and source/host inventory belong to the integrating framework/project.

Native fixtures cover hide/show, coalescing, reversal, latest content publication,
content reentry, detach/reuse and exact authored opacity/transform/layout. A game
must separately exercise its actual producers, inputs, languages, viewports and
intermediate pixels.
