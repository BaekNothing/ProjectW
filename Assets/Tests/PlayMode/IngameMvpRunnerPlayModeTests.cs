using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectW.IngameMvp;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectW.Tests.PlayMode
{
    public class IngameMvpRunnerPlayModeTests
    {
        [UnityTest]
        public IEnumerator Runner_InitializesAndSpawnsCharacters_FromCsv()
        {
            var go = new GameObject("IngameMvpRunner_PlayModeTest");
            var runner = go.AddComponent<IngameMvpRunner>();
            Assert.IsNotNull(runner);

            bool initialized = runner.InitializeAndRun();
            Assert.IsTrue(initialized);
            Assert.AreEqual(string.Empty, runner.LastErrorCode, "Runner should initialize with default CSV set.");

            int characterCount = 0;
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (tr.name.StartsWith("Character_"))
                {
                    characterCount++;
                }
            }

            Assert.GreaterOrEqual(characterCount, 1, "At least one character should be spawned from CharacterProfiles.csv.");
            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runner_WithMissingCsvFolder_ReturnsCsvError()
        {
            var go = new GameObject("PlayModeRunnerMissingCsv");
            var runner = go.AddComponent<IngameMvpRunner>();

            var csvField = typeof(IngameMvpRunner).GetField("csvFolderName", BindingFlags.Instance | BindingFlags.NonPublic);
            var autoStartField = typeof(IngameMvpRunner).GetField("autoStartOnPlay", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(csvField);
            Assert.IsNotNull(autoStartField);

            autoStartField.SetValue(runner, false);
            csvField.SetValue(runner, "FolderThatDoesNotExist");

            LogAssert.Expect(LogType.Error, "[E-CSV-004] Required file missing: SessionConfig.csv");
            bool initialized = runner.InitializeAndRun();
            Assert.IsFalse(initialized);
            Assert.AreEqual("E-CSV-004", runner.LastErrorCode);

            Object.Destroy(go);
            yield return null;
        }
    }
}
