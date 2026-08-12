using UnityEngine.UIElements;

namespace VM233.UIElements
{
    [UxmlElement]
    public partial class BoolStateVisualElement : BaseBoolField
    {
        public BoolStateVisualElement() : base(null)
        {
            hierarchy.Clear();
            RemoveFromClassList(BaseField<bool>.ussClassName);
            RemoveFromClassList(BaseField<bool>.noLabelVariantUssClassName);

            pickingMode = PickingMode.Ignore;
            focusable = false;
        }
    }
}
