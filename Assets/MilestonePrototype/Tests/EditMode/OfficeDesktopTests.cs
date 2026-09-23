using NUnit.Framework;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class OfficeDesktopTests
    {
        private string original;
        private bool existed;
        [SetUp] public void PreserveSave()
        {
            existed = PlayerPrefs.HasKey(OfficeScenario.SaveKey);
            original = PlayerPrefs.GetString(OfficeScenario.SaveKey);
        }
        [TearDown] public void RestoreSave()
        {
            if (existed) PlayerPrefs.SetString(OfficeScenario.SaveKey, original);
            else PlayerPrefs.DeleteKey(OfficeScenario.SaveKey);
        }
        [Test] public void DesktopResumesIndependentScenario()
        {
            var scenario = new OfficeScenario();
            scenario.Choose(1);
            scenario.Advance();
            PlayerPrefs.SetString(OfficeScenario.SaveKey, scenario.Export());
            var desktop = new OfficeDesktop();
            Assert.That(desktop.Scenario.Day, Is.EqualTo(2));
            Assert.That(desktop.SaveBlocked, Is.False);
        }
        [Test] public void UnsupportedSaveIsPreservedAndBlocksWriting()
        {
            PlayerPrefs.SetString(OfficeScenario.SaveKey, "unsupported future save");
            var desktop = new OfficeDesktop();
            Assert.That(desktop.SaveBlocked, Is.True);
            Assert.That(PlayerPrefs.GetString(OfficeScenario.SaveKey), Is.EqualTo("unsupported future save"));
        }
        [TestCase(1280, 800)]
        [TestCase(1280, 720)]
        [TestCase(800, 600)]
        public void WorkspaceFitsViewport(int width, int height)
        {
            float scale = OfficeDesktop.ScaleFor(width, height);
            Assert.That(1280 * scale, Is.LessThanOrEqualTo(width));
            Assert.That(800 * scale, Is.LessThanOrEqualTo(height));
        }
    }
}
