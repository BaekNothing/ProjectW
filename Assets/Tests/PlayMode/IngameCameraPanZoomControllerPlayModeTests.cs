using System.Collections;
using NUnit.Framework;
using ProjectW.IngameMvp;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectW.Tests.PlayMode
{
    public class IngameCameraPanZoomControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator RoutineSessionAwake_AttachesControllerWithinOneFrame()
        {
            var tempScene = SceneManager.CreateScene("CameraPanZoom_FallbackAttach");
            SceneManager.SetActiveScene(tempScene);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;

            Assert.IsNull(camera.GetComponent<IngameCameraPanZoomController>());

            var sessionGo = new GameObject("RoutineSession_FallbackAttach");
            sessionGo.AddComponent<RoutineObservationMvpSession>();

            yield return null;

            var controller = camera.GetComponent<IngameCameraPanZoomController>();
            Assert.IsNotNull(controller, "RoutineObservationMvpSession.Awake must ensure pan/zoom controller attachment.");

            Object.Destroy(sessionGo);
            Object.Destroy(cameraGo);
        }

        [UnityTest]
        public IEnumerator RoutineSessionAwake_LogsWarning_WhenNoCameraExists()
        {
            var tempScene = SceneManager.CreateScene("CameraPanZoom_NoCamera");
            SceneManager.SetActiveScene(tempScene);

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                {
                    Object.Destroy(cameras[i].gameObject);
                }
            }

            yield return null;

            LogAssert.Expect(LogType.Warning, "[RoutineMVP] No camera found in Awake. Camera pan/zoom controller fallback attach skipped.");

            var sessionGo = new GameObject("RoutineSession_NoCamera");
            sessionGo.AddComponent<RoutineObservationMvpSession>();

            yield return null;

            Object.Destroy(sessionGo);
        }

    }
}
