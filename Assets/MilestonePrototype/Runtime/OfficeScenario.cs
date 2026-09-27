#if UNITY_WEBGL || UNITY_EDITOR
using System;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    // Spec: MagicalGirlOfficePrototype — authored M1 only; not the campaign simulator.
    [Serializable]
    public sealed class OfficeSnapshot
    {
        public int SchemaVersion = 1;
        public string ScenarioId = "office-seven-days";
        public int Day = 1;
        public int[] Choices = { -1, -1, -1, -1, -1, -1, -1 };
    }

    public sealed class OfficeScenario
    {
        public const string SaveKey = "projectw.office.m1.v1";
        public static readonly string[] PersonIds = { "office-hana", "office-sori", "office-yuna" };
        public static string[] Names => SheetContent.OfficeGroup("Names", 3);
        public static string[] Roles => SheetContent.OfficeGroup("Roles", 3);
        public static string[] Titles => SheetContent.OfficeGroup("Titles", 7);
        public static string[] Senders => SheetContent.OfficeGroup("Senders", 7);
        public static string[] Bodies => SheetContent.OfficeGroup("Bodies", 7);
        public static string[] Options => SheetContent.OfficeGroup("Options", 14);
        public static string[] Reasons => SheetContent.OfficeGroup("Reasons", 14);
        public static string[] Results => SheetContent.OfficeGroup("Results", 14);

        private OfficeSnapshot state = new OfficeSnapshot();
        public int Day => state.Day;
        public bool Complete => Day == 8;
        public int CurrentIndex => Math.Min(Day - 1, 6);
        public bool CanAdvance => !Complete && Choice(CurrentIndex) >= 0;
        public int Choice(int index) => index >= 0 && index < 7 ? state.Choices[index] : -1;
        public bool Choose(int choice)
        {
            if (Complete || choice < 0 || choice > 1) return false;
            state.Choices[CurrentIndex] = choice;
            return true;
        }
        public bool Advance()
        {
            if (!CanAdvance) return false;
            state.Day++;
            return true;
        }
        public string Report(int index) => Choice(index) < 0 ? SheetContent.T("office.no_decision") : Results[index * 2 + Choice(index)];
        public string Export() => JsonUtility.ToJson(state);
        public bool Restore(string json)
        {
            // Require fields explicitly: JsonUtility otherwise accepts absent fields as defaults.
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"SchemaVersion\"") ||
                !json.Contains("\"ScenarioId\"") || !json.Contains("\"Day\"") || !json.Contains("\"Choices\"")) return false;
            OfficeSnapshot next;
            try { next = JsonUtility.FromJson<OfficeSnapshot>(json); }
            catch (ArgumentException) { return false; }
            if (next == null || next.SchemaVersion != 1 || next.ScenarioId != "office-seven-days" ||
                next.Day < 1 || next.Day > 8 || next.Choices == null || next.Choices.Length != 7) return false;
            for (int i = 0; i < 7; i++)
                if (next.Choices[i] < -1 || next.Choices[i] > 1 ||
                    (i < next.Day - 1 && next.Choices[i] < 0) || (i > next.Day - 1 && next.Choices[i] != -1)) return false;
            state = next;
            return true;
        }
    }
}
#endif
