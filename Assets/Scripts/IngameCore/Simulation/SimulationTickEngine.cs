using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Simulation
{
    public sealed class SimulationTickEngine
    {
        private static readonly HashSet<(int hour, int minute)> MealTimes = new HashSet<(int hour, int minute)>()
        {
            (7, 0),
            (12, 30),
            (19, 0)
        };

        private readonly Random _random;

        public SimulationTickEngine(int seed)
        {
            _random = new Random(seed);
        }

        public int AdvanceTick(DateTime simTime, int tickIndex, TaskModel task, IReadOnlyList<AgentRuntimeState> agents)
        {
            var hour = simTime.Hour;
            var minute = simTime.Minute;
            var isMealTime = MealTimes.Contains((hour, minute));

            task.AdvanceElapsedHours(SimulationConstants.TickMinutes / 60f);
            var deadlinePressure = task.ComputeDeadlinePressure();

            TaskAllocator.AssignWorkChunks(task, agents, _random);

            var sumSubtask = 0f;
            for (var i = 0; i < agents.Count; i++)
            {
                sumSubtask += Math.Max(0, agents[i].SubtaskWork);
            }

            var avgSubtask = Math.Max(1f, sumSubtask / Math.Max(1, agents.Count));
            var totalProgress = 0;

            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                agent.Satiety = Math.Max(0f, agent.Satiety - 1f);
                if (agent.Satiety < 25f)
                {
                    agent.Happiness = Math.Max(0f, agent.Happiness - 0.3f);
                    agent.Stress += 0.1f;
                }

                var inWorkHours = hour >= SimulationConstants.WorkStartHour && hour < SimulationConstants.WorkEndHour;
                var shouldWork = false;
                var isOvertimeWork = false;

                if (isMealTime)
                {
                    agent.OvertimeState = false;
                    agent.SetTargetZone((int)RoutineZone.Eat);
                }
                else if (inWorkHours)
                {
                    agent.OvertimeState = false;
                    agent.SetTargetZone((int)RoutineZone.Work);
                    shouldWork = true;
                }
                else
                {
                    var loadRatio = Math.Max(0f, agent.SubtaskWork) / avgSubtask;
                    var overtimeOn = agent.DecideOvertime(loadRatio, deadlinePressure);
                    if (overtimeOn)
                    {
                        agent.SetTargetZone((int)RoutineZone.Work);
                        shouldWork = true;
                        isOvertimeWork = true;
                    }
                    else
                    {
                        agent.SetTargetZone((int)RoutineZone.Sleep);
                    }
                }

                var moved = agent.MoveOneTick();
                if (moved)
                {
                    agent.OvertimeState = agent.OvertimeState && isOvertimeWork;
                    continue;
                }

                if (agent.Position == (int)RoutineZone.PathSleepWork || agent.Position == (int)RoutineZone.PathWorkEat)
                {
                    continue;
                }

                if (shouldWork && agent.Position == (int)RoutineZone.Work)
                {
                    totalProgress += agent.Work(tickIndex, isOvertimeWork, _random);
                }
                else if (isMealTime && agent.Position == (int)RoutineZone.Eat)
                {
                    agent.Eat();
                }
                else if (agent.Position == (int)RoutineZone.Sleep || agent.Position == (int)RoutineZone.Eat)
                {
                    agent.Rest();
                }
            }

            task.ApplyProgress(totalProgress);
            return totalProgress;
        }
    }
}
