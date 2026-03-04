using System;
using ProjectW.Outgame;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectW.IngameMvp
{
    public sealed class IngameFlowBridge : MonoBehaviour
    {
        private const string IngameSceneName = "MVP Scene";
        private const string OutgameSceneName = "Outgame Scene";

        private RoutineObservationMvpSession _session;
        private IngameResultPopupController _popup;
        private bool _sessionStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBridge()
        {
            var scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.name, IngameSceneName, StringComparison.Ordinal))
            {
                return;
            }

            var existing = FindFirstObjectByType<IngameFlowBridge>();
            if (existing != null)
            {
                return;
            }

            var go = new GameObject(nameof(IngameFlowBridge));
            go.AddComponent<IngameFlowBridge>();
        }

        private void Start()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, IngameSceneName, StringComparison.Ordinal))
            {
                return;
            }

            _session = FindFirstObjectByType<RoutineObservationMvpSession>();
            if (_session == null)
            {
                Debug.LogError("[IngameFlowBridge] RoutineObservationMvpSession not found.");
                return;
            }

            _popup = FindFirstObjectByType<IngameResultPopupController>();
            if (_popup == null)
            {
                var popupGo = new GameObject(nameof(IngameResultPopupController));
                _popup = popupGo.AddComponent<IngameResultPopupController>();
            }

            var setup = SessionFlowRuntimeContext.ConsumePendingSetupOrDefault();
            _session.ApplyOutgameSetup(setup);
            _session.SessionEnded += HandleSessionEnded;
            _session.StartSession();
            _sessionStarted = true;
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.SessionEnded -= HandleSessionEnded;
            }
        }

        private void HandleSessionEnded(SessionResultSummary summary)
        {
            SessionFlowRuntimeContext.SetLastResult(summary);
            if (_popup == null)
            {
                ReturnToOutgame();
                return;
            }

            _popup.Show(summary, ReturnToOutgame);
        }

        private void ReturnToOutgame()
        {
            if (!_sessionStarted)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(OutgameSceneName))
            {
                Debug.LogError("[IngameFlowBridge] Cannot load scene: " + OutgameSceneName);
                return;
            }

            SceneManager.LoadScene(OutgameSceneName);
        }
    }
}
