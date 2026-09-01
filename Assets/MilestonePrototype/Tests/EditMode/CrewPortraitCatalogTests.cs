using NUnit.Framework;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class CrewPortraitCatalogTests
    {
        [Test]
        public void ProductionCrewUsesStablePortraitAddressesInRosterOrder()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();

            Assert.That(data.Crew, Has.Length.EqualTo(CrewPortraitCatalog.Count));
            for (int index = 0; index < data.Crew.Length; index++)
                Assert.That(data.Crew[index].PortraitAddress,
                    Is.EqualTo(CrewPortraitCatalog.ExpectedAddressForSlot(index)));
        }

        [Test]
        public void PortraitCatalogRejectsUnknownRosterSlots()
        {
            Assert.That(CrewPortraitCatalog.ExpectedAddressForSlot(-1), Is.Empty);
            Assert.That(CrewPortraitCatalog.ExpectedAddressForSlot(CrewPortraitCatalog.Count), Is.Empty);
        }

        [Test]
        public void ValidationRejectsCrewWithoutPortraitAddress()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Crew[0].PortraitAddress = string.Empty;

            Assert.That(() => TaskSystemDataLoader.Validate(data),
                Throws.InvalidOperationException.With.Message.Contains("portrait address"));
        }

        [Test]
        public void RestoreBackfillsPortraitAddressForOlderCampaignSaves()
        {
            var original = new MilestoneSimulation(1);
            CampaignSnapshot snapshot = original.CreateSnapshot();
            snapshot.Crew[0].PortraitAddress = null;

            var restored = new MilestoneSimulation(2);

            Assert.That(restored.Restore(snapshot), Is.True);
            Assert.That(restored.Crew[0].PortraitAddress, Is.EqualTo(CrewPortraitCatalog.HanTech));
        }
    }
}
