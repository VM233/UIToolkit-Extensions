using NUnit.Framework;

namespace VM233.UIElements.Editor.Tests
{
    public sealed class UiVisibilitySegmentTests
    {
        [TestCase(.032)]
        [TestCase(.064)]
        [TestCase(.112)]
        public void ReversalStartsAtBothCurrentPropertyValues(double interruption)
        {
            var entering = new UiVisibilitySegment(UiTransitionPhase.Entering, 0, 1, 10, 0, 0, .16f);
            float opacity = entering.SampleOpacity(interruption), translation = entering.SampleTranslation(interruption);
            Assert.That(opacity, Is.InRange(.001f, .999f));
            var exiting = new UiVisibilitySegment(UiTransitionPhase.Exiting, opacity, 0, translation, -10, interruption, .16f);
            Assert.That(exiting.SampleOpacity(interruption), Is.EqualTo(opacity));
            Assert.That(exiting.SampleTranslation(interruption), Is.EqualTo(translation));
            float oldSignedProjection = -(1 - opacity) * 10;
            Assert.That(System.Math.Abs(oldSignedProjection - translation), Is.GreaterThan(.001f));
            Assert.That(exiting.SampleOpacity(interruption + .2), Is.Zero);
            Assert.That(exiting.SampleTranslation(interruption + .2), Is.EqualTo(-10));
        }

        [Test]
        public void RepeatedReversalsRetainTheCurrentPoseAndSettleAtTheLatestTarget()
        {
            var segment = new UiVisibilitySegment(UiTransitionPhase.Entering, 0, 1, 10, 0, 0, .16f);
            double now = 0;
            for (int index = 0; index < 6; index++)
            {
                now += .04;
                float opacity = segment.SampleOpacity(now), translation = segment.SampleTranslation(now);
                bool show = index % 2 != 0;
                segment = new UiVisibilitySegment(show ? UiTransitionPhase.Entering : UiTransitionPhase.Exiting,
                    opacity, show ? 1 : 0, translation, show ? 0 : -10, now, .16f);
                Assert.That(segment.SampleOpacity(now), Is.EqualTo(opacity));
                Assert.That(segment.SampleTranslation(now), Is.EqualTo(translation));
            }
            Assert.That(segment.SampleOpacity(now + .2), Is.EqualTo(1));
            Assert.That(segment.SampleTranslation(now + .2), Is.Zero);
            Assert.That(segment.Complete(now + .2), Is.True);
        }
    }
}
