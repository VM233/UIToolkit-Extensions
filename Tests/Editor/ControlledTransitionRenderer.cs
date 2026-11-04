using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace VM233.UIElements.Editor.Tests
{
    // This controlled resource proves the publication/retirement protocol, never native pixels.
    internal sealed class ControlledTransitionRenderer : IUiTransitionRenderer
    {
        private TaskCompletionSource<IUiTransitionImage> capture;
        private TaskCompletionSource<bool> render;
        private CancellationTokenRegistration captureCancellation, renderCancellation;
        private Func<Rect> captureBounds;
        internal Task CaptureTask => capture.Task;
        internal Task RenderTask => render.Task;
        internal bool CapturePending => capture != null && !capture.Task.IsCompleted;
        internal bool RenderPending => render != null && !render.Task.IsCompleted;
        internal ControlledImage Image { get; private set; }

        public Task<IUiTransitionImage> CaptureAsync(VisualElement root, Func<Rect> bounds, CancellationToken cancellationToken)
        {
            captureBounds = bounds;
            capture = new TaskCompletionSource<IUiTransitionImage>();
            var owned = capture;
            captureCancellation = cancellationToken.Register(() => owned.SetCanceled(cancellationToken));
            return capture.Task;
        }

        internal void CompleteCapture()
        {
            var bounds = captureBounds();
            if (bounds.width <= 0 || bounds.height <= 0) throw new InvalidOperationException("The controlled source bounds are empty.");
            captureCancellation.Dispose();
            Image = new ControlledImage(Time.frameCount);
            capture.SetResult(Image);
        }

        public Task WaitForRenderedFrameAsync(CancellationToken cancellationToken)
        {
            render = new TaskCompletionSource<bool>();
            var owned = render;
            renderCancellation = cancellationToken.Register(() => owned.SetCanceled(cancellationToken));
            return render.Task;
        }

        internal void CompleteRender()
        {
            renderCancellation.Dispose(); render.SetResult(true);
        }

        internal sealed class ControlledImage : IUiTransitionImage
        {
            internal int Disposals { get; private set; }
            internal float Opacity { get; private set; } = 1;
            public bool ContainsSourcePixels => Disposals == 0;
            public int CapturedFrame { get; }
            internal ControlledImage(int frame) => CapturedFrame = frame;
            public void SetSourceOpacity(float opacity) => Opacity = opacity;
            public void Dispose() => Disposals++;
        }
    }
}
