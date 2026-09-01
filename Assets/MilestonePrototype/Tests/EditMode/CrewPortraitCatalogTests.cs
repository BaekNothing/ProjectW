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
        public void ModularPortraitCatalogHasStableUniqueAddresses()
        {
            var addresses = new string[CrewPortraitCatalog.ModularAssetCount];
            for (int index = 0; index < addresses.Length; index++)
            {
                addresses[index] = CrewPortraitCatalog.ExpectedModularAddressForAsset(index);
                Assert.That(addresses[index], Is.Not.Empty);
            }

            Assert.That(addresses, Is.Unique);
            Assert.That(CrewPortraitCatalog.ExpectedModularAddressForAsset(-1), Is.Empty);
            Assert.That(CrewPortraitCatalog.ExpectedModularAddressForAsset(addresses.Length), Is.Empty);
        }

        [TestCase(0, 0, 0)]
        [TestCase(30, 0, 1)]
        [TestCase(55, 0, 2)]
        [TestCase(80, 0, 3)]
        [TestCase(0, 2, 3)]
        public void DarkCircleVariantTracksExistingCrewConditionThresholds(
            int fatigue, int injuryDays, int expectedVariant)
        {
            Assert.That(CrewPortraitCatalog.DarkCircleVariant(fatigue, injuryDays),
                Is.EqualTo(expectedVariant));
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
