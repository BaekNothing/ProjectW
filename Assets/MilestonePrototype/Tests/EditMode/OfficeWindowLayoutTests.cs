using NUnit.Framework;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class OfficeWindowLayoutTests
    {
        [Test]
        public void FirstEntryIsAnEmptyDesktop()
        {
            var layout = new OfficeWindowLayout();
            Assert.That(layout.Front, Is.EqualTo(-1));
            for (int i = 0; i < OfficeWindowLayout.AppCount; i++) Assert.That(layout.Get(i).Open, Is.False);
        }
        [Test]
        public void MultipleWindowsRemainOpenAndHitTestFollowsFocus()
        {
            var layout = new OfficeWindowLayout();
            layout.Show(0); layout.Show(4);
            layout.Get(0).Rect = layout.Get(4).Rect = new Rect(200, 100, 700, 500);
            Assert.That(layout.HitTest(new Vector2(300, 200)), Is.EqualTo(4));
            layout.Focus(0);
            Assert.That(layout.HitTest(new Vector2(300, 200)), Is.EqualTo(0));
            Assert.That(layout.Get(4).Open, Is.True);
        }
        [Test]
        public void MinimizeAndRestoreKeepWindowSizeAndAppState()
        {
            var layout = new OfficeWindowLayout();
            layout.Show(2);
            var window = layout.Get(2);
            window.Rect = new Rect(310, 90, 720, 480);
            window.Scroll = new Vector2(0, 144);
            window.Person = 1;
            layout.Minimize(2);
            Assert.That(layout.HitTest(new Vector2(400, 200)), Is.EqualTo(-1));
            Assert.That(window.Open, Is.True);
            layout.Show(2);
            Assert.That(window.Minimized, Is.False);
            Assert.That(window.Rect, Is.EqualTo(new Rect(310, 90, 720, 480)));
            Assert.That(window.Scroll.y, Is.EqualTo(144));
            Assert.That(window.Person, Is.EqualTo(1));
        }
        [Test]
        public void ReopeningExistingAppDoesNotDuplicateWindowAndCloseRemovesHitTarget()
        {
            var layout = new OfficeWindowLayout();
            layout.Show(0); layout.Show(1); layout.Show(1);
            layout.Close(1);
            Assert.That(layout.Front, Is.EqualTo(0));
            Assert.That(layout.Get(1).Open, Is.False);
            layout.Close(0);
            Assert.That(layout.Front, Is.EqualTo(-1));
        }
        [TestCase(-600f, -100f, 20f, 20f)]
        [TestCase(1200f, 900f, 2000f, 2000f)]
        [TestCase(400f, 300f, 800f, 600f)]
        public void MoveAndResizeKeepControlsInsideDesktop(float x, float y, float w, float h)
        {
            Rect rect = OfficeWindowLayout.Constrain(new Rect(x, y, w, h), 1280, 800);
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(640));
            Assert.That(rect.height, Is.GreaterThanOrEqualTo(420));
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(8));
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(38));
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(1272));
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(738));
        }
        [TestCase(true, false, -1, true)]
        [TestCase(false, true, -1, true)]
        [TestCase(false, false, 0, true)]
        [TestCase(false, false, -1, false)]
        public void OverlayOrWindowPreventsDesktopClickThrough(bool modal, bool toast, int window, bool blocked)
            => Assert.That(OfficeWindowLayout.BlocksDesktop(modal, toast, window), Is.EqualTo(blocked));
        [Test]
        public void InvalidApplicationDoesNotChangeFocus()
        {
            var layout = new OfficeWindowLayout();
            Assert.That(layout.Show(-1), Is.False);
            Assert.That(layout.Show(7), Is.False);
            layout.Focus(7); layout.Minimize(-1); layout.Close(7);
            Assert.That(layout.Front, Is.EqualTo(-1));
        }
    }
}
