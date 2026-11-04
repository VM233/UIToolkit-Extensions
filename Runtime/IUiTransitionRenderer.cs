using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace VM233.UIElements
{
    public interface IUiTransitionRenderer
    {
        Task<IUiTransitionImage> CaptureAsync(VisualElement root, Func<Rect> captureBounds, CancellationToken cancellationToken);
        Task WaitForRenderedFrameAsync(CancellationToken cancellationToken);
    }
}
