using System;
using System.IO;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public static class TaskSystemDataLoader
    {
        public const int SupportedSchema = 1;
        public const string FileName = "task-system.json";
        public const string OverrideStorageKey = "projectw.game-data.override.v1";
        private const string ResourceName = "task-system";
        private static string patchDataPath;
        private static ProjectW.Contracts.IStringStorage storage;

        public static void Configure(string dataPath, ProjectW.Contracts.IStringStorage value = null)
        {
            patchDataPath = string.IsNullOrWhiteSpace(dataPath) ? null : dataPath;
            storage = value;
        }

        public static TaskSystemData Load()
        {
            string json = null;
            if (storage != null && storage.TryGetString(OverrideStorageKey, out string overridden) &&
                !string.IsNullOrWhiteSpace(overridden))
                json = overridden;
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

            return Parse(json);
        }

        public static string Serialize(TaskSystemData data)
        {
            Validate(data);
            return JsonUtility.ToJson(data, true);
        }

        public static TaskSystemData Parse(string json)
        {
            TaskSystemData data = ParseUnchecked(json);
            Validate(data);
            return data;
        }

        public static TaskSystemData ParseUnchecked(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Gameplay data JSON is empty.");
            TaskSystemData data;
            try
            {
                data = JsonUtility.FromJson<TaskSystemData>(json);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Gameplay data '{FileName}' is invalid JSON.", exception);
            }

            return data;
        }

        public static void SaveOverride(TaskSystemData data)
        {
            if (storage == null) throw new InvalidOperationException("Gameplay data storage is unavailable.");
            storage.SetString(OverrideStorageKey, Serialize(data));
        }

        public static void DeleteOverride()
        {
            if (storage != null) storage.DeleteKey(OverrideStorageKey);
        }

        public static void Validate(TaskSystemData data)
        {
            if (data == null || data.SchemaVersion != SupportedSchema)
                throw new InvalidOperationException($"Unsupported task-system schema {data?.SchemaVersion ?? 0}.");
            if (data.Balance == null || data.Works == null || data.Tasks == null || data.Crew == null ||
                data.Codex == null || data.Codex.Length == 0 ||
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
                if (string.IsNullOrWhiteSpace(member.PortraitAddress))
                    throw new InvalidOperationException("Every crew member requires a portrait address.");
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
            if (data.CriticalEvents != null)
            {
                foreach (CriticalEventDefinition definition in data.CriticalEvents)
                {
                    if (definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
                        definition.StartDay <= 0 || string.IsNullOrWhiteSpace(definition.FirstNodeId) ||
                        definition.Nodes == null || definition.Nodes.Length == 0)
                        throw new InvalidOperationException("Every critical event requires an id, start day, first node, and nodes.");
                    bool hasFirstNode = false;
                    foreach (CriticalEventNode node in definition.Nodes)
                    {
                        if (node == null || string.IsNullOrWhiteSpace(node.Id) ||
                            string.IsNullOrWhiteSpace(node.Subject) || node.Choices == null ||
                            node.Choices.Length == 0)
                            throw new InvalidOperationException("Every critical event node requires content and choices.");
                        if (node.Id == definition.FirstNodeId) hasFirstNode = true;
                        foreach (CriticalEventChoice choice in node.Choices)
                        {
                            if (choice == null || string.IsNullOrWhiteSpace(choice.Id) ||
                                string.IsNullOrWhiteSpace(choice.Text) || choice.Outcomes == null ||
                                choice.Outcomes.Length == 0)
                                throw new InvalidOperationException("Every critical event choice requires weighted outcomes.");
                            int totalWeight = 0;
                            foreach (CriticalEventOutcome outcome in choice.Outcomes)
                            {
                                if (outcome == null)
                                    throw new InvalidOperationException("Critical event outcomes cannot be null.");
                                if (outcome.Weight > 0) totalWeight += outcome.Weight;
                                if (!string.IsNullOrWhiteSpace(outcome.NextNodeId))
                                {
                                    bool hasNextNode = false;
                                    foreach (CriticalEventNode candidate in definition.Nodes)
                                        if (candidate != null && candidate.Id == outcome.NextNodeId)
                                            hasNextNode = true;
                                    if (!hasNextNode)
                                        throw new InvalidOperationException("A critical event next node was not found.");
                                }
                            }
                            if (totalWeight <= 0)
                                throw new InvalidOperationException("Critical event outcome weights must total above zero.");
                        }
                    }
                    if (!hasFirstNode)
                        throw new InvalidOperationException("A critical event first node was not found.");
                }
            }
            for (int i = 0; i < data.Codex.Length; i++)
            {
                CodexEntry entry = data.Codex[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) ||
                    string.IsNullOrWhiteSpace(entry.Category) ||
                    string.IsNullOrWhiteSpace(entry.Name) ||
                    string.IsNullOrWhiteSpace(entry.Description))
                    throw new InvalidOperationException("Every Codex entry requires an id, category, name, and description.");
                for (int previous = 0; previous < i; previous++)
                    if (data.Codex[previous].Id == entry.Id)
                        throw new InvalidOperationException($"Codex entry id '{entry.Id}' is duplicated.");
            }
            if (data.CampaignEndDay <= 0 || data.MidpointReviewDay <= 0 ||
                data.MidpointReviewDay >= data.CampaignEndDay || data.StartingResources < 0)
                throw new InvalidOperationException("Task-system campaign values are invalid.");
            if (data.Balance.PrimaryProgressDays <= 0f ||
                data.Balance.InterruptionCostDays < 0f ||
                data.Balance.ResumptionCostDays < 0f ||
                data.Balance.WeekendFatigueRecovery < 0 ||
                data.Balance.WeekendMentalRecovery < 0 ||
                data.Balance.WeekendInjuryRecoveryChance < 0 ||
                data.Balance.WeekendInjuryRecoveryChance > 100 ||
                data.Balance.FirstRegularCheckupDay <= 0 ||
                data.Balance.RegularCheckupIntervalDays <= 0 ||
                data.Balance.UnscheduledCheckupResourceCost < 0 ||
                data.Balance.RegenerationResourceCost < 0 ||
                data.Balance.RegenerationAbilityInheritanceCost < 0 ||
                data.Balance.RegenerationPerkInheritanceCost < 0 ||
                data.Balance.RegenerationPersonalityRetentionWeight < 0 ||
                data.Balance.RegenerationPersonalityRetentionWeight > 100 ||
                data.Balance.PayrollIntervalDays <= 0 ||
                data.Balance.BaseSalary < 0 ||
                data.Balance.ExperiencePerSalaryIncrease <= 0 ||
                data.Balance.SalaryIncrease < 0 ||
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
