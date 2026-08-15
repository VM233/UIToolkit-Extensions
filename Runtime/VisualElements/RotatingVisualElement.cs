using UnityEngine;
using UnityEngine.UIElements;

namespace VM233.UIElements
{
    [UxmlElement]
    public partial class RotatingVisualElement : VisualElement
    {
        private const long UPDATE_INTERVAL_MILLISECONDS = 16;

        private IVisualElementScheduledItem rotationUpdate;
        private double previousUpdateTime;
        private float rotationDegrees;

        [UxmlAttribute("degrees-per-second")]
        public float DegreesPerSecond { get; set; }

        public RotatingVisualElement()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            previousUpdateTime = Time.realtimeSinceStartupAsDouble;
            rotationUpdate = schedule.Execute(UpdateRotation).Every(UPDATE_INTERVAL_MILLISECONDS);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            rotationUpdate.Pause();
            rotationUpdate = null;
        }

        private void UpdateRotation()
        {
            var currentTime = Time.realtimeSinceStartupAsDouble;
            var elapsedSeconds = (float)(currentTime - previousUpdateTime);
            previousUpdateTime = currentTime;

            rotationDegrees = Mathf.Repeat(
                rotationDegrees + DegreesPerSecond * elapsedSeconds,
                360f);
            style.rotate = new Rotate(Angle.Degrees(rotationDegrees));
        }
    }
}
