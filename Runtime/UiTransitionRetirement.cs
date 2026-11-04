using System;
using System.Collections.Generic;

namespace VM233.UIElements
{
    internal sealed class UiTransitionRetirement
    {
        private readonly List<Exception> failures = new();
        internal UiTransitionRetirement(Exception primary = null)
        {
            if (primary != null) failures.Add(primary);
        }
        internal void Attempt(Action action)
        {
            try { action(); }
            catch (Exception error) { failures.Add(error); }
        }
        internal void ThrowIfFailed()
        {
            if (failures.Count > 0) throw new AggregateException("UI presentation retirement failed.", failures);
        }
    }
}
