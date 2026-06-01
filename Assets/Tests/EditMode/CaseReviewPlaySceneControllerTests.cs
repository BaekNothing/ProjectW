using NUnit.Framework;
using ProjectW.IngameMvp;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectW.Tests.EditMode
{
    public sealed class CaseReviewPlaySceneControllerTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MVP Scene.unity", OpenSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Awake_BuildsManagementOfficePanelsAndControls()
        {
            var controller = new GameObject("CaseReviewPlaySceneController").AddComponent<CaseReviewPlaySceneController>();
            controller.InitializeForTests();

            Assert.IsNotNull(controller.Session);
            Assert.IsNotNull(GameObject.Find("GlobalStatusBar"));
            Assert.IsNotNull(GameObject.Find("OfficeNavigationPanel"));
            Assert.IsNotNull(GameObject.Find("OfficeWorkPanel"));
            Assert.IsNotNull(GameObject.Find("OfficeSignalPanel"));
            Assert.IsNotNull(GameObject.Find("TurnProgressPanel"));
            Assert.IsNotNull(GameObject.Find("OfficeActionPanel"));
            Assert.IsNotNull(GameObject.Find("CommandLogPanel"));
            Assert.IsNotNull(GameObject.Find("DebugCommandPanel"));
            Assert.IsNotNull(GameObject.Find("ConsoleInput")?.GetComponent<InputField>());
            Assert.IsNotNull(GameObject.Find("SubmitButton")?.GetComponent<Button>());
            Assert.IsNotNull(GameObject.Find("Nav_TaskBoard")?.GetComponent<Button>());
            Assert.IsNotNull(GameObject.Find("Nav_Lab")?.GetComponent<Button>());
            Assert.IsNotNull(GameObject.Find("Action_CONFIRM_PLAN")?.GetComponent<Button>());
            Assert.IsNotNull(GameObject.Find("Action_SELECT_NEXT_TASK")?.GetComponent<Button>());
            Assert.IsNotNull(GameObject.Find("Action_LAB_ADD_TASK")?.GetComponent<Button>());
        }

        [Test]
        public void SubmitCommand_UpdatesSessionAndConsoleHistory()
        {
            var controller = new GameObject("CaseReviewPlaySceneController").AddComponent<CaseReviewPlaySceneController>();
            controller.InitializeForTests();

            controller.SubmitCommand("plan");
            controller.SubmitCommand("confirm plan");
            controller.SubmitCommand("report");
            controller.SubmitCommand("review all");
            controller.SubmitCommand("next day");

            Assert.AreEqual(2, controller.Session.State.Day);
            Assert.That(controller.ConsoleHistory, Does.Contain("> plan"));
            Assert.That(controller.ConsoleHistory, Does.Contain("> confirm plan"));
            Assert.That(controller.ConsoleHistory, Does.Contain("> next day"));
            Assert.IsNotEmpty(GameObject.Find("GlobalStatusText")?.GetComponent<Text>()?.text);
            Assert.IsNotEmpty(GameObject.Find("OfficeWorkText")?.GetComponent<Text>()?.text);
            Assert.IsNotEmpty(GameObject.Find("OfficeSignalText")?.GetComponent<Text>()?.text);
            Assert.IsNotEmpty(GameObject.Find("ConsoleHistoryText")?.GetComponent<Text>()?.text);
        }

        [Test]
        public void Awake_DoesNotExposeTruthDebugPanelInManagementOffice()
        {
            new GameObject("CaseReviewPlaySceneController").AddComponent<CaseReviewPlaySceneController>().InitializeForTests();

            Assert.IsNull(GameObject.Find("HiddenPanel"));
            Assert.IsNull(GameObject.Find("HiddenText"));
            Assert.That(GameObject.Find("OfficeWorkText")?.GetComponent<Text>()?.text, Does.Not.Contain("TruthFrames"));
        }

        [Test]
        public void ToggleCycleGuide_ShowsOneCycleInstructions()
        {
            var controller = new GameObject("CaseReviewPlaySceneController").AddComponent<CaseReviewPlaySceneController>();
            controller.InitializeForTests();

            Assert.IsFalse(controller.IsCycleGuideVisible);

            controller.ToggleCycleGuide();

            Assert.IsTrue(controller.IsCycleGuideVisible);
            var guideText = GameObject.Find("CycleGuideText")?.GetComponent<Text>()?.text;
            Assert.That(guideText, Does.Contain("한 사이클 권장 진행"));
            Assert.That(guideText, Does.Contain("실험실에서 상태를 흔듭니다"));
            Assert.That(guideText, Does.Contain("` : 이 설명서 열기/닫기"));

            controller.ToggleCycleGuide();

            Assert.IsFalse(controller.IsCycleGuideVisible);
        }
    }
}
