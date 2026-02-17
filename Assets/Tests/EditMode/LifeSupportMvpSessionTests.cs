using NUnit.Framework;
using ProjectW.IngameMvp;

namespace ProjectW.Tests.EditMode
{
    public class LifeSupportMvpSessionTests
    {
        [Test]
        public void Core_CompletesRepairAndReturn_WhenNoDisaster()
        {
            var config = new LifeSupportMvpConfig
            {
                requiredRepairTicks = 50,
                halfDayTicks = 30,
                disasterCheckIntervalTicks = 10,
                disasterChance = 0f,
                disasterLossRate = 0.10f,
                returnTicks = 10
            };

            var core = new LifeSupportMvpCore(config, seed: 7);

            int maxTicks = 300;
            for (int i = 0; i < maxTicks && !core.IsComplete; i++)
            {
                core.Step();
            }

            Assert.IsTrue(core.IsComplete);
            Assert.AreEqual(MvpSessionPhase.Returned, core.CurrentPhase);
            Assert.AreEqual(10, core.ReturnProgressTicks);
            Assert.GreaterOrEqual(core.RepairProgressTicks, 50f);
        }

        [Test]
        public void Core_LosesTenPercentProgress_WhenDisasterOccurs()
        {
            var config = new LifeSupportMvpConfig
            {
                requiredRepairTicks = 999,
                halfDayTicks = 30,
                disasterCheckIntervalTicks = 10,
                disasterChance = 1f,
                disasterLossRate = 0.10f,
                returnTicks = 10
            };

            var core = new LifeSupportMvpCore(config, seed: 1);

            for (int i = 0; i < 10; i++)
            {
                core.Step();
            }

            Assert.AreEqual(10, core.TickIndex);
            Assert.AreEqual(8.1f, core.RepairProgressTicks, 0.001f);
        }
    }
}
