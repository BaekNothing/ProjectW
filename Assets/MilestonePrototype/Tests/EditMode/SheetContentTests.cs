using NUnit.Framework;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class SheetContentTests
    {
        [Test] public void TranslationFallbackAndExplicitEmptyAreDifferent()
        {
            var row = new SheetText { tid = "test.name", ko = "이름", en = "" };
            Assert.That(SheetContent.Resolve(row, "en"), Is.EqualTo("이름"));
            Assert.That(SheetContent.Resolve(row, "tid"), Is.EqualTo("test.name"));
            row.en = "<EMPTY>";
            Assert.That(SheetContent.Resolve(row, "en"), Is.Empty);
            row.en = ""; row.ko = "";
            Assert.That(SheetContent.Resolve(row, "en"), Is.EqualTo("test.name"));
        }
        [Test] public void SamplePerksChangeTaskProbabilityAndRespectCaps()
        {
            var data = SheetContent.Data;
            Assert.That(SheetContent.TaskSuccess(data, "yunha", "listen_to_friend"), Is.EqualTo(.9f).Within(.0001));
            Assert.That(SheetContent.TaskSuccess(data, "doyun", "listen_to_friend"), Is.EqualTo(.7f).Within(.0001));
            Assert.That(SheetContent.TaskSuccess(data, "yunha", "listen_to_friend", 100), Is.EqualTo(.95f).Within(.0001));
            Assert.That(SheetContent.TaskSuccess(data, "yunha", "listen_to_friend", -200), Is.EqualTo(.05f).Within(.0001));
        }
        [Test] public void EventConditionsAndCooldownPrecedePerkMultipliers()
        {
            var data = SheetContent.Data;
            Assert.That(SheetContent.EventProbability(data, "yunha", "friend_invitation", "", 100), Is.EqualTo(.3f).Within(.0001));
            Assert.That(SheetContent.EventProbability(data, "taeo", "friend_invitation", "", 100), Is.Zero);
            Assert.That(SheetContent.EventProbability(data, "yunha", "friend_invitation", "", 0), Is.Zero);
            Assert.That(SheetContent.EventProbability(data, "doyun", "shift_request", "", 100), Is.Zero);
            Assert.That(SheetContent.EventProbability(data, "doyun", "shift_request", "employed;colleague_absent", 100), Is.EqualTo(.315f).Within(.0001));
        }
        [Test] public void TagMatchingUsesWholeTagsAndRequiresEveryTag()
        {
            Assert.That(SheetContent.TagsMatch("social;friends", "social;friends;night"), Is.True);
            Assert.That(SheetContent.TagsMatch("social;friends", "social"), Is.False);
            Assert.That(SheetContent.TagsMatch("friend", "friends"), Is.False);
        }
        [Test] public void LocaleSwitchRefreshesAuthoredOfficeText()
        {
            string previous = SheetContent.Locale;
            try
            {
                SheetContent.SetLocale("en"); Assert.That(OfficeScenario.Names[0], Is.EqualTo("Hana"));
                SheetContent.SetLocale("tid"); Assert.That(OfficeScenario.Names[0], Is.EqualTo("office.names.0"));
                SheetContent.SetLocale("ko"); Assert.That(OfficeScenario.Names[0], Is.EqualTo("하나"));
                Assert.Throws<System.ArgumentException>(() => SheetContent.SetLocale("fr"));
            }
            finally { SheetContent.SetLocale(previous); }
        }
    }
}
