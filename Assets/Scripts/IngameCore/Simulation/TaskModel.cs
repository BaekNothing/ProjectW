using System;

namespace ProjectW.IngameCore.Simulation
{
    [Serializable]
    public sealed class TaskModel
    {
        public int TotalWork { get; private set; }
        public int RemainingWork { get; private set; }
        public float DeadlineHours { get; private set; }
        public float ElapsedHours { get; private set; }

        public TaskModel(int totalWork, float deadlineHours)
        {
            TotalWork = Math.Max(1, totalWork);
            RemainingWork = TotalWork;
            DeadlineHours = Math.Max(0.25f, deadlineHours);
            ElapsedHours = 0f;
        }

        public bool IsDone() => RemainingWork <= 0;

        public void AdvanceElapsedHours(float hours)
        {
            ElapsedHours = Math.Max(0f, ElapsedHours + hours);
        }

        public float ComputeDeadlinePressure()
        {
            var remainingDeadline = Math.Max(0.25f, DeadlineHours - ElapsedHours);
            return RemainingWork / remainingDeadline;
        }

        public void ApplyProgress(int totalProgress)
        {
            RemainingWork = Math.Max(0, RemainingWork - Math.Max(0, totalProgress));
        }
    }
}
