using NUnit.Framework;
using ProjectW.IngameMvp;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectW.Tests.EditMode
{
    public class IngameCameraPanZoomControllerEditModeTests
    {
        private const string MvpScenePath = "Assets/Scenes/MVP Scene.unity";

        [Test]
        public void MvpScene_MainCamera_HasController_And_EnsureAttachedTo_IsIdempotent()
        {
            var previousScene = SceneManager.GetActiveScene();
            var previousPath = previousScene.path;

            try
            {
                var scene = EditorSceneManager.OpenScene(MvpScenePath, OpenSceneMode.Single);
                Assert.IsTrue(scene.IsValid(), "MVP scene failed to open.");

                var mainCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
                Assert.IsNotNull(mainCamera, "Main camera is missing in MVP scene.");

                var existing = mainCamera.GetComponent<IngameCameraPanZoomController>();
                Assert.IsNotNull(existing, "IngameCameraPanZoomController must be serialized on Main Camera.");

                var ensured = IngameCameraPanZoomController.EnsureAttachedTo(mainCamera);
                Assert.IsNotNull(ensured);
                Assert.AreSame(existing, ensured);

                var allControllers = mainCamera.GetComponents<IngameCameraPanZoomController>();
                Assert.AreEqual(1, allControllers.Length, "EnsureAttachedTo must not add duplicate controllers.");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousPath))
                {
                    EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
                }
            }
        }
    }
}
