using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    [Serializable]
    public sealed class ResourcePlacementMeta
    {
        public string PresetId = "default";
        public string DisplayName = "Default";
        public string SourceScene = string.Empty;
        public string AuthoredAtUtcIso8601 = string.Empty;
    }

    public enum ResourcePlacementType
    {
        Generic = 0,
        Zone = 1,
        Character = 2
    }

    [Serializable]
    public sealed class ResourcePlacement
    {
        public ResourcePlacementType Type = ResourcePlacementType.Generic;
        public string PlacementId = string.Empty;
        public string ObjectName = string.Empty;
        public string ParentPath = string.Empty;
        public Vector3 LocalPosition = Vector3.zero;
        public Quaternion LocalRotation = Quaternion.identity;
        public Vector3 LocalScale = Vector3.one;
        public bool Active = true;
        public string Tag = "Untagged";
        public int Layer = 0;
        public string ZoneId = string.Empty;
        public string[] ZoneTags = Array.Empty<string>();
    }

    [CreateAssetMenu(
        fileName = "ResourcePlacementPreset",
        menuName = "ProjectW/Ingame/Resource Placement Preset")]
    public sealed class ResourcePlacementPreset : ScriptableObject
    {
        [SerializeField] private List<ResourcePlacement> placements = new List<ResourcePlacement>();
        [SerializeField] private ResourcePlacementMeta meta = new ResourcePlacementMeta();

        public IReadOnlyList<ResourcePlacement> Placements => placements;
        public ResourcePlacementMeta Meta => meta;

        public void SetData(List<ResourcePlacement> sourcePlacements, ResourcePlacementMeta sourceMeta)
        {
            placements = sourcePlacements ?? new List<ResourcePlacement>();
            meta = sourceMeta ?? new ResourcePlacementMeta();
        }
    }
}
