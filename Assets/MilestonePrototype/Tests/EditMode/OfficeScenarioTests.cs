using NUnit.Framework;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class OfficeScenarioTests
    {
        [TestCase("ko")]
        [TestCase("en")]
        [TestCase("tid")]
        public void EverySevenDayPathKeepsLocalizedReportsAfterSaveRestore(string locale)
        {
            string originalLocale = SheetContent.Locale;
            try
            {
                SheetContent.SetLocale(locale);
                for (int path = 0; path < 128; path++)
                {
                    var scenario = new OfficeScenario();
                    var reports = new string[7];
                    for (int day = 0; day < 7; day++)
                    {
                        Assert.That(scenario.Choose((path >> day) & 1), Is.True);
                        reports[day] = scenario.Report(day);
                        Assert.That(reports[day], Is.Not.Empty);
                        if (locale != "tid") Assert.That(reports[day], Does.Not.StartWith("office."));
                        Assert.That(scenario.Advance(), Is.True);
                        var restored = new OfficeScenario();
                        Assert.That(restored.Restore(scenario.Export()), Is.True);
                        for (int prior = 0; prior <= day; prior++)
                            Assert.That(restored.Report(prior), Is.EqualTo(reports[prior]));
                        scenario = restored;
                    }
                    Assert.That(scenario.Complete, Is.True);
                }
            }
            finally { SheetContent.SetLocale(originalLocale); }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SevenDaysPreserveDecisionsAndEndWithoutFurtherTransitions(int option)
        {
            var scenario = new OfficeScenario();
            for (int day = 1; day <= 7; day++)
            {
                Assert.That(scenario.Day, Is.EqualTo(day));
                Assert.That(scenario.Advance(), Is.False);
                Assert.That(scenario.Choose(option), Is.True);
                Assert.That(scenario.Advance(), Is.True);
                var restored = new OfficeScenario();
                Assert.That(restored.Restore(scenario.Export()), Is.True);
                Assert.That(restored.Day, Is.EqualTo(day + 1));
                Assert.That(restored.Choice(day - 1), Is.EqualTo(option));
                scenario = restored;
            }
            Assert.That(scenario.Complete, Is.True);
            Assert.That(scenario.Advance(), Is.False);
            Assert.That(scenario.Choose(0), Is.False);
        }

        [TestCase("")]
        [TestCase("{}")]
        [TestCase("not json")]
        [TestCase("{\"SchemaVersion\":2,\"ScenarioId\":\"office-seven-days\",\"Day\":1,\"Choices\":[-1,-1,-1,-1,-1,-1,-1]}")]
        [TestCase("{\"SchemaVersion\":1,\"ScenarioId\":\"office-seven-days\",\"Day\":3,\"Choices\":[0,-1,-1,-1,-1,-1,-1]}")]
        [TestCase("{\"SchemaVersion\":1,\"ScenarioId\":\"office-seven-days\",\"Day\":1,\"Choices\":[-1,0,-1,-1,-1,-1,-1]}")]
        [TestCase("{\"SchemaVersion\":1,\"ScenarioId\":\"campaign\",\"Day\":1,\"Choices\":[-1,-1,-1,-1,-1,-1,-1]}")]
        public void InvalidSaveIsRejectedWithoutReplacingCurrentState(string json)
        {
            var scenario = new OfficeScenario();
            scenario.Choose(1);
            string before = scenario.Export();
            Assert.That(scenario.Restore(json), Is.False);
            Assert.That(scenario.Export(), Is.EqualTo(before));
        }

        [Test]
        public void BranchesProduceDifferentFollowupsAndRejectInvalidChoices()
        {
            var scenario = new OfficeScenario();
            Assert.That(scenario.Choose(-1), Is.False);
            Assert.That(scenario.Choose(2), Is.False);
            scenario.Choose(0);
            string first = scenario.Report(0);
            scenario.Choose(1);
            Assert.That(scenario.Report(0), Is.Not.EqualTo(first));
            Assert.That(OfficeScenario.PersonIds, Is.Unique);
            Assert.That(OfficeScenario.SaveKey, Is.Not.EqualTo("projectw.campaign.v1"));
        }
    }
}
