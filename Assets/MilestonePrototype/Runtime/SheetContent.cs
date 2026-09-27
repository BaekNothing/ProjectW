#if UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    // Spec Ref: DataAuthoring/GoogleSheetsContent.md. Build-owned data, no runtime network.
    [Serializable] public sealed class SheetText { public string tid, ko, en; }
    [Serializable] public sealed class SheetCharacter { public string id, name_tid; public int affinity, focus, response, inquiry; }
    [Serializable] public sealed class SheetPerk { public string id, name_tid, description_tid; }
    [Serializable] public sealed class SheetCharacterPerk { public string id, character_id, perk_id; }
    [Serializable] public sealed class SheetEffect { public string id, perk_id, target, tags_all, operation; public float value; }
    [Serializable] public sealed class SheetTask { public string id, name_tid, stat, tags; public int difficulty; public float duration_hours; }
    [Serializable] public sealed class SheetEvent { public string id, name_tid, tags, task_id, required_flags; public float base_probability; public int cooldown_days, min_friends, min_closeness; }
    [Serializable] public sealed class SheetRelationship { public string id, character_id, other_id, kind; public int closeness; }
    [Serializable] public sealed class SheetOfficeText { public string id, group, text_tid; public int position; }
    [Serializable] public sealed class SheetBundle
    {
        public int schema_version;
        public SheetText[] Texts;
        public SheetCharacter[] Characters;
        public SheetPerk[] Perks;
        public SheetCharacterPerk[] CharacterPerks;
        public SheetEffect[] PerkEffects;
        public SheetTask[] Tasks;
        public SheetEvent[] Events;
        public SheetRelationship[] Relationships;
        public SheetOfficeText[] OfficeTextGroups;
    }

    public static class SheetContent
    {
        public const string LocaleKey = "projectw.locale.v1";
        private static SheetBundle data;
        private static string locale;
        private static readonly Dictionary<string, SheetText> textIndex = new Dictionary<string, SheetText>();
        private static readonly Dictionary<string, string[]> officeGroups = new Dictionary<string, string[]>();
        public static SheetBundle Data
        {
            get
            {
                if (data != null) return data;
                var asset = Resources.Load<TextAsset>("sheet-content");
                if (asset == null) throw new InvalidOperationException("Missing sheet-content. Run the content importer before building.");
                data = JsonUtility.FromJson<SheetBundle>(asset.text);
                if (data == null || data.schema_version != 1 || data.Texts == null)
                    throw new InvalidOperationException("Unsupported sheet-content bundle.");
                foreach (var row in data.Texts) textIndex.Add(row.tid, row);
                return data;
            }
        }
        public static string Locale
        {
            get { if (locale == null) { locale = PlayerPrefs.GetString(LocaleKey, "ko"); if (locale != "ko" && locale != "en" && locale != "tid") locale = "ko"; } return locale; }
        }
        public static void SetLocale(string value)
        {
            if (value != "ko" && value != "en" && value != "tid") throw new ArgumentException("Unsupported locale", nameof(value));
            locale = value;
            officeGroups.Clear();
            PlayerPrefs.SetString(LocaleKey, value);
            PlayerPrefs.Save();
        }
        public static string Resolve(SheetText row, string mode)
        {
            if (mode == "tid") return row.tid;
            string text = mode == "en" ? row.en : row.ko;
            if (string.IsNullOrEmpty(text)) text = row.ko;
            if (string.IsNullOrEmpty(text)) return row.tid;
            return text == "<EMPTY>" ? "" : text;
        }
        public static string T(string tid)
        {
            if (Locale == "tid") return tid;
            var loaded = Data;
            if (textIndex.TryGetValue(tid, out var row)) return Resolve(row, Locale);
            return tid;
        }
        public static string Format(string tid, params string[] pairs)
        {
            if (pairs.Length % 2 != 0) throw new ArgumentException("Expected key/value pairs");
            if (Locale == "tid") return tid;
            string result = T(tid);
            for (int i = 0; i < pairs.Length; i += 2) result = result.Replace("{" + pairs[i] + "}", pairs[i + 1]);
            return result;
        }
        public static string[] OfficeGroup(string name, int count)
        {
            if (officeGroups.TryGetValue(name, out var cached)) return cached;
            var result = new string[count];
            foreach (var row in Data.OfficeTextGroups)
                if (row.group == name) result[row.position] = T(row.text_tid);
            officeGroups.Add(name, result);
            return result;
        }
        public static bool TagsMatch(string required, string actual)
        {
            if (string.IsNullOrEmpty(required)) return true;
            string bounded = ";" + (actual ?? "") + ";";
            foreach (string tag in required.Split(';')) if (!bounded.Contains(";" + tag + ";")) return false;
            return true;
        }
        public static float Apply(SheetBundle bundle, string characterId, string target, string tags, float initial)
        {
            float value = initial;
            foreach (var effect in bundle.PerkEffects)
            {
                if (effect.target != target || !TagsMatch(effect.tags_all, tags)) continue;
                bool equipped = false;
                foreach (var link in bundle.CharacterPerks)
                    if (link.character_id == characterId && link.perk_id == effect.perk_id) { equipped = true; break; }
                if (equipped) value = effect.operation == "multiply" ? value * effect.value : value + effect.value;
            }
            return value;
        }
        public static float TaskSuccess(SheetBundle bundle, string characterId, string taskId, float stateBonus = 0)
        {
            SheetCharacter character = null;
            SheetTask task = null;
            foreach (var row in bundle.Characters) if (row.id == characterId) character = row;
            foreach (var row in bundle.Tasks) if (row.id == taskId) task = row;
            if (character == null || task == null) throw new ArgumentException("Unknown character/task ID");
            int stat = task.stat == "affinity" ? character.affinity : task.stat == "focus" ? character.focus : task.stat == "response" ? character.response : character.inquiry;
            return Mathf.Clamp(Apply(bundle, characterId, "task_success", task.tags, 50 + stat - task.difficulty + stateBonus), 5, 95) / 100f;
        }
        public static float EventProbability(SheetBundle bundle, string characterId, string eventId, string flags, int daysSinceLast)
        {
            bool found = false;
            foreach (var row in bundle.Characters) if (row.id == characterId) found = true;
            if (!found) throw new ArgumentException("Unknown character ID");
            foreach (var row in bundle.Events)
            {
                if (row.id != eventId) continue;
                if (daysSinceLast < row.cooldown_days || !TagsMatch(row.required_flags, flags)) return 0;
                int friends = 0;
                foreach (var rel in bundle.Relationships)
                    if (rel.character_id == characterId && rel.kind == "friend" && rel.closeness >= row.min_closeness) friends++;
                if (friends < row.min_friends) return 0;
                return Mathf.Clamp01(Apply(bundle, characterId, "event_probability", row.tags, row.base_probability));
            }
            throw new ArgumentException("Unknown event ID");
        }
    }
}
#endif
