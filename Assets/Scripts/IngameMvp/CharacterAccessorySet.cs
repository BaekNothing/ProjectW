using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectW.IngameMvp
{
    [Serializable]
    public sealed class CharacterAccessoryDefinition
    {
        public string accessoryId = "accessory.default";
        public Sprite sprite;

        [FormerlySerializedAs("attachPartType")]
        public CharacterPartType targetPart = CharacterPartType.Head;

        public string anchorName = string.Empty;
        public Vector2 localOffset;

        [FormerlySerializedAs("sortingOffset")]
        public int orderOffset = 1;

        [Tooltip("같은 그룹은 동시에 하나만 장착 가능합니다. 예: hat")]
        public string exclusiveGroup = string.Empty;
    }

    [CreateAssetMenu(fileName = "CharacterAccessorySet", menuName = "ProjectW/IngameMvp/Character Accessory Set")]
    public sealed class CharacterAccessorySet : ScriptableObject
    {
        [SerializeField] private string accessorySetId = "character.accessories.default";
        [SerializeField] private CharacterAccessoryDefinition[] accessories = Array.Empty<CharacterAccessoryDefinition>();

        public string AccessorySetId => string.IsNullOrWhiteSpace(accessorySetId) ? name : accessorySetId.Trim();
        public IReadOnlyList<CharacterAccessoryDefinition> Accessories => accessories ?? Array.Empty<CharacterAccessoryDefinition>();
    }
}
