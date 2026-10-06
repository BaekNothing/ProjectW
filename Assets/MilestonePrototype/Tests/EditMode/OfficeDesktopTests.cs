using NUnit.Framework;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class OfficeDesktopTests
    {
        [TestCase("ko")]
        [TestCase("en")]
        public void AuthoredReportsRemainReadableAtMinimumWindowWidth(string locale)
        {
            string originalLocale = SheetContent.Locale;
            try
            {
                SheetContent.SetLocale(locale);
                var style = new GUIStyle { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), fontSize = 17, wordWrap = true };
                float narrow = OfficeWindowLayout.MinimumWidth - 86;
                bool expanded = false;
                foreach (string report in OfficeScenario.Results)
                {
                    float height = OfficeDesktop.TextHeight(report, style, narrow, 70);
                    Assert.That(height, Is.GreaterThanOrEqualTo(style.CalcHeight(new GUIContent(report), narrow)));
                    Assert.That(height, Is.GreaterThanOrEqualTo(OfficeDesktop.TextHeight(report, style, narrow + 300, 70)));
                    expanded |= height > 70;
                    Assert.That(388 + OfficeDesktop.TextHeight(report, style, 632, 126), Is.LessThanOrEqualTo(800));
                }
                Assert.That(expanded, Is.True, "Fixture must exercise reports that exceed the old fixed height.");
                Assert.That(style.fontSize, Is.EqualTo(17));
            }
            finally { SheetContent.SetLocale(originalLocale); }
        }

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
