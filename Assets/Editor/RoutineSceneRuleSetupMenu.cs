#if UNITY_EDITOR
using ProjectW.IngameMvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectW.EditorTools
{
    public static class RoutineSceneRuleSetupMenu
    {
        [MenuItem("ProjectW/Scene/Setup From Current MVP Scene")]
        public static void SetupFromCurrentMvpScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ProjectW] Stop Play Mode before applying scene setup.");
                return;
            }

            RoutineCharacterAnimatorControllerBuilder.CreateOrUpdateDefaultController();

            var session = Object.FindFirstObjectByType<RoutineObservationMvpSession>();
            if (session == null)
            {
                var go = new GameObject("RoutineMvpSession");
                session = go.AddComponent<RoutineObservationMvpSession>();
            }

            session.SetupSceneFromCurrentLayout();
            session.BakeGeneratedObjectsToScene();
            session.ApplyDepthLayoutInEditor();

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            EditorUtility.SetDirty(session);
            Debug.Log("[ProjectW] Setup from current MVP scene completed.");
        }
    }
}
#endif
