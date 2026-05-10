using System.Collections;
using NUnit.Framework;
using ProjectW.IngameMvp;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ProjectW.Tests.PlayMode
{
    public sealed class CaseReviewPlayScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator MvpScene_LoadsCaseReviewConsole()
        {
            var load = SceneManager.LoadSceneAsync("MVP Scene", LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;

            var controller = Object.FindFirstObjectByType<CaseReviewPlaySceneController>();
            var session = Object.FindFirstObjectByType<CaseReviewSessionController>();

            Assert.IsNotNull(controller);
            Assert.IsNotNull(session);
            Assert.IsTrue(session.IsInitialized);
            Assert.IsNotNull(GameObject.Find("PublicPanel"));
            Assert.IsNotNull(GameObject.Find("HiddenPanel"));
            Assert.IsNotNull(GameObject.Find("ConsoleHistoryPanel"));
            Assert.IsNotNull(GameObject.Find("ConsolePanel"));
            Assert.IsNotEmpty(GameObject.Find("PublicText")?.GetComponent<Text>()?.text);
            Assert.IsNotEmpty(GameObject.Find("HiddenText")?.GetComponent<Text>()?.text);
            Assert.IsNotEmpty(GameObject.Find("ConsoleHistoryText")?.GetComponent<Text>()?.text);
        }
    }
}
