using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    public sealed class RoutineCharacterAssembler : MonoBehaviour
    {
        public enum ExclusiveGroupConflictPolicy
        {
            ReplaceExisting = 0,
            RejectNew = 1
        }

        private const string AccessoryRootName = "AccessoryRoot";

        private static readonly Dictionary<CharacterPartType, string> PartNodeNames = new Dictionary<CharacterPartType, string>
        {
            { CharacterPartType.Head, "Head" },
            { CharacterPartType.Eyes, "Eyes" },
            { CharacterPartType.Neck, "Neck" },
            { CharacterPartType.Torso, "Torso" },
            { CharacterPartType.ArmL, "ArmL" },
            { CharacterPartType.ArmR, "ArmR" },
            { CharacterPartType.LegL, "LegL" },
            { CharacterPartType.LegR, "LegR" },
            { CharacterPartType.Accessory, AccessoryRootName }
        };

        [SerializeField] private ExclusiveGroupConflictPolicy exclusiveGroupConflictPolicy = ExclusiveGroupConflictPolicy.ReplaceExisting;

        private readonly Dictionary<CharacterPartType, SpriteRenderer> _partRenderers = new Dictionary<CharacterPartType, SpriteRenderer>();
        private readonly Dictionary<CharacterPartType, Transform> _partNodes = new Dictionary<CharacterPartType, Transform>();
        private readonly Dictionary<string, GameObject> _spawnedAccessories = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _equippedAccessoryGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private CharacterAccessorySet _accessorySet;
        private Transform _accessoryRoot;

        public void EnsureBound()
        {
            if (_partRenderers.Count > 0)
            {
                return;
            }

            _partRenderers.Clear();
            _partNodes.Clear();
            _accessoryRoot = null;

            foreach (var pair in PartNodeNames)
            {
                var node = transform.Find(pair.Value);
                if (node == null)
                {
                    continue;
                }

                var renderer = node.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    renderer = node.gameObject.AddComponent<SpriteRenderer>();
                }

                _partRenderers[pair.Key] = renderer;
                _partNodes[pair.Key] = node;
                if (pair.Key == CharacterPartType.Accessory)
                {
                    _accessoryRoot = node;
                }
            }
        }

        public SpriteRenderer GetRenderer(CharacterPartType partType)
        {
            EnsureBound();
            _partRenderers.TryGetValue(partType, out var renderer);
            return renderer;
        }

        public void ApplyPartSet(CharacterPartSet set)
        {
            EnsureBound();
            if (set == null)
            {
                return;
            }

            var definitions = set.Parts;
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (!_partRenderers.TryGetValue(definition.partType, out var renderer) || renderer == null)
                {
                    continue;
                }

                if (definition.sprite != null)
                {
                    renderer.sprite = definition.sprite;
                }

                renderer.sortingOrder = definition.sortingOrder;
            }
        }

        public void SetAccessorySet(CharacterAccessorySet set)
        {
            _accessorySet = set;
        }

        public GameObject AttachAccessory(string accessoryId)
        {
            EnsureBound();

            if (string.IsNullOrWhiteSpace(accessoryId) || _accessorySet == null)
            {
                return null;
            }

            var definition = FindAccessoryDefinition(accessoryId.Trim());
            if (definition == null)
            {
                return null;
            }

            if (!TryResolveExclusiveGroup(definition))
            {
                Debug.LogWarning($"[Accessory] 장착 거부: '{definition.accessoryId}' 그룹 '{definition.exclusiveGroup}' 충돌 (정책: {exclusiveGroupConflictPolicy}).", this);
                return null;
            }

            DetachAccessory(definition.accessoryId);

            var parent = ResolveAccessoryParent(definition.targetPart, definition.anchorName);
            if (parent == null)
            {
                return null;
            }

            var go = new GameObject($"Accessory_{definition.accessoryId}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = definition.localOffset;
            go.transform.localScale = Vector3.one;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = definition.sprite;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = ResolveAccessorySortingOrder(definition.targetPart, definition.orderOffset);
            renderer.color = Color.white;

            _spawnedAccessories[definition.accessoryId] = go;
            RegisterAccessoryGroup(definition);
            return go;
        }

        public bool DetachAccessory(string accessoryId)
        {
            if (string.IsNullOrWhiteSpace(accessoryId))
            {
                return false;
            }

            var key = accessoryId.Trim();
            if (!_spawnedAccessories.TryGetValue(key, out var accessory) || accessory == null)
            {
                _spawnedAccessories.Remove(key);
                _equippedAccessoryGroups.Remove(key);
                return false;
            }

            _spawnedAccessories.Remove(key);
            _equippedAccessoryGroups.Remove(key);
            if (Application.isPlaying)
            {
                Destroy(accessory);
            }
            else
            {
                DestroyImmediate(accessory);
            }

            return true;
        }

        public void DetachAccessory()
        {
            var keys = new List<string>(_spawnedAccessories.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                DetachAccessory(keys[i]);
            }
        }

        private CharacterAccessoryDefinition FindAccessoryDefinition(string accessoryId)
        {
            var accessories = _accessorySet.Accessories;
            for (int i = 0; i < accessories.Count; i++)
            {
                var candidate = accessories[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.accessoryId))
                {
                    continue;
                }

                if (candidate.accessoryId.Equals(accessoryId, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private Transform ResolveAccessoryParent(CharacterPartType targetPart, string anchorName)
        {
            var partRoot = ResolvePartRoot(targetPart);
            if (partRoot == null)
            {
                return _accessoryRoot != null ? _accessoryRoot : transform;
            }

            if (!string.IsNullOrWhiteSpace(anchorName))
            {
                var explicitAnchor = partRoot.Find(anchorName.Trim());
                if (explicitAnchor != null)
                {
                    return explicitAnchor;
                }
            }

            var defaultAnchor = partRoot.Find($"{targetPart}Anchor");
            if (defaultAnchor != null)
            {
                return defaultAnchor;
            }

            return partRoot;
        }

        private Transform ResolvePartRoot(CharacterPartType targetPart)
        {
            if (_partNodes.TryGetValue(targetPart, out var partRoot) && partRoot != null)
            {
                return partRoot;
            }

            return null;
        }

        private int ResolveAccessorySortingOrder(CharacterPartType targetPart, int orderOffset)
        {
            if (_partRenderers.TryGetValue(targetPart, out var renderer) && renderer != null)
            {
                return renderer.sortingOrder + orderOffset;
            }

            return orderOffset;
        }

        private bool TryResolveExclusiveGroup(CharacterAccessoryDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.exclusiveGroup))
            {
                return true;
            }

            var groupName = definition.exclusiveGroup.Trim();
            string conflictingAccessoryId = null;
            foreach (var pair in _equippedAccessoryGroups)
            {
                if (pair.Value.Equals(groupName, StringComparison.OrdinalIgnoreCase) &&
                    !pair.Key.Equals(definition.accessoryId, StringComparison.OrdinalIgnoreCase))
                {
                    conflictingAccessoryId = pair.Key;
                    break;
                }
            }

            if (string.IsNullOrEmpty(conflictingAccessoryId))
            {
                return true;
            }

            if (exclusiveGroupConflictPolicy == ExclusiveGroupConflictPolicy.RejectNew)
            {
                return false;
            }

            var detached = DetachAccessory(conflictingAccessoryId);
            if (detached)
            {
                Debug.Log($"[Accessory] 그룹 충돌 교체: '{groupName}' 기존 '{conflictingAccessoryId}' 해제 후 '{definition.accessoryId}' 장착.", this);
            }

            return true;
        }

        private void RegisterAccessoryGroup(CharacterAccessoryDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.accessoryId))
            {
                return;
            }

            var id = definition.accessoryId.Trim();
            if (string.IsNullOrWhiteSpace(definition.exclusiveGroup))
            {
                _equippedAccessoryGroups.Remove(id);
                return;
            }

            _equippedAccessoryGroups[id] = definition.exclusiveGroup.Trim();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _partRenderers.Clear();
            _partNodes.Clear();
        }
#endif
    }
}
