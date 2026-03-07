using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    [Serializable]
    public sealed class CharacterPartDefinition
    {
        public CharacterPartType partType = CharacterPartType.Torso;
        public Sprite sprite;
        public int sortingOrder;
        public bool useCustomPivot;
        public Vector2 customPivot = new Vector2(0.5f, 0.5f);
    }

    [CreateAssetMenu(fileName = "CharacterPartSet", menuName = "ProjectW/IngameMvp/Character Part Set")]
    public sealed class CharacterPartSet : ScriptableObject
    {
        [SerializeField] private string partSetId = "character.parts.default";
        [SerializeField] private CharacterPartDefinition[] parts = Array.Empty<CharacterPartDefinition>();

        public string PartSetId => string.IsNullOrWhiteSpace(partSetId) ? name : partSetId.Trim();
        public IReadOnlyList<CharacterPartDefinition> Parts => parts ?? Array.Empty<CharacterPartDefinition>();
    }
}
