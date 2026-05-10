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
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var scene = SceneManager.CreateScene("MVP Scene");
            SceneManager.SetActiveScene(scene);
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
        public void Awake_BuildsFourPanelsAndConsoleControls()
        {
            var controller = new GameObject("CaseReviewPlaySceneController").AddComponent<CaseReviewPlaySceneController>();

            Assert.IsNotNull(controller.Session);
            Assert.IsNotNull(GameObject.Find("PublicPanel"));
            Assert.IsNotNull(GameObject.Find("HiddenPanel"));
            Assert.IsNotNull(GameObject.Find("ConsoleHistoryPanel"));
            Assert.IsNotNull(GameObject.Find("ConsolePanel"));
            Assert.IsNotNull(GameObject.Find("ConsoleInput")?.GetComponent<InputField>());
            Assert.IsNotNull(GameObject.Find("SubmitButton")?.GetComponent<Button>());
            Assert.IsNotNull(GameObject.Find("Quick_CONFIRM_PLAN")?.GetComponent<Button>());
        }

        [Test]
        public void SubmitCommand_UpdatesSessionAndConsoleHistory()
        {
            var controller = new GameObject("CaseReviewPlaySceneController").AddComponent<CaseReviewPlaySceneController>();

            controller.SubmitCommand("plan");
            controller.SubmitCommand("confirm plan");
            controller.SubmitCommand("report");
            controller.SubmitCommand("review all");
            controller.SubmitCommand("next day");

            Assert.AreEqual(2, controller.Session.State.Day);
            Assert.That(controller.ConsoleHistory, Does.Contain("> plan"));
            Assert.That(controller.ConsoleHistory, Does.Contain("> confirm plan"));
            Assert.That(controller.ConsoleHistory, Does.Contain("> next day"));
            Assert.IsNotEmpty(GameObject.Find("PublicText")?.GetComponent<Text>()?.text);
            Assert.IsNotEmpty(GameObject.Find("HiddenText")?.GetComponent<Text>()?.text);
            Assert.IsNotEmpty(GameObject.Find("ConsoleHistoryText")?.GetComponent<Text>()?.text);
        }
    }
}
