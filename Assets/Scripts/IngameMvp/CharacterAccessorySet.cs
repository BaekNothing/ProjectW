using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    [Serializable]
    public sealed class CharacterAccessoryDefinition
    {
        public string accessoryId = "accessory.default";
        public Sprite sprite;
        public CharacterPartType attachPartType = CharacterPartType.Head;
        public Vector2 localOffset;
        public int sortingOffset;
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
