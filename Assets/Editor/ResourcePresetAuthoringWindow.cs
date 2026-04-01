using System;
using System.Collections.Generic;
using System.IO;
using ProjectW.IngameMvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectW.Editor
{
    public sealed class ResourcePresetAuthoringWindow : EditorWindow
    {
        private const string PresetRootPath = "Assets/Data/ResourcePresets";

        [Serializable]
        public struct ResourceObjectSnapshot
        {
            public string hierarchyPath;
            public string objectName;
            public string tag;
            public int layer;
            public bool active;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        private readonly List<ResourceObjectSnapshot> _workingSnapshots = new List<ResourceObjectSnapshot>();
        private ResourcePlacementPreset _selectedPreset;
        private Vector2 _scroll;

        [MenuItem("ProjectW/Tools/Resource Preset Authoring")]
        public static void Open()
        {
            var window = GetWindow<ResourcePresetAuthoringWindow>("Resource Preset Authoring");
            window.minSize = new Vector2(500f, 380f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Resource Preset Authoring", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"Preset assets are saved under '{PresetRootPath}'.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Scene", GUILayout.Height(30f)))
                {
                    ScanScene();
                }

                if (GUILayout.Button("Save Preset Asset", GUILayout.Height(30f)))
                {
                    SavePresetAsset();
                }

                if (GUILayout.Button("Load Preset Asset", GUILayout.Height(30f)))
                {
                    LoadPresetAsset();
                }
            }

            _selectedPreset = (ResourcePlacementPreset)EditorGUILayout.ObjectField(
                "Loaded Preset",
                _selectedPreset,
                typeof(ResourcePlacementPreset),
                false);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField($"Scanned Objects: {_workingSnapshots.Count}", EditorStyles.miniBoldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < _workingSnapshots.Count; i++)
            {
                var snapshot = _workingSnapshots[i];
                EditorGUILayout.LabelField($"{i + 1}. {snapshot.hierarchyPath}");
            }
            EditorGUILayout.EndScrollView();
        }

        private void ScanScene()
        {
            _workingSnapshots.Clear();

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[ProjectW] No active loaded scene to scan.");
                return;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                CollectRecursive(root.transform, root.name);
            }

            Debug.Log($"[ProjectW] Scene scan complete. Captured {_workingSnapshots.Count} objects.");
            Repaint();
        }

        private static void EnsurePresetDirectory()
        {
            if (AssetDatabase.IsValidFolder(PresetRootPath))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            AssetDatabase.CreateFolder("Assets/Data", "ResourcePresets");
        }

        private void SavePresetAsset()
        {
            if (_workingSnapshots.Count == 0)
            {
                Debug.LogWarning("[ProjectW] Scan the scene before saving a preset asset.");
                return;
            }

            EnsurePresetDirectory();

            var defaultFileName = $"ResourcePreset_{DateTime.Now:yyyyMMdd_HHmmss}.asset";
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Resource Preset",
                defaultFileName,
                "asset",
                "Select where to save the resource preset asset.",
                PresetRootPath);

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!path.StartsWith(PresetRootPath, StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("Invalid Save Path", $"Save path must be under '{PresetRootPath}'.", "OK");
                return;
            }

            var asset = CreateInstance<ResourcePlacementPreset>();
            asset.SetData(ConvertToPlacements(_workingSnapshots), new ResourcePlacementMeta
            {
                PresetId = Path.GetFileNameWithoutExtension(path),
                DisplayName = Path.GetFileNameWithoutExtension(path),
                SourceScene = SceneManager.GetActiveScene().name,
                AuthoredAtUtcIso8601 = DateTime.UtcNow.ToString("o")
            });
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _selectedPreset = asset;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[ProjectW] Resource preset saved: {path}");
        }

        private void LoadPresetAsset()
        {
            EnsurePresetDirectory();

            var path = EditorUtility.OpenFilePanel("Load Resource Preset", PresetRootPath, "asset");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var projectPath = FileUtil.GetProjectRelativePath(path);
            if (string.IsNullOrWhiteSpace(projectPath) || !projectPath.StartsWith(PresetRootPath, StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("Invalid Load Path", $"Preset must be loaded from '{PresetRootPath}'.", "OK");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<ResourcePlacementPreset>(projectPath);
            if (asset == null)
            {
                Debug.LogWarning($"[ProjectW] Failed to load preset at path: {projectPath}");
                return;
            }

            _selectedPreset = asset;
            ApplyPreset(asset);
            Repaint();

            Debug.Log($"[ProjectW] Resource preset loaded: {projectPath}");
        }

        private void ApplyPreset(ResourcePlacementPreset preset)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[ProjectW] No active loaded scene to apply preset.");
                return;
            }

            var snapshots = ConvertToSnapshots(preset.Placements);
            foreach (var snapshot in snapshots)
            {
                var target = FindByHierarchyPath(snapshot.hierarchyPath);
                if (target == null)
                {
                    var created = new GameObject(snapshot.objectName);
                    created.tag = snapshot.tag;
                    created.layer = snapshot.layer;
                    target = created.transform;
                    AttachToParentFromPath(target, snapshot.hierarchyPath);
                }

                target.name = snapshot.objectName;
                target.gameObject.tag = snapshot.tag;
                target.gameObject.layer = snapshot.layer;
                target.gameObject.SetActive(snapshot.active);
                target.localPosition = snapshot.localPosition;
                target.localRotation = snapshot.localRotation;
                target.localScale = snapshot.localScale;
            }

            _workingSnapshots.Clear();
            _workingSnapshots.AddRange(snapshots);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private void CollectRecursive(Transform transform, string path)
        {
            _workingSnapshots.Add(new ResourceObjectSnapshot
            {
                hierarchyPath = path,
                objectName = transform.name,
                tag = transform.gameObject.tag,
                layer = transform.gameObject.layer,
                active = transform.gameObject.activeSelf,
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = transform.localScale
            });

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                CollectRecursive(child, $"{path}/{child.name}");
            }
        }

        private static List<ResourcePlacement> ConvertToPlacements(List<ResourceObjectSnapshot> snapshots)
        {
            var placements = new List<ResourcePlacement>(snapshots.Count);
            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                placements.Add(new ResourcePlacement
                {
                    Type = InferPlacementType(snapshot),
                    PlacementId = snapshot.hierarchyPath,
                    ObjectName = snapshot.objectName,
                    ParentPath = ResolveParentPath(snapshot.hierarchyPath),
                    LocalPosition = snapshot.localPosition,
                    LocalRotation = snapshot.localRotation,
                    LocalScale = snapshot.localScale,
                    Active = snapshot.active,
                    Tag = snapshot.tag,
                    Layer = snapshot.layer
                });
            }

            return placements;
        }

        private static List<ResourceObjectSnapshot> ConvertToSnapshots(IReadOnlyList<ResourcePlacement> placements)
        {
            var snapshots = new List<ResourceObjectSnapshot>(placements.Count);
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                snapshots.Add(new ResourceObjectSnapshot
                {
                    hierarchyPath = string.IsNullOrWhiteSpace(placement.PlacementId)
                        ? BuildHierarchyPath(placement.ParentPath, placement.ObjectName)
                        : placement.PlacementId,
                    objectName = placement.ObjectName,
                    tag = string.IsNullOrWhiteSpace(placement.Tag) ? "Untagged" : placement.Tag,
                    layer = placement.Layer,
                    active = placement.Active,
                    localPosition = placement.LocalPosition,
                    localRotation = placement.LocalRotation,
                    localScale = placement.LocalScale
                });
            }

            return snapshots;
        }

        private static ResourcePlacementType InferPlacementType(ResourceObjectSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.tag) && snapshot.tag.IndexOf("zone", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResourcePlacementType.Zone;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.objectName) && snapshot.objectName.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResourcePlacementType.Character;
            }

            return ResourcePlacementType.Generic;
        }

        private static string ResolveParentPath(string hierarchyPath)
        {
            if (string.IsNullOrWhiteSpace(hierarchyPath))
            {
                return string.Empty;
            }

            var splitIndex = hierarchyPath.LastIndexOf('/');
            return splitIndex <= 0 ? string.Empty : hierarchyPath.Substring(0, splitIndex);
        }

        private static string BuildHierarchyPath(string parentPath, string objectName)
        {
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                return objectName;
            }

            return $"{parentPath}/{objectName}";
        }

        private static Transform FindByHierarchyPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var parts = path.Split('/');
            if (parts.Length == 0)
            {
                return null;
            }

            var current = GameObject.Find(parts[0])?.transform;
            for (var i = 1; i < parts.Length && current != null; i++)
            {
                current = current.Find(parts[i]);
            }

            return current;
        }

        private static void AttachToParentFromPath(Transform child, string fullPath)
        {
            var slashIndex = fullPath.LastIndexOf('/');
            if (slashIndex <= 0)
            {
                return;
            }

            var parentPath = fullPath.Substring(0, slashIndex);
            var parent = FindByHierarchyPath(parentPath);
            if (parent != null)
            {
                child.SetParent(parent, false);
            }
        }
    }
}
