using NUnit.Framework;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class NotificationCenterTests
    {
        [Test]
        public void NewestNotificationComesFirstAndOldestIsDiscardedAtCapacity()
        {
            var center = new NotificationCenter(2, 6f);

            center.Add("메일", "첫 번째", "A", 1, 0f);
            center.Add("활동", "두 번째", "B", 2, 1f);
            center.Add("메일", "세 번째", "C", 3, 2f);

            Assert.That(center.Count, Is.EqualTo(2));
            Assert.That(center.Get(0).Title, Is.EqualTo("세 번째"));
            Assert.That(center.Get(1).Title, Is.EqualTo("두 번째"));
        }

        [Test]
        public void BannerExpiresButEntryRemainsInHistory()
        {
            var center = new NotificationCenter(10, 6f);
            center.Add("메일", "새 메일", "내용", 4, 10f);

            Assert.That(center.ActiveBannerCount(15.99f, 3), Is.EqualTo(1));
            Assert.That(center.ActiveBannerCount(16f, 3), Is.Zero);
            Assert.That(center.Count, Is.EqualTo(1));
        }

        [Test]
        public void ActiveBannerListSkipsExpiredEntriesAndHonorsMaximum()
        {
            var center = new NotificationCenter(10, 5f);
            center.Add("활동", "만료", "old", 1, 0f);
            center.Add("메일", "표시 1", "new", 2, 5f);
            center.Add("활동", "표시 2", "newest", 2, 6f);

            Assert.That(center.ActiveBannerCount(7f, 1), Is.EqualTo(1));
            Assert.That(center.ActiveBannerAt(7f, 0).Title, Is.EqualTo("표시 2"));
            Assert.That(center.ActiveBannerAt(7f, 1).Title, Is.EqualTo("표시 1"));
            Assert.That(center.NextExpiry(7f), Is.EqualTo(10f));
        }
    }
}
