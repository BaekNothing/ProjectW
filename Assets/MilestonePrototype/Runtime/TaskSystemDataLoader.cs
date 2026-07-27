using System;
using System.IO;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public static class TaskSystemDataLoader
    {
        public const int SupportedSchema = 1;
        public const string FileName = "task-system.json";
        private const string ResourceName = "task-system";
        private static string patchDataPath;

        public static void Configure(string dataPath) =>
            patchDataPath = string.IsNullOrWhiteSpace(dataPath) ? null : dataPath;

        public static TaskSystemData Load()
        {
            string json = null;
            if (!string.IsNullOrWhiteSpace(patchDataPath))
            {
                string path = Path.Combine(patchDataPath, FileName);
                if (File.Exists(path)) json = File.ReadAllText(path);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                TextAsset asset = Resources.Load<TextAsset>(ResourceName);
                if (asset != null) json = asset.text;
            }

            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException($"Required gameplay data '{FileName}' was not found.");

            TaskSystemData data;
            try
            {
                data = JsonUtility.FromJson<TaskSystemData>(json);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Gameplay data '{FileName}' is invalid JSON.", exception);
            }

            Validate(data);
            return data;
        }

        public static void Validate(TaskSystemData data)
        {
            if (data == null || data.SchemaVersion != SupportedSchema)
                throw new InvalidOperationException($"Unsupported task-system schema {data?.SchemaVersion ?? 0}.");
            if (data.Balance == null || data.Works == null || data.Tasks == null || data.Crew == null)
                throw new InvalidOperationException("Task-system data is missing a required section.");
            if (data.CampaignEndDay <= 0 || data.StartingResources < 0)
                throw new InvalidOperationException("Task-system campaign values are invalid.");
            if (data.Balance.PrimaryProgressDays <= 0f ||
                data.Balance.InterruptionCostDays < 0f ||
                data.Balance.ResumptionCostDays < 0f)
                throw new InvalidOperationException("Task-system balance values are invalid.");
        }
    }
}
