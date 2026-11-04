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
        private UiTransitionSite site;
        private DisplayStyle rendered, destination;
        private float opacity, origin;
        private float destinationOpacity;
        private bool contentInitialized;
        private object publishedIdentity, requestedIdentity;
        private Action pendingContent;
        private double began;
        private float activeDuration;
        private bool moving, ownsStyles, retired;
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
            this.element = element; this.seconds = seconds; this.distance = distance; this.observed = observed; this.release = release;
            this.clock = clock;
            rendered = destination = Requested = element.style.display.keyword == StyleKeyword.Undefined
                ? element.style.display.value : element.resolvedStyle.display;
            opacity = rendered == DisplayStyle.None ? 0 : 1;
            commit = element.schedule.Execute(Commit); commit.Pause();
            animation = element.schedule.Execute(Advance).Every(16); animation.Pause();
            RegisterInput(true);
            element.RegisterCallback<DetachFromPanelEvent>(Detached);
        }

        internal void Request(DisplayStyle display, UiTransitionSite source, bool animate)
        {
            site = source;
            Requested = display;
            if (!animate)
            {
                commit.Pause(); animation.Pause(); moving = false;
                RestoreStyles(); rendered = display; opacity = display == DisplayStyle.None ? 0 : 1;
                element.style.display = display; observed(Observe());
            }
            else commit.ExecuteLater(0);
        }

        private void Commit()
        {
            bool replacing = pendingContent != null && Requested != DisplayStyle.None;
            float target = Requested == DisplayStyle.None || replacing ? 0 : 1;
            if (moving && Requested == destination && target == destinationOpacity) return;
            if (Requested == rendered && !moving && !replacing) return;
            if (moving) Advance();
            destination = Requested;
            destinationOpacity = target;
            if (target == opacity) { Complete(); return; }
            float duration = seconds();
            if (!float.IsFinite(duration) || duration <= 0 || !float.IsFinite(distance()) || distance() < 0)
                throw new InvalidOperationException("UI region motion requires a finite positive duration and nonnegative distance.");
            AcquireStyles();
            origin = opacity; began = clock(); activeDuration = replacing ? duration * .5f : duration; moving = true;
            intermediateFrames = 0; lastIntermediateFrame = -1;
            SettleInput();
            if (destination == DisplayStyle.None) FreezeOutgoingLayout();
            else { RestoreLayout(); element.style.display = DisplayStyle.Flex; }
            Apply(); animation.Resume(); observed(Observe());
        }

        internal void Replace(object identity, Action publish, UiTransitionSite source, bool animate)
        {
            site = source; requestedIdentity = identity;
            if (!contentInitialized || !animate)
            {
                contentInitialized = true; publishedIdentity = identity; pendingContent = null;
                publish();
                return;
            }
            if (Equals(publishedIdentity, identity))
            {
                pendingContent = null; publish();
                if (moving && destinationOpacity == 0 && Requested != DisplayStyle.None) commit.ExecuteLater(0);
                return;
            }
            pendingContent = publish;
            commit.ExecuteLater(0);
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

        private void Advance()
        {
            float fraction = (float)((clock() - began) / activeDuration);
            if (fraction >= 1) { Complete(); return; }
            float ease = 1 - (1 - fraction) * (1 - fraction) * (1 - fraction);
            opacity = Mathf.LerpUnclamped(origin, destinationOpacity, ease);
            Apply();
            if (opacity > 0 && opacity < 1 && lastIntermediateFrame != Time.frameCount)
            { intermediateFrames++; lastIntermediateFrame = Time.frameCount; }
            observed(Observe());
        }

        private void Apply()
        {
            element.style.opacity = authoredOpacity * opacity;
            float offset = distance() * (1 - opacity) * (destinationOpacity == 0 ? -1 : 1);
            float baseY = authoredTranslate.y.unit == LengthUnit.Percent
                ? authoredTranslate.y.value * element.layout.height / 100 : authoredTranslate.y.value;
            element.style.translate = new Translate(authoredTranslate.x, baseY + offset, authoredTranslate.z);
        }

        private void Complete()
        {
            if (pendingContent != null && Requested != DisplayStyle.None)
            {
                var publish = pendingContent; pendingContent = null;
                publishedIdentity = requestedIdentity;
                opacity = origin = 0; destination = Requested; destinationOpacity = 1;
                began = clock(); activeDuration = seconds() * .5f; moving = true;
                publish(); Apply(); animation.Resume(); observed(Observe());
                return;
            }
            moving = false; animation.Pause();
            rendered = destination; opacity = destinationOpacity;
            element.style.display = rendered;
            if (opacity == 1 || rendered == DisplayStyle.None) RestoreStyles();
            else Apply();
            observed(Observe());
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
        { if (moving || pendingContent != null || Requested == DisplayStyle.None) evt.StopImmediatePropagation(); }

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

        public UiTransitionFrame Observe() => new(site, element, Requested != DisplayStyle.None,
            moving, opacity, intermediateFrames, retired);

        private void Detached(DetachFromPanelEvent evt)
        {
            if (ReferenceEquals(evt.target, element)) release(element);
        }

        public void Dispose()
        {
            commit.Pause(); animation.Pause(); moving = false; retired = true;
            pendingContent = null;
            element.style.display = Requested; RestoreStyles();
            RegisterInput(false); element.UnregisterCallback<DetachFromPanelEvent>(Detached);
            observed(Observe());
        }
    }
}
