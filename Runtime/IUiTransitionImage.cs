using System;

namespace VM233.UIElements
{
    public interface IUiTransitionImage : IDisposable
    {
        bool ContainsSourcePixels { get; }
        int CapturedFrame { get; }
        void SetSourceOpacity(float opacity);
    }
}
