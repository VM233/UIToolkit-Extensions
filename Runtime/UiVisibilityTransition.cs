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
        private readonly Action<UiVisibilityTransition> contentRequested, contentRemoved;
        private readonly IVisualElementScheduledItem commit, animation;
        private UiTransitionSite requestedSite, contentSite, activeSite;
        private DisplayStyle rendered;
        private StyleEnum<Visibility> requestedVisibility, renderedVisibility, segmentVisibility;
        private DisplayStyle segmentDisplay;
        private UiVisibilitySegment segment;
        private float opacity, translation;
        private bool contentInitialized, ownsStyles, retired;
        private object publishedIdentity, requestedIdentity;
        private Action pendingContent;
        private UiTransitionPhase? contentPhase;
        private float contentProgress;
        private bool contentPixels;
        private int contentCaptureFrame = -1, contentIntermediateFrames, contentLastFrame = -1;
        private int intermediateFrames, lastIntermediateFrame = -1;
        private StyleFloat originalOpacity;
        private StyleTranslate originalTranslate;
        private float authoredOpacity;
        private Translate authoredTranslate;
        public DisplayStyle Requested { get; private set; }
        internal bool RequestedVisible => Requested != DisplayStyle.None && Visible(requestedVisibility);
        private bool RenderedVisible => rendered != DisplayStyle.None && Visible(renderedVisibility);
        private static bool Visible(StyleEnum<Visibility> value) => value.keyword != StyleKeyword.Undefined || value.value == Visibility.Visible;
        internal VisualElement Element => element;
        internal bool HasPendingContent => pendingContent != null;
        internal bool InputSettled => segment == null && pendingContent == null && contentPhase == null &&
            Requested == rendered && requestedVisibility.Equals(renderedVisibility);

        internal UiVisibilityTransition(VisualElement element, Func<float> seconds, Func<float> distance, Func<double> clock,
            Action<UiTransitionFrame> observed, Action<VisualElement> release,
            Action<UiVisibilityTransition> contentRequested, Action<UiVisibilityTransition> contentRemoved)
        {
            this.element = element; this.seconds = seconds; this.distance = distance; this.observed = observed;
            this.release = release; this.clock = clock;
            this.contentRequested = contentRequested; this.contentRemoved = contentRemoved;
            rendered = Requested = element.style.display.keyword == StyleKeyword.Undefined
                ? element.style.display.value : element.resolvedStyle.display;
            requestedVisibility = renderedVisibility = element.style.visibility;
            opacity = RenderedVisible ? 1 : 0;
            commit = element.schedule.Execute(Commit); commit.Pause();
            animation = element.schedule.Execute(Advance).Every(16); animation.Pause();
            RegisterInput(true); element.RegisterCallback<DetachFromPanelEvent>(Detached);
        }

        internal void Request(DisplayStyle display, UiTransitionSite source, bool animate)
        {
            Requested = display;
            Request(source, animate);
        }

        internal void RequestVisibility(StyleEnum<Visibility> visibility, UiTransitionSite source, bool animate)
        {
            requestedVisibility = visibility;
            Request(source, animate);
        }

        private void Request(UiTransitionSite source, bool animate)
        {
            requestedSite = source;
            if (!RequestedVisible) { pendingContent = null; contentRequested(this); }
            if (segment == null && pendingContent == null && contentPhase == null) activeSite = source;
            if (!animate)
            {
                commit.Pause(); Settle(Requested, requestedVisibility);
                if (contentPhase == null) activeSite = source;
                observed(Observe());
            }
            else commit.ExecuteLater(0);
        }

        internal void Replace(object identity, Action publish, UiTransitionSite source, bool animate)
        {
            requestedSite = contentSite = source; requestedIdentity = identity;
            if (segment == null && pendingContent == null && contentPhase == null) activeSite = source;
            if (!contentInitialized || !animate)
            {
                contentInitialized = true; publishedIdentity = identity; pendingContent = null;
                contentRequested(this); publish(); return;
            }
            if (Equals(publishedIdentity, identity)) { pendingContent = null; publish(); }
            else
            {
                bool begins = pendingContent == null && contentPhase == null;
                pendingContent = publish;
                if (contentPhase == null) contentProgress = 0;
                if (begins) SettleInput();
            }
            contentRequested(this);
        }

        internal void CancelPendingContent() => pendingContent = null;

        internal Action TakeContentPublication()
        {
            var publish = pendingContent; pendingContent = null;
            publishedIdentity = requestedIdentity; activeSite = contentSite;
            if (publish == null) throw new InvalidOperationException("The captured region has no admitted content publication.");
            return publish;
        }

        internal void SetContentPhase(UiTransitionPhase phase, float progress, bool pixels, int capturedFrame)
        {
            if (phase == UiTransitionPhase.PublishingReplacement) { contentIntermediateFrames = 0; contentLastFrame = -1; }
            contentPhase = phase; contentProgress = progress; contentPixels = pixels; contentCaptureFrame = capturedFrame;
            if (phase == UiTransitionPhase.Replacing && progress is > 0 and < 1 && contentLastFrame != Time.frameCount)
            { contentLastFrame = Time.frameCount; contentIntermediateFrames++; }
            observed(Observe());
        }

        internal void ClearContentPhase()
        {
            contentPhase = null; contentPixels = false; contentCaptureFrame = -1;
            contentProgress = pendingContent == null ? 1 : 0;
            observed(Observe());
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
            double now = clock(); opacity = current.SampleOpacity(now);
            translation = current.SampleTranslation(now); Apply();
            if (!current.Complete(now)) return;
            segment = null; animation.Pause();
            Settle(segmentDisplay, segmentVisibility);
        }

        private void Reconcile()
        {
            if (!RequestedVisible)
            {
                if (segment?.Phase == UiTransitionPhase.Exiting) return;
                if (opacity == 0 || !RenderedVisible) { Settle(Requested, requestedVisibility); return; }
                Begin(UiTransitionPhase.Exiting, 0, seconds(), requestedSite); return;
            }
            if (segment?.Phase == UiTransitionPhase.Entering) return;
            if (opacity == 1 && RenderedVisible) { Settle(Requested, requestedVisibility); return; }
            Begin(UiTransitionPhase.Entering, 1, seconds(), requestedSite);
        }

        private void Begin(UiTransitionPhase phase, float destination, float duration, UiTransitionSite source)
        {
            UiTransitionProtocol.RequireVisibility(segment?.Phase ??
                (RenderedVisible ? UiTransitionPhase.Shown : UiTransitionPhase.Hidden), phase);
            if (!float.IsFinite(duration) || duration <= 0 || !float.IsFinite(distance()) || distance() < 0)
                throw new InvalidOperationException("UI region motion requires a finite positive duration and nonnegative distance.");
            if (!ownsStyles) translation = phase == UiTransitionPhase.Entering ? distance() : 0;
            AcquireStyles(); activeSite = source;
            segment = new UiVisibilitySegment(phase, opacity, destination, translation,
                destination == 0 ? -distance() : 0, clock(), duration);
            segmentDisplay = Requested; segmentVisibility = requestedVisibility;
            intermediateFrames = 0; lastIntermediateFrame = -1;
            SettleInput();
            if (phase == UiTransitionPhase.Entering)
            {
                rendered = Requested; renderedVisibility = requestedVisibility;
                element.style.display = rendered; element.style.visibility = renderedVisibility;
            }
            Apply(); animation.Resume();
        }

        private void Settle(DisplayStyle display, StyleEnum<Visibility> visibility)
        {
            segment = null; animation.Pause(); rendered = display;
            renderedVisibility = visibility; opacity = RenderedVisible ? 1 : 0;
            element.style.display = display; element.style.visibility = visibility; RestoreStyles();
        }

        private void AcquireStyles()
        {
            if (ownsStyles) return;
            ownsStyles = true;
            originalOpacity = element.style.opacity; originalTranslate = element.style.translate;
            authoredOpacity = element.resolvedStyle.opacity; authoredTranslate = element.resolvedStyle.translate;
        }

        private void Apply()
        {
            element.style.opacity = authoredOpacity * opacity;
            float baseY = authoredTranslate.y.unit == LengthUnit.Percent
                ? authoredTranslate.y.value * element.layout.height / 100 : authoredTranslate.y.value;
            element.style.translate = new Translate(authoredTranslate.x, baseY + translation, authoredTranslate.z);
        }

        private void RestoreStyles()
        {
            if (!ownsStyles) return;
            element.style.opacity = originalOpacity; element.style.translate = originalTranslate;
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
        {
            if ((segment != null || pendingContent != null || contentPhase != null || !RequestedVisible) &&
                !UiPresentation.PreservesInput(element, evt)) evt.StopImmediatePropagation();
        }

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

        public UiTransitionFrame Observe()
        {
            bool replacing = contentPhase != null || pendingContent != null;
            var phase = retired ? UiTransitionPhase.Retired : contentPhase ??
                (pendingContent != null ? UiTransitionPhase.CapturingReplacement : segment?.Phase ??
                    (RenderedVisible ? UiTransitionPhase.Shown : UiTransitionPhase.Hidden));
            return new UiTransitionFrame(activeSite, element, RequestedVisible, phase,
                replacing ? contentProgress : opacity, replacing ? contentIntermediateFrames : intermediateFrames, clock(),
                contentPixels, contentCaptureFrame);
        }

        private void Detached(DetachFromPanelEvent evt)
        {
            if (ReferenceEquals(evt.target, element)) release(element);
        }

        public void Dispose()
        {
            commit.Pause(); pendingContent = null;
            var retirement = new UiTransitionRetirement();
            retirement.Attempt(() => contentRemoved(this)); retirement.Attempt(() => Settle(Requested, requestedVisibility)); retired = true;
            retirement.Attempt(() => RegisterInput(false));
            element.UnregisterCallback<DetachFromPanelEvent>(Detached);
            retirement.Attempt(() => observed(Observe())); retirement.ThrowIfFailed();
        }
    }
}
