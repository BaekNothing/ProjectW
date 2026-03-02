using System.Linq;
using ProjectW.IngameMvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectW.Editor
{
    [InitializeOnLoad]
    public static class MvpScene2DAutoSetup
    {
        private const string MvpScenePath = "Assets/Scenes/MVP Scene.unity";

        static MvpScene2DAutoSetup()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        [MenuItem("ProjectW/Rebuild MVP Scene (2D Only Asset)")]
        public static void RebuildActiveMvpScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != MvpScenePath)
            {
                Debug.LogWarning("[ProjectW] Active scene is not MVP Scene. Open Assets/Scenes/MVP Scene.unity first.");
                return;
            }

            RebuildScene(scene);
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path != MvpScenePath)
            {
                return;
            }

            RebuildScene(scene);
        }

        private static void RebuildScene(Scene scene)
        {
            var session = Object.FindFirstObjectByType<RoutineObservationMvpSession>();
            if (session == null)
            {
                var go = new GameObject("RoutineMvpSession");
                session = go.AddComponent<RoutineObservationMvpSession>();
            }

            if (!Needs2DRebuild())
            {
                return;
            }

            session.RebuildMvpScene2D();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[ProjectW] MVP Scene rebuilt to 2D-only layout.");
        }

        private static bool Needs2DRebuild()
        {
            var zones = GameObject.Find("Zones");
            var characters = GameObject.Find("Characters");
            if (zones == null || characters == null)
            {
                return true;
            }

            var zoneHas3D = zones.GetComponentsInChildren<MeshRenderer>(true).Any()
                            || zones.GetComponentsInChildren<MeshFilter>(true).Any()
                            || zones.GetComponentsInChildren<BoxCollider>(true).Any();
            var characterHas3D = characters.GetComponentsInChildren<MeshRenderer>(true).Any()
                                 || characters.GetComponentsInChildren<MeshFilter>(true).Any()
                                 || characters.GetComponentsInChildren<Collider>(true).Any();
            return zoneHas3D || characterHas3D;
        }
    }
}
