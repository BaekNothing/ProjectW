using System;

namespace ProjectW.MilestonePrototype
{
    public readonly struct GanttBarProjection
    {
        public readonly int StartDay;
        public readonly int EndDayExclusive;
        public readonly float Progress01;

        public GanttBarProjection(int startDay, int endDayExclusive, float progress01)
        {
            StartDay = startDay;
            EndDayExclusive = Math.Max(startDay + 1, endDayExclusive);
            Progress01 = Math.Max(0f, Math.Min(1f, progress01));
        }
    }

    public static class ProjectWUiProjection
    {
        public static int TimelineStartDay(int today, int planningHorizon, int pastDays = 3) =>
            Math.Max(1, Math.Min(Math.Max(1, planningHorizon), today) - Math.Max(0, pastDays));

        public static GanttBarProjection Project(WorkTask task, TaskScheduleEstimate estimate, int today)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            int start = task.StartedDay > 0
                ? task.StartedDay
                : estimate != null && estimate.StartDay > 0 ? estimate.StartDay : Math.Max(1, today);
            int end = task.CompletedDay > 0
                ? task.CompletedDay + 1
                : estimate != null && estimate.CompletionDay >= start
                    ? estimate.CompletionDay + 1
                    : start + Math.Max(1, (int)Math.Ceiling(task.RemainingWork));
            return new GanttBarProjection(start, end, task.Completion);
        }

        public static string WorkDeadlineLabel(WorkGroup work, int today)
        {
            if (work == null) return string.Empty;
            int remaining = work.HardDeadline - today;
            return remaining >= 0 ? $"D-{remaining}" : $"D+{-remaining}";
        }
    }
}
