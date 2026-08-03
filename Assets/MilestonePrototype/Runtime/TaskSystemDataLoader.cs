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
            if (data.Balance == null || data.Works == null || data.Tasks == null || data.Crew == null ||
                data.RandomTaskWords == null ||
                data.RandomTaskWords.Adjectives == null || data.RandomTaskWords.Adjectives.Length == 0 ||
                data.RandomTaskWords.Targets == null || data.RandomTaskWords.Targets.Length == 0 ||
                data.RandomTaskWords.Actions == null || data.RandomTaskWords.Actions.Length == 0)
                throw new InvalidOperationException("Task-system data is missing a required section.");
            if (data.Crew.Length != MilestoneSimulation.TeamSize)
                throw new InvalidOperationException(
                    $"A field team must contain exactly {MilestoneSimulation.TeamSize} crew members.");
            foreach (CrewMember member in data.Crew)
            {
                if (member == null || string.IsNullOrWhiteSpace(member.Personality))
                    throw new InvalidOperationException("Every crew member requires a personality.");
                if (member == null || member.Competencies == null ||
                    member.Competencies.Length != CrewMember.CompetencyCount)
                    throw new InvalidOperationException(
                        $"Every crew member requires exactly {CrewMember.CompetencyCount} competencies.");
                foreach (int competency in member.Competencies)
                    if (competency < 0 || competency > CrewMember.MaximumCompetency)
                        throw new InvalidOperationException(
                            $"Crew competencies must be between 0 and {CrewMember.MaximumCompetency}.");
            }
            foreach (WorkTask task in data.Tasks)
                ValidateRequiredCompetencies(task?.RequiredCompetencies, "Every Task");
            if (data.CampaignEndDay <= 0 || data.MidpointReviewDay <= 0 ||
                data.MidpointReviewDay >= data.CampaignEndDay || data.StartingResources < 0)
                throw new InvalidOperationException("Task-system campaign values are invalid.");
            if (data.Balance.PrimaryProgressDays <= 0f ||
                data.Balance.InterruptionCostDays < 0f ||
                data.Balance.ResumptionCostDays < 0f ||
                data.Balance.PrerequisiteProgressLimit < 0f ||
                data.Balance.PrerequisiteProgressLimit > 1f ||
                data.Balance.LowOutputChance < 0 ||
                data.Balance.HighOutputChance < 0 ||
                data.Balance.LowOutputChance + data.Balance.HighOutputChance > 100 ||
                data.Balance.FreshLowOutputChance < 0 ||
                data.Balance.FreshHighOutputChance < 0 ||
                data.Balance.FreshLowOutputChance + data.Balance.FreshHighOutputChance > 100 ||
                data.Balance.ExhaustedLowOutputChance < 0 ||
                data.Balance.ExhaustedHighOutputChance < 0 ||
                data.Balance.ExhaustedLowOutputChance + data.Balance.ExhaustedHighOutputChance > 100 ||
                data.Balance.LowOutputMultiplier <= 0f ||
                data.Balance.HighOutputMultiplier <= 0f)
                throw new InvalidOperationException("Task-system balance values are invalid.");
            if (data.Balance.RandomWorkChanceScalePercent < 0 ||
                data.Balance.RandomWorkChanceScalePercent > 100 ||
                data.Balance.RandomWorkMinRequiredDays <= 0 ||
                data.Balance.RandomWorkMaxRequiredDays <
                data.Balance.RandomWorkMinRequiredDays)
                throw new InvalidOperationException("Random work balance values are invalid.");
            foreach (RandomTaskTarget target in data.RandomTaskWords.Targets)
            {
                bool hasCompatibleAction = false;
                foreach (RandomTaskAction action in data.RandomTaskWords.Actions)
                {
                    if (action != null && target != null && action.Role == target.Role)
                        hasCompatibleAction = true;
                }
                if (target == null || string.IsNullOrWhiteSpace(target.Id) ||
                    string.IsNullOrWhiteSpace(target.Text) || !hasCompatibleAction)
                    throw new InvalidOperationException(
                        "Every random task target requires at least one compatible action.");
                ValidateRequiredCompetencies(target.RequiredCompetencies, "Every random task target");
            }
            foreach (RandomTaskAdjective adjective in data.RandomTaskWords.Adjectives)
            {
                if (adjective == null || string.IsNullOrWhiteSpace(adjective.Id) ||
                    string.IsNullOrWhiteSpace(adjective.Text))
                    throw new InvalidOperationException("Random task words cannot be empty.");
            }
            foreach (RandomTaskAction action in data.RandomTaskWords.Actions)
            {
                if (action == null || string.IsNullOrWhiteSpace(action.Id) ||
                    string.IsNullOrWhiteSpace(action.Text))
                    throw new InvalidOperationException("Random task words cannot be empty.");
                ValidateRequiredCompetencies(action.RequiredCompetencies, "Every random task action");
            }
        }

        private static void ValidateRequiredCompetencies(int[] competencies, string owner)
        {
            if (competencies == null || competencies.Length < 1 || competencies.Length > 3)
                throw new InvalidOperationException($"{owner} requires one to three competencies.");
            for (int i = 0; i < competencies.Length; i++)
            {
                if (competencies[i] < 0 || competencies[i] >= CrewMember.CompetencyCount)
                    throw new InvalidOperationException($"{owner} contains an invalid competency index.");
                for (int previous = 0; previous < i; previous++)
                    if (competencies[previous] == competencies[i])
                        throw new InvalidOperationException($"{owner} cannot repeat a competency.");
            }
        }
    }
}
