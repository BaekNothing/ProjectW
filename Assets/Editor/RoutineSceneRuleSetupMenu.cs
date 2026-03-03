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
        [MenuItem("ProjectW/Scene/Apply Declared Object Rules")]
        public static void ApplyDeclaredObjectRules()
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

            session.RebuildMvpScene2D();
            session.BakeGeneratedObjectsToScene();
            session.ApplyDepthLayoutInEditor();

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            EditorUtility.SetDirty(session);
            Debug.Log("[ProjectW] Applied declared object rules to current scene.");
        }
    }
}
#endif
