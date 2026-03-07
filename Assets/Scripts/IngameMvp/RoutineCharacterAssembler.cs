using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    public sealed class RoutineCharacterAssembler : MonoBehaviour
    {
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

        private readonly Dictionary<CharacterPartType, SpriteRenderer> _partRenderers = new Dictionary<CharacterPartType, SpriteRenderer>();
        private readonly Dictionary<CharacterPartType, Transform> _partAnchors = new Dictionary<CharacterPartType, Transform>();
        private readonly Dictionary<string, GameObject> _spawnedAccessories = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private CharacterAccessorySet _accessorySet;
        private Transform _accessoryRoot;

        public void EnsureBound()
        {
            if (_partRenderers.Count > 0)
            {
                return;
            }

            _partRenderers.Clear();
            _partAnchors.Clear();
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
                if (pair.Key == CharacterPartType.Accessory)
                {
                    _accessoryRoot = node;
                }
            }

            RegisterAnchor(CharacterPartType.Head, "HeadAnchor");
            RegisterAnchor(CharacterPartType.Eyes, "EyesAnchor");
            RegisterAnchor(CharacterPartType.Neck, "NeckAnchor");
            RegisterAnchor(CharacterPartType.Torso, "TorsoAnchor");
            RegisterAnchor(CharacterPartType.ArmL, "ArmLAnchor");
            RegisterAnchor(CharacterPartType.ArmR, "ArmRAnchor");
            RegisterAnchor(CharacterPartType.LegL, "LegLAnchor");
            RegisterAnchor(CharacterPartType.LegR, "LegRAnchor");
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

            DetachAccessory(definition.accessoryId);

            var parent = ResolveAccessoryParent(definition.attachPartType);
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
            renderer.sortingOrder = 30 + definition.sortingOffset;
            renderer.color = Color.white;

            _spawnedAccessories[definition.accessoryId] = go;
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
                return false;
            }

            _spawnedAccessories.Remove(key);
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

        private Transform ResolveAccessoryParent(CharacterPartType partType)
        {
            if (_partAnchors.TryGetValue(partType, out var anchor) && anchor != null)
            {
                return anchor;
            }

            return _accessoryRoot != null ? _accessoryRoot : transform;
        }

        private void RegisterAnchor(CharacterPartType partType, string anchorName)
        {
            if (_accessoryRoot == null)
            {
                return;
            }

            var anchor = _accessoryRoot.Find(anchorName);
            if (anchor != null)
            {
                _partAnchors[partType] = anchor;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _partRenderers.Clear();
            _partAnchors.Clear();
        }
#endif
    }
}
