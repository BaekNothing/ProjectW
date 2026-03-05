using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Simulation
{
    public enum SessionDifficulty
    {
        Easy = 0,
        Normal = 1,
        Risky = 2
    }

    public enum WorkType
    {
        Observe = 0,
        Labor = 1,
        Routine = 2,
        Reflex = 3
    }

    [Serializable]
    public struct PriorityPair
    {
        public WorkType PrimaryWorkType;
        public WorkType SecondaryWorkType;

        public PriorityPair(WorkType primaryWorkType, WorkType secondaryWorkType)
        {
            PrimaryWorkType = primaryWorkType;
            SecondaryWorkType = secondaryWorkType;
        }
    }

    [Serializable]
    public sealed class DynamicSubtask
    {
        public string SubtaskId;
        public int RequiredWork;
        public List<string> RequiredTags = new List<string>();
        public WorkType WorkType;
        public string AssignedZoneKey;
        public int Progress;
    }

    [Serializable]
    public sealed class DynamicTaskModel
    {
        public string TaskId;
        public SessionDifficulty Difficulty;
        public List<DynamicSubtask> Subtasks = new List<DynamicSubtask>();

        public int TotalRequiredWork
        {
            get
            {
                var total = 0;
                for (var i = 0; i < Subtasks.Count; i++)
                {
                    total += Math.Max(0, Subtasks[i]?.RequiredWork ?? 0);
                }

                return total;
            }
        }

        public int TotalProgress
        {
            get
            {
                var total = 0;
                for (var i = 0; i < Subtasks.Count; i++)
                {
                    total += Math.Max(0, Subtasks[i]?.Progress ?? 0);
                }

                return total;
            }
        }

        public DynamicSubtask FirstIncomplete()
        {
            for (var i = 0; i < Subtasks.Count; i++)
            {
                var subtask = Subtasks[i];
                if (subtask == null)
                {
                    continue;
                }

                if (subtask.Progress < Math.Max(1, subtask.RequiredWork))
                {
                    return subtask;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class CharacterAptitudeProfile
    {
        public WorkType PrimaryType;
        public WorkType SecondaryType;
        public float ObserveMultiplier = 1f;
        public float LaborMultiplier = 1f;
        public float RoutineMultiplier = 1f;
        public float ReflexMultiplier = 1f;

        public float GetMultiplier(WorkType workType)
        {
            switch (workType)
            {
                case WorkType.Observe:
                    return ObserveMultiplier;
                case WorkType.Labor:
                    return LaborMultiplier;
                case WorkType.Routine:
                    return RoutineMultiplier;
                case WorkType.Reflex:
                    return ReflexMultiplier;
                default:
                    return 1f;
            }
        }
    }
}
