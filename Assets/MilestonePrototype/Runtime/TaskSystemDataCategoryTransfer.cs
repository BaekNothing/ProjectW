using System;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public static class TaskSystemDataCategoryTransfer
    {
        public const int Characters = 0;
        public const int Balance = 1;
        public const int CriticalEvents = 2;
        public const int Mail = 3;

        public static string Copy(TaskSystemData source, int category)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var transfer = new TaskSystemData
            {
                SchemaVersion = TaskSystemDataLoader.SupportedSchema,
                TransferCategory = CategoryName(category)
            };
            switch (category)
            {
                case Characters:
                    transfer.Crew = source.Crew;
                    transfer.PerkDefinitions = source.PerkDefinitions;
                    break;
                case Balance: transfer.Balance = source.Balance; break;
                case CriticalEvents: transfer.CriticalEvents = source.CriticalEvents; break;
                case Mail: transfer.Mail = source.Mail; break;
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
            return JsonUtility.ToJson(transfer, true);
        }

        public static bool TryApply(TaskSystemData source, int category, string json,
            out TaskSystemData updated, out string error)
        {
            updated = source;
            error = string.Empty;
            try
            {
                if (source == null) throw new InvalidOperationException("Current gameplay data is unavailable.");
                TaskSystemData transfer = TaskSystemDataLoader.ParseUnchecked(json);
                if (transfer.SchemaVersion != TaskSystemDataLoader.SupportedSchema)
                    throw new InvalidOperationException("The category JSON has an unsupported schema version.");
                string expectedCategory = CategoryName(category);
                if (transfer.TransferCategory != expectedCategory)
                    throw new InvalidOperationException(
                        $"Expected category '{expectedCategory}', got '{transfer.TransferCategory ?? "none"}'.");
                TaskSystemData candidate = TaskSystemDataLoader.Parse(TaskSystemDataLoader.Serialize(source));
                switch (category)
                {
                    case Characters:
                        if (transfer.Crew == null) throw new InvalidOperationException("Characters JSON requires Crew.");
                        candidate.Crew = transfer.Crew;
                        if (transfer.PerkDefinitions == null)
                            throw new InvalidOperationException("Characters JSON requires PerkDefinitions.");
                        candidate.PerkDefinitions = transfer.PerkDefinitions;
                        EnsureUniqueCrew(candidate.Crew);
                        break;
                    case Balance:
                        if (transfer.Balance == null) throw new InvalidOperationException("Balance JSON requires Balance.");
                        candidate.Balance = transfer.Balance;
                        break;
                    case CriticalEvents:
                        if (transfer.CriticalEvents == null)
                            throw new InvalidOperationException("Critical-events JSON requires CriticalEvents.");
                        candidate.CriticalEvents = transfer.CriticalEvents;
                        EnsureUniqueCriticalEvents(candidate.CriticalEvents);
                        break;
                    case Mail:
                        if (transfer.Mail == null) throw new InvalidOperationException("Mail JSON requires Mail.");
                        candidate.Mail = transfer.Mail;
                        EnsureUniqueMail(candidate.Mail);
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(category));
                }
                TaskSystemDataLoader.Validate(candidate);
                updated = candidate;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string CategoryName(int category)
        {
            switch (category)
            {
                case Characters: return "Characters";
                case Balance: return "BalanceAndProbabilities";
                case CriticalEvents: return "CriticalEvents";
                case Mail: return "Mail";
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        private static void EnsureUniqueCrew(CrewMember[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null || string.IsNullOrWhiteSpace(values[i].Name))
                    throw new InvalidOperationException("Every character requires a name.");
                for (int previous = 0; previous < i; previous++)
                    if (values[previous].Name == values[i].Name)
                        throw new InvalidOperationException($"Character name '{values[i].Name}' is duplicated.");
            }
        }

        private static void EnsureUniqueCriticalEvents(CriticalEventDefinition[] values)
        {
            for (int i = 0; i < values.Length; i++)
                for (int previous = 0; previous < i; previous++)
                    if (values[previous] != null && values[i] != null && values[previous].Id == values[i].Id)
                        throw new InvalidOperationException($"Critical-event id '{values[i].Id}' is duplicated.");
        }

        private static void EnsureUniqueMail(MailEvent[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null || string.IsNullOrWhiteSpace(values[i].Id))
                    throw new InvalidOperationException("Every mail item requires an id.");
                for (int previous = 0; previous < i; previous++)
                    if (values[previous].Id == values[i].Id)
                        throw new InvalidOperationException($"Mail id '{values[i].Id}' is duplicated.");
            }
        }
    }
}
