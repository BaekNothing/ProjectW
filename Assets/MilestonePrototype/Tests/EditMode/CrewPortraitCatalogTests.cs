using NUnit.Framework;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class CrewPortraitCatalogTests
    {
        [TestCase(32f, 40f, 32f)]
        [TestCase(160f, 90f, 90f)]
        [TestCase(133f, 133f, 133f)]
        [TestCase(0f, 40f, 0f)]
        public void PortraitFitsSlotWithoutStretchingOrChangingItsCenter(
            float width, float height, float expectedSize)
        {
            var area = new UnityEngine.Rect(17f, 23f, width, height);
            var actual = CrewPortraitCatalog.FitPortraitRect(area);
            Assert.That(actual.width, Is.EqualTo(expectedSize));
            Assert.That(actual.height, Is.EqualTo(expectedSize));
            Assert.That(actual.center, Is.EqualTo(area.center));
            Assert.That(actual.xMin, Is.GreaterThanOrEqualTo(area.xMin));
            Assert.That(actual.yMin, Is.GreaterThanOrEqualTo(area.yMin));
            Assert.That(actual.xMax, Is.LessThanOrEqualTo(area.xMax));
            Assert.That(actual.yMax, Is.LessThanOrEqualTo(area.yMax));
        }

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
        public void ConditionPortraitCatalogHasStableUniqueAddresses()
        {
            var addresses = new string[CrewPortraitCatalog.ConditionAssetCount];
            for (int index = 0; index < addresses.Length; index++)
            {
                addresses[index] = CrewPortraitCatalog.ExpectedConditionAddressForAsset(index);
                Assert.That(addresses[index], Is.Not.Empty);
            }

            Assert.That(addresses, Is.Unique);
            Assert.That(CrewPortraitCatalog.ExpectedConditionAddressForAsset(-1), Is.Empty);
            Assert.That(CrewPortraitCatalog.ExpectedConditionAddressForAsset(addresses.Length), Is.Empty);
        }

        [Test]
        public void EveryRosterAndConditionMapsToItsOwnSheetCell()
        {
            int[] fatigue = { 0, 30, 55, 80 };
            for (int state = 0; state < 4; state++)
                for (int crew = 0; crew < 4; crew++)
                {
                    int index = CrewPortraitCatalog.ConditionAssetForCrew(crew, fatigue[state], 0);
                    Assert.That(index, Is.EqualTo(state * 4 + crew));
                    Assert.That(CrewPortraitCatalog.ExpectedConditionAddressForAsset(index),
                        Is.EqualTo(CrewPortraitCatalog.ExpectedAddressForSlot(crew) + "/condition-" + state));
                    Assert.That(CrewPortraitCatalog.ConditionAssetForCrew(crew, 0, 1), Is.EqualTo(12 + crew));
                }
            Assert.That(CrewPortraitCatalog.ConditionAssetForCrew(-1, 0, 0), Is.EqualTo(-1));
            Assert.That(CrewPortraitCatalog.ConditionAssetForCrew(4, 0, 0), Is.EqualTo(-1));
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
