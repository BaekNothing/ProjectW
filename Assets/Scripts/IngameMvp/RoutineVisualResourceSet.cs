using System;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    [Serializable]
    public sealed class RoutineVisualResourceSet
    {
        [Header("Character Sprites")]
        public Sprite characterA;
        public Sprite characterB;
        public Sprite characterC;

        [Header("Character Part Sets")]
        public CharacterPartSet characterPartSetA;
        public CharacterPartSet characterPartSetB;
        public CharacterPartSet characterPartSetC;

        [Header("Character Accessory Set")]
        public CharacterAccessorySet characterAccessorySet;

        [Header("Zone Sprites")]
        public Sprite zoneMission;
        public Sprite zoneCafeteria;
        public Sprite zoneSleep;

        [Header("Zone Sprite Animation Frames (Optional)")]
        public Sprite[] zoneMissionFrames;
        public Sprite[] zoneCafeteriaFrames;
        public Sprite[] zoneSleepFrames;

        [Header("Item Tag Sprites")]
        public Sprite itemDesk;
        public Sprite itemComputer;
        public Sprite itemBed;
        public Sprite itemPillow;
        public Sprite itemBlanket;
        public Sprite itemTable;
        public Sprite itemTray;
        public Sprite itemCup;

        public void EnsureLoadedFromResources()
        {
            characterA = LoadIfMissing(characterA, "PlaceholderSprites/character_a");
            characterB = LoadIfMissing(characterB, "PlaceholderSprites/character_b");
            characterC = LoadIfMissing(characterC, "PlaceholderSprites/character_c");

            zoneMission = LoadIfMissing(zoneMission, "PlaceholderSprites/zone_mission");
            zoneCafeteria = LoadIfMissing(zoneCafeteria, "PlaceholderSprites/zone_cafeteria");
            zoneSleep = LoadIfMissing(zoneSleep, "PlaceholderSprites/zone_sleep");

            itemDesk = LoadIfMissing(itemDesk, "PlaceholderSprites/item_desk");
            itemComputer = LoadIfMissing(itemComputer, "PlaceholderSprites/item_computer");
            itemBed = LoadIfMissing(itemBed, "PlaceholderSprites/item_bed");
            itemPillow = LoadIfMissing(itemPillow, "PlaceholderSprites/item_pillow");
            itemBlanket = LoadIfMissing(itemBlanket, "PlaceholderSprites/item_blanket");
            itemTable = LoadIfMissing(itemTable, "PlaceholderSprites/item_table");
            itemTray = LoadIfMissing(itemTray, "PlaceholderSprites/item_tray");
            itemCup = LoadIfMissing(itemCup, "PlaceholderSprites/item_cup");
        }

        public Sprite ResolveCharacterSprite(string actorName, int index)
        {
            if (!string.IsNullOrWhiteSpace(actorName))
            {
                if (actorName.IndexOf("_A", StringComparison.OrdinalIgnoreCase) >= 0) return characterA;
                if (actorName.IndexOf("_B", StringComparison.OrdinalIgnoreCase) >= 0) return characterB;
                if (actorName.IndexOf("_C", StringComparison.OrdinalIgnoreCase) >= 0) return characterC;
            }

            if (index == 0) return characterA;
            if (index == 1) return characterB;
            if (index == 2) return characterC;
            return characterA;
        }

        public CharacterPartSet ResolveCharacterPartSet(string actorName, int index)
        {
            if (!string.IsNullOrWhiteSpace(actorName))
            {
                if (actorName.IndexOf("_A", StringComparison.OrdinalIgnoreCase) >= 0) return characterPartSetA;
                if (actorName.IndexOf("_B", StringComparison.OrdinalIgnoreCase) >= 0) return characterPartSetB;
                if (actorName.IndexOf("_C", StringComparison.OrdinalIgnoreCase) >= 0) return characterPartSetC;
            }

            if (index == 0) return characterPartSetA;
            if (index == 1) return characterPartSetB;
            if (index == 2) return characterPartSetC;
            return characterPartSetA;
        }

        public Sprite ResolveZoneSprite(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return zoneMission;
            }

            if (zoneId.IndexOf("cafeteria", StringComparison.OrdinalIgnoreCase) >= 0
                || zoneId.IndexOf("eat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return zoneCafeteria;
            }

            if (zoneId.IndexOf("sleep", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return zoneSleep;
            }

            return zoneMission;
        }

        public Sprite[] ResolveZoneAnimationFrames(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return zoneMissionFrames;
            }

            if (zoneId.IndexOf("cafeteria", StringComparison.OrdinalIgnoreCase) >= 0
                || zoneId.IndexOf("eat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return zoneCafeteriaFrames;
            }

            if (zoneId.IndexOf("sleep", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return zoneSleepFrames;
            }

            return zoneMissionFrames;
        }

        public Sprite ResolveItemSprite(string itemTag)
        {
            EnsureLoadedFromResources();

            if (string.IsNullOrWhiteSpace(itemTag))
            {
                return null;
            }

            var tag = itemTag.Trim();
            if (tag.Equals("desk", StringComparison.OrdinalIgnoreCase)) return itemDesk;
            if (tag.Equals("computer", StringComparison.OrdinalIgnoreCase)) return itemComputer;
            if (tag.Equals("bed", StringComparison.OrdinalIgnoreCase)) return itemBed;
            if (tag.Equals("pillow", StringComparison.OrdinalIgnoreCase)) return itemPillow;
            if (tag.Equals("blanket", StringComparison.OrdinalIgnoreCase)) return itemBlanket;
            if (tag.Equals("table", StringComparison.OrdinalIgnoreCase)) return itemTable;
            if (tag.Equals("tray", StringComparison.OrdinalIgnoreCase)) return itemTray;
            if (tag.Equals("cup", StringComparison.OrdinalIgnoreCase)) return itemCup;
            return null;
        }

        private static Sprite LoadIfMissing(Sprite current, string resourcePath)
        {
            if (current != null)
            {
                return current;
            }

            return Resources.Load<Sprite>(resourcePath);
        }
    }
}
