using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace VM233.UIElements
{
    public sealed class UiVisibilityTransition : IDisposable
    {
        private readonly VisualElement element;
        private readonly Func<float> seconds, distance;
        private readonly Func<double> clock;
        private readonly Action<UiTransitionFrame> observed;
        private readonly Action<VisualElement> release;
        private readonly IVisualElementScheduledItem commit, animation;
        private UiTransitionSite requestedSite, contentSite, activeSite;
        private DisplayStyle rendered;
        private UiTransitionSegment segment;
        private float opacity;
        private bool contentInitialized, ownsStyles, retired;
        private object publishedIdentity, requestedIdentity;
        private Action pendingContent;
        private int intermediateFrames, lastIntermediateFrame = -1;
        private StyleFloat originalOpacity;
        private StyleTranslate originalTranslate;
        private float authoredOpacity;
        private Translate authoredTranslate;
        private StyleEnum<Position> originalPosition;
        private StyleLength originalLeft, originalTop, originalRight, originalBottom, originalWidth, originalHeight;
        public DisplayStyle Requested { get; private set; }

        internal UiVisibilityTransition(VisualElement element, Func<float> seconds, Func<float> distance, Func<double> clock,
            Action<UiTransitionFrame> observed, Action<VisualElement> release)
        {
            this.element = element; this.seconds = seconds; this.distance = distance; this.observed = observed;
            this.release = release; this.clock = clock;
            rendered = Requested = element.style.display.keyword == StyleKeyword.Undefined
                ? element.style.display.value : element.resolvedStyle.display;
            opacity = rendered == DisplayStyle.None ? 0 : 1;
            commit = element.schedule.Execute(Commit); commit.Pause();
            animation = element.schedule.Execute(Advance).Every(16); animation.Pause();
            RegisterInput(true); element.RegisterCallback<DetachFromPanelEvent>(Detached);
        }

        internal void Request(DisplayStyle display, UiTransitionSite source, bool animate)
        {
            requestedSite = source; Requested = display;
            if (segment == null && pendingContent == null) activeSite = source;
            if (!animate)
            {
                commit.Pause(); Settle(display); activeSite = source; observed(Observe());
            }
            else commit.ExecuteLater(0);
        }

        internal void Replace(object identity, Action publish, UiTransitionSite source, bool animate)
        {
            requestedSite = contentSite = source; requestedIdentity = identity;
            if (segment == null && pendingContent == null) activeSite = source;
            if (!contentInitialized || !animate)
            {
                contentInitialized = true; publishedIdentity = identity; pendingContent = null;
                publish(); return;
            }
            if (Equals(publishedIdentity, identity)) { pendingContent = null; publish(); }
            else pendingContent = publish;
            commit.ExecuteLater(0);
        }

        private void Commit()
        {
            SampleSegment(); Reconcile(); observed(Observe());
        }

        private void Advance()
        {
            SampleSegment(); Reconcile();
            if (segment != null && opacity > 0 && opacity < 1 && lastIntermediateFrame != Time.frameCount)
            { intermediateFrames++; lastIntermediateFrame = Time.frameCount; }
            observed(Observe());
        }

        private void SampleSegment()
        {
            var current = segment;
            if (current == null) return;
            double now = clock(); opacity = current.Sample(now); Apply(current.Destination);
            if (!current.Complete(now)) return;
            segment = null; animation.Pause();
            if (current.Phase == UiTransitionPhase.Exiting) Settle(DisplayStyle.None);
            else if (current.Phase is UiTransitionPhase.Entering or UiTransitionPhase.ReplacingIncoming) Settle(DisplayStyle.Flex);
            // ReplacingOutgoing retains zero-opacity content until Reconcile consumes the latest request.
        }

        private void Reconcile()
        {
            if (Requested == DisplayStyle.None)
            {
                if (segment?.Phase == UiTransitionPhase.Exiting) return;
                if (opacity == 0 || rendered == DisplayStyle.None) { Settle(DisplayStyle.None); return; }
                Begin(UiTransitionPhase.Exiting, 0, seconds(), requestedSite); return;
            }
            bool published = false;
            var publicationSite = contentSite;
            if (pendingContent != null)
            {
                if (segment?.Phase == UiTransitionPhase.ReplacingOutgoing) return;
                if (opacity > 0 && rendered != DisplayStyle.None)
                { Begin(UiTransitionPhase.ReplacingOutgoing, 0, seconds() * .5f, contentSite); return; }
                var publish = pendingContent; pendingContent = null;
                publishedIdentity = requestedIdentity; published = true;
                publish();
            }
            if (segment?.Phase is UiTransitionPhase.Entering or UiTransitionPhase.ReplacingIncoming) return;
            if (opacity == 1 && rendered != DisplayStyle.None) { Settle(DisplayStyle.Flex); return; }
            Begin(published ? UiTransitionPhase.ReplacingIncoming : UiTransitionPhase.Entering, 1,
                seconds() * (published ? .5f : 1), published ? publicationSite : requestedSite);
        }

        private void Begin(UiTransitionPhase phase, float destination, float duration, UiTransitionSite source)
        {
            if (!float.IsFinite(duration) || duration <= 0 || !float.IsFinite(distance()) || distance() < 0)
                throw new InvalidOperationException("UI region motion requires a finite positive duration and nonnegative distance.");
            AcquireStyles(); activeSite = source;
            segment = new UiTransitionSegment(phase, opacity, destination, clock(), duration);
            intermediateFrames = 0; lastIntermediateFrame = -1;
            SettleInput();
            if (phase == UiTransitionPhase.Exiting) FreezeOutgoingLayout();
            else { RestoreLayout(); rendered = DisplayStyle.Flex; element.style.display = DisplayStyle.Flex; }
            Apply(destination); animation.Resume();
        }

        private void Settle(DisplayStyle display)
        {
            segment = null; animation.Pause(); rendered = display;
            opacity = display == DisplayStyle.None ? 0 : 1;
            element.style.display = display; RestoreStyles();
        }

        private void AcquireStyles()
        {
            if (ownsStyles) return;
            ownsStyles = true;
            originalOpacity = element.style.opacity; originalTranslate = element.style.translate;
            authoredOpacity = element.resolvedStyle.opacity; authoredTranslate = element.resolvedStyle.translate;
            originalPosition = element.style.position; originalLeft = element.style.left; originalTop = element.style.top;
            originalRight = element.style.right; originalBottom = element.style.bottom;
            originalWidth = element.style.width; originalHeight = element.style.height;
        }

        private void FreezeOutgoingLayout()
        {
            var bounds = element.layout;
            element.style.position = Position.Absolute;
            element.style.left = bounds.x - element.resolvedStyle.marginLeft;
            element.style.top = bounds.y - element.resolvedStyle.marginTop;
            element.style.right = StyleKeyword.Auto; element.style.bottom = StyleKeyword.Auto;
            element.style.width = bounds.width; element.style.height = bounds.height;
        }

        private void Apply(float destination)
        {
            element.style.opacity = authoredOpacity * opacity;
            float offset = distance() * (1 - opacity) * (destination == 0 ? -1 : 1);
            float baseY = authoredTranslate.y.unit == LengthUnit.Percent
                ? authoredTranslate.y.value * element.layout.height / 100 : authoredTranslate.y.value;
            element.style.translate = new Translate(authoredTranslate.x, baseY + offset, authoredTranslate.z);
        }

        private void RestoreLayout()
        {
            element.style.position = originalPosition; element.style.left = originalLeft; element.style.top = originalTop;
            element.style.right = originalRight; element.style.bottom = originalBottom;
            element.style.width = originalWidth; element.style.height = originalHeight;
        }

        private void RestoreStyles()
        {
            if (!ownsStyles) return;
            RestoreLayout(); element.style.opacity = originalOpacity; element.style.translate = originalTranslate;
            ownsStyles = false;
        }

        private void SettleInput()
        {
            var panel = element.panel;
            for (int pointer = 0; pointer < PointerId.maxPointers; pointer++)
                if (panel.GetCapturingElement(pointer) is VisualElement captured &&
                    (ReferenceEquals(captured, element) || element.Contains(captured))) captured.ReleasePointer(pointer);
            if (element.focusController.focusedElement is VisualElement focused &&
                (ReferenceEquals(focused, element) || element.Contains(focused))) focused.Blur();
        }

        private void Block<T>(T evt) where T : EventBase<T>, new()
        { if (segment != null || pendingContent != null || Requested == DisplayStyle.None) evt.StopImmediatePropagation(); }

        private void RegisterInput(bool register)
        {
            Bind<PointerDownEvent>(register); Bind<PointerUpEvent>(register); Bind<PointerMoveEvent>(register);
            Bind<ClickEvent>(register); Bind<WheelEvent>(register); Bind<KeyDownEvent>(register); Bind<KeyUpEvent>(register);
            Bind<NavigationSubmitEvent>(register); Bind<NavigationMoveEvent>(register); Bind<NavigationCancelEvent>(register);
        }

        private void Bind<T>(bool register) where T : EventBase<T>, new()
        {
            if (register) element.RegisterCallback<T>(Block, TrickleDown.TrickleDown);
            else element.UnregisterCallback<T>(Block, TrickleDown.TrickleDown);
        }

        public UiTransitionFrame Observe() => new(activeSite, element, Requested != DisplayStyle.None,
            segment?.Phase ?? (retired ? UiTransitionPhase.Retired : rendered == DisplayStyle.None ? UiTransitionPhase.Hidden : UiTransitionPhase.Shown),
            opacity, intermediateFrames, clock());

        private void Detached(DetachFromPanelEvent evt)
        {
            if (ReferenceEquals(evt.target, element)) release(element);
        }

        public void Dispose()
        {
            commit.Pause(); pendingContent = null; Settle(Requested); retired = true;
            RegisterInput(false); element.UnregisterCallback<DetachFromPanelEvent>(Detached);
            observed(Observe());
        }
    }
}
