using UnityEngine.UIElements;

namespace VM233.UIElements
{
    public interface IUiTransitionInputRegion
    {
        bool AcceptsTransitionInput(EventBase input);
    }
}
