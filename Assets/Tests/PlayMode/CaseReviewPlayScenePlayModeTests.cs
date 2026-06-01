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
        public IEnumerator MvpScene_LoadsManagementOffice()
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
            Assert.IsNotNull(GameObject.Find("GlobalStatusBar"));
            Assert.IsNotNull(GameObject.Find("OfficeNavigationPanel"));
            Assert.IsNotNull(GameObject.Find("OfficeWorkPanel"));
            Assert.IsNotNull(GameObject.Find("OfficeSignalPanel"));
            Assert.IsNotNull(GameObject.Find("TurnProgressPanel"));
            Assert.IsNotNull(GameObject.Find("OfficeActionPanel"));
            Assert.IsNotNull(GameObject.Find("CommandLogPanel"));
            Assert.IsNotNull(GameObject.Find("DebugCommandPanel"));
            Assert.IsNotEmpty(GameObject.Find("GlobalStatusText")?.GetComponent<Text>()?.text);
            Assert.IsNotEmpty(GameObject.Find("OfficeWorkText")?.GetComponent<Text>()?.text);
            Assert.IsNotEmpty(GameObject.Find("OfficeSignalText")?.GetComponent<Text>()?.text);
            Assert.IsNotEmpty(GameObject.Find("ConsoleHistoryText")?.GetComponent<Text>()?.text);
            Assert.IsNull(GameObject.Find("HiddenPanel"));
        }
    }
}
