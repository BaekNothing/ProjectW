using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Simulation
{
    public sealed class SimulationTickEngine
    {
        private readonly Random _random;
        private readonly CharacterNeuronSystem _neuronSystem;

        public SimulationTickEngine(int seed)
        {
            _random = new Random(seed);
            _neuronSystem = new CharacterNeuronSystem();
        }

        public int AdvanceTick(DateTime simTime, int tickIndex, TaskModel task, IReadOnlyList<AgentRuntimeState> agents)
        {
            var hour = simTime.Hour;
            var minute = simTime.Minute;
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

                var loadRatio = Math.Max(0f, agent.SubtaskWork) / avgSubtask;
                var intent = _neuronSystem.EvaluateCore(new CoreNeuronContext(
                    agent.Id,
                    tickIndex,
                    hour,
                    minute,
                    agent.Satiety,
                    25f,
                    agent.BurnoutLevel,
                    SimulationConstants.BurnoutThreshold,
                    loadRatio,
                    deadlinePressure,
                    agent.CanOvertime()));

                var shouldWork = intent == CharacterNeuronIntent.Work;
                var isMealTime = intent == CharacterNeuronIntent.Eat;
                var isOvertimeWork = shouldWork && !(hour >= SimulationConstants.WorkStartHour && hour < SimulationConstants.WorkEndHour);

                if (intent == CharacterNeuronIntent.Eat)
                {
                    agent.OvertimeState = false;
                    agent.SetTargetZone((int)RoutineZone.Eat);
                }
                else if (intent == CharacterNeuronIntent.Work)
                {
                    agent.OvertimeState = isOvertimeWork;
                    agent.SetTargetZone((int)RoutineZone.Work);
                }
                else
                {
                    agent.OvertimeState = false;
                    agent.SetTargetZone((int)RoutineZone.Sleep);
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
