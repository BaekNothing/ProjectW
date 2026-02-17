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
        public IEnumerator SampleScene_BootstrapsRunner_AndSpawnsCharacters()
        {
            var load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            Assert.IsNotNull(load);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;

            var runner = Object.FindFirstObjectByType<IngameMvpRunner>();
            Assert.IsNotNull(runner, "SampleScene should include an IngameMvpRunner.");
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
