using NUnit.Framework;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class ProjectWUiProjectionTests
    {
        [TestCase(1, 90, 1)]
        [TestCase(4, 90, 1)]
        [TestCase(20, 90, 17)]
        [TestCase(100, 90, 87)]
        public void TimelineStartsWithThreePastDaysAndStaysInPlanningRange(
            int today, int horizon, int expected)
        {
            Assert.That(ProjectWUiProjection.TimelineStartDay(today, horizon), Is.EqualTo(expected));
        }

        [Test]
        public void CompletedTaskUsesRecordedHistoryInsteadOfMovingWithToday()
        {
            var task = new WorkTask
            {
                RequiredWork = 4f,
                Progress = 4f,
                StartedDay = 6,
                CompletedDay = 9,
                State = TaskState.Complete
            };

            GanttBarProjection projection = ProjectWUiProjection.Project(task, null, 30);

            Assert.That(projection.StartDay, Is.EqualTo(6));
            Assert.That(projection.EndDayExclusive, Is.EqualTo(10));
            Assert.That(projection.Progress01, Is.EqualTo(1f));
        }

        [Test]
        public void UnstartedTaskUsesPriorityScheduleProjection()
        {
            var task = new WorkTask { RequiredWork = 3f, Progress = 0f };
            var estimate = new TaskScheduleEstimate { StartDay = 12, CompletionDay = 15 };

            GanttBarProjection projection = ProjectWUiProjection.Project(task, estimate, 4);

            Assert.That(projection.StartDay, Is.EqualTo(12));
            Assert.That(projection.EndDayExclusive, Is.EqualTo(16));
            Assert.That(projection.Progress01, Is.Zero);
        }

        [TestCase(20, 15, "D-5")]
        [TestCase(20, 20, "D-0")]
        [TestCase(20, 23, "D+3")]
        public void DeadlineLabelShowsBeforeTodayAndOverdue(int deadline, int today, string expected)
        {
            Assert.That(ProjectWUiProjection.WorkDeadlineLabel(
                new WorkGroup { HardDeadline = deadline }, today), Is.EqualTo(expected));
        }
    }
}
