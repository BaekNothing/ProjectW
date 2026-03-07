using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    public sealed class RoutineCharacterPartRig : MonoBehaviour
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

        private readonly Dictionary<CharacterPartType, SpriteRenderer> _renderers = new Dictionary<CharacterPartType, SpriteRenderer>();
        private readonly Dictionary<CharacterPartType, Transform> _anchors = new Dictionary<CharacterPartType, Transform>();
        private Transform _accessoryRoot;

        public SpriteRenderer GetRenderer(CharacterPartType partType)
        {
            EnsureBound();
            _renderers.TryGetValue(partType, out var renderer);
            return renderer;
        }

        public Vector3 GetAccessoryAnchorLocalPosition(CharacterPartType partType)
        {
            EnsureBound();
            return _anchors.TryGetValue(partType, out var anchor) && anchor != null ? anchor.localPosition : Vector3.zero;
        }

        public void EnsureBound()
        {
            if (_renderers.Count > 0)
            {
                return;
            }

            _renderers.Clear();
            _anchors.Clear();

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

                _renderers[pair.Key] = renderer;
                if (pair.Key == CharacterPartType.Accessory)
                {
                    _accessoryRoot = node;
                }
            }

            if (_accessoryRoot == null)
            {
                return;
            }

            RegisterAnchor(CharacterPartType.Head, "HeadAnchor");
            RegisterAnchor(CharacterPartType.Neck, "NeckAnchor");
            RegisterAnchor(CharacterPartType.Torso, "TorsoAnchor");
        }

        public void SetAccessoryLocalPosition(Vector3 localPosition)
        {
            EnsureBound();
            if (_accessoryRoot != null)
            {
                _accessoryRoot.localPosition = localPosition;
            }
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
                _anchors[partType] = anchor;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _renderers.Clear();
            _anchors.Clear();
        }
#endif
    }
}
