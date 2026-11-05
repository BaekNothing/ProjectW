using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectW.MilestonePrototype
{
    public static class MilestoneRules
    {
        public static bool ShouldShowWorkInTaskPanel(
            WorkGroup group, List<WorkTask> tasks, int currentDay)
        {
            if (group == null || group.State != WorkState.Complete) return true;
            int completionDay = WorkCompletionDay(group, tasks);
            return completionDay <= 0 || currentDay - completionDay < 14;
        }

        public static int WorkCompletionDay(WorkGroup group, List<WorkTask> tasks)
        {
            if (group == null || tasks == null) return 0;
            int completionDay = 0;
            foreach (WorkTask task in tasks)
                if (task != null && task.GroupId == group.Id && task.Required &&
                    task.State == TaskState.Complete)
                    completionDay = Math.Max(completionDay, task.CompletedDay);
            return completionDay;
        }

        public static int TaskActualDurationDays(WorkTask task, int currentDay)
        {
            if (task == null || task.StartedDay <= 0) return 0;
            int endDay = task.CompletedDay > 0
                ? task.CompletedDay
                : Math.Max(task.StartedDay, currentDay - 1);
            return Math.Max(1, endDay - task.StartedDay + 1);
        }

        public static bool HasWorkHistory(WorkGroup group, List<WorkTask> tasks)
        {
            if (group == null || tasks == null) return false;
            if (group.State == WorkState.InProgress || group.State == WorkState.Complete ||
                group.State == WorkState.Failed) return true;
            return tasks.Any(task => task != null &&
                (task.StartedDay > 0 || task.Progress > 0f || task.Records?.Count > 0));
        }

        public static int WorkHistoryStartDay(List<WorkTask> tasks)
        {
            if (tasks == null) return 0;
            int earliest = int.MaxValue;
            foreach (WorkTask task in tasks)
            {
                if (task == null) continue;
                if (task.StartedDay > 0) earliest = Math.Min(earliest, task.StartedDay);
                if (task.Records == null) continue;
                foreach (TaskRecord record in task.Records)
                    if (record != null && record.Day > 0) earliest = Math.Min(earliest, record.Day);
            }
            return earliest == int.MaxValue ? 0 : earliest;
        }

        public static int WorkHistoryLatestDay(WorkGroup group, List<WorkTask> tasks, int currentDay)
        {
            int latest = WorkHistoryStartDay(tasks);
            if (tasks != null)
                foreach (WorkTask task in tasks)
                {
                    if (task == null) continue;
                    latest = Math.Max(latest, task.CompletedDay);
                    if (task.Records == null) continue;
                    foreach (TaskRecord record in task.Records)
                        if (record != null) latest = Math.Max(latest, record.Day);
                }
            if (group?.State == WorkState.InProgress) latest = Math.Max(latest, currentDay);
            if (group?.State == WorkState.Failed) latest = Math.Max(latest, group.HardDeadline + 1);
            return latest;
        }

        public static int WorkHistoryEndDay(WorkGroup group, List<WorkTask> tasks, int currentDay) =>
            WorkHistoryLatestDay(group, tasks, currentDay);

        public static int WorkHistoryElapsedDays(WorkGroup group, List<WorkTask> tasks, int currentDay)
        {
            int start = WorkHistoryStartDay(tasks);
            int end = WorkHistoryEndDay(group, tasks, currentDay);
            return start <= 0 || end < start ? 0 : end - start + 1;
        }

        public static string WorkHistoryParticipants(List<WorkTask> tasks, List<CrewMember> crew)
        {
            var names = new List<string>();
            if (tasks == null) return "기록 없음";
            foreach (WorkTask task in tasks)
            {
                if (task == null) continue;
                if (task.Records != null)
                    foreach (TaskRecord record in task.Records)
                        AddHistoryParticipant(names, record?.Actor);
                AddHistoryParticipant(names, CrewName(crew, task.AssignedCharacter));
                AddHistoryParticipant(names, CrewName(crew, task.LastWorker));
            }
            return names.Count == 0 ? "기록 없음" : string.Join(", ", names);
        }

        private static void AddHistoryParticipant(List<string> names, string name)
        {
            if (names == null || string.IsNullOrEmpty(name) || name == "SYSTEM" || names.Contains(name)) return;
            names.Add(name);
        }

        private static string CrewName(List<CrewMember> crew, int index) =>
            crew != null && index >= 0 && index < crew.Count ? crew[index].Name : null;
    }
}
