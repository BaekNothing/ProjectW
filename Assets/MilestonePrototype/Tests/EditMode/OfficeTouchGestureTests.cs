using NUnit.Framework;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class OfficeTouchGestureTests
    {
        private OfficeWindowLayout layout;
        private OfficeTouchGesture gesture;
        [SetUp] public void Setup()
        {
            layout = new OfficeWindowLayout(); layout.Show(0);
            layout.Get(0).Rect = new Rect(200, 60, 800, 600);
            layout.Get(0).ContentHeight = 1100;
            gesture = new OfficeTouchGesture();
        }
        private void Touch(int count, Vector2 a, Vector2 b = default, bool blocked = false, int id = 1)
            => gesture.Process(layout, count, id, 2, a, b, 1, 1280, 800, blocked);
        [Test] public void SmallMotionRemainsATap()
        {
            Touch(1, new Vector2(400, 300)); Touch(1, new Vector2(403, 302));
            Assert.That(gesture.SuppressPointer, Is.False);
            Assert.That(layout.Get(0).Scroll.y, Is.Zero);
        }
        [Test] public void ContentDragScrollsAndConsumesRelease()
        {
            Touch(1, new Vector2(400, 300)); Touch(1, new Vector2(400, 240));
            Assert.That(layout.Get(0).Scroll.y, Is.EqualTo(60));
            Assert.That(gesture.SuppressPointer, Is.True);
            Touch(0, default); Assert.That(gesture.SuppressPointer, Is.True);
            Touch(0, default); Assert.That(gesture.SuppressPointer, Is.False);
        }
        [Test] public void ScrollClampsAtBothEnds()
        {
            Touch(1, new Vector2(400, 300)); Touch(1, new Vector2(400, -2000));
            Assert.That(layout.Get(0).Scroll.y, Is.EqualTo(665));
            Touch(1, new Vector2(400, 2000));
            Assert.That(layout.Get(0).Scroll.y, Is.Zero);
        }
        [Test] public void TitlebarDoesNotStartContentScroll()
        {
            Touch(1, new Vector2(400, 80)); Touch(1, new Vector2(400, 240));
            Assert.That(gesture.SuppressPointer, Is.False);
            Assert.That(layout.Get(0).Scroll.y, Is.Zero);
        }
        [Test] public void PinchChangesWindowAndRemainingFingerCannotScrollOrClick()
        {
            Touch(2, new Vector2(400, 300), new Vector2(600, 300));
            Touch(2, new Vector2(375, 300), new Vector2(625, 300));
            Assert.That(layout.Get(0).Rect.width, Is.EqualTo(1000));
            Assert.That(gesture.SuppressPointer, Is.True);
            Touch(1, new Vector2(400, 200));
            Assert.That(gesture.SuppressPointer, Is.True);
            Assert.That(layout.Get(0).Scroll.y, Is.Zero);
        }
        [Test] public void PinchShrinksToMinimumWithoutLeavingDesktop()
        {
            Touch(2, new Vector2(400, 300), new Vector2(800, 300));
            Touch(2, new Vector2(580, 300), new Vector2(620, 300));
            Assert.That(layout.Get(0).Rect.width, Is.EqualTo(640));
            Assert.That(layout.Get(0).Rect.height, Is.EqualTo(420));
            Assert.That(layout.Get(0).Rect.xMin, Is.GreaterThanOrEqualTo(8));
        }
        [Test] public void TwoFingersOnDifferentWindowsDoNotResizeEither()
        {
            layout.Show(1); layout.Get(1).Rect = new Rect(650, 80, 640, 500);
            Touch(2, new Vector2(400, 300), new Vector2(800, 300));
            Touch(2, new Vector2(350, 300), new Vector2(850, 300));
            Assert.That(layout.Get(0).Rect.width, Is.EqualTo(800));
            Assert.That(layout.Get(1).Rect.width, Is.EqualTo(640));
            Assert.That(gesture.SuppressPointer, Is.True);
        }
        [Test] public void OverlayStartCannotTurnIntoBackgroundScroll()
        {
            Touch(1, new Vector2(400, 300), blocked: true);
            Touch(1, new Vector2(400, 200));
            Assert.That(layout.Get(0).Scroll.y, Is.Zero);
            Assert.That(gesture.SuppressPointer, Is.False);
        }
        [Test] public void ReplacementFingerDoesNotJumpScroll()
        {
            Touch(1, new Vector2(400, 300)); Touch(1, new Vector2(400, 200), id: 3);
            Assert.That(layout.Get(0).Scroll.y, Is.Zero);
            Assert.That(gesture.SuppressPointer, Is.True);
        }
        [Test] public void ThresholdUsesScreenPixelsInsteadOfScaledCanvasPixels()
        {
            gesture.Process(layout, 1, 1, 0, new Vector2(400, 300), default, .5f, 1280, 800, false);
            gesture.Process(layout, 1, 1, 0, new Vector2(400, 290), default, .5f, 1280, 800, false);
            Assert.That(gesture.SuppressPointer, Is.False);
            gesture.Process(layout, 1, 1, 0, new Vector2(400, 280), default, .5f, 1280, 800, false);
            Assert.That(gesture.SuppressPointer, Is.True);
        }
    }
}
