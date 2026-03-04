using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Simulation
{
    public sealed class SimulationTickEngine
    {
        private readonly Random _random;
        private readonly CharacterNeuronSystem _neuronSystem;
        private readonly JobSystem _jobSystem;
        private readonly List<WorldItem> _officeItems;
        private readonly EventLogCollector _eventLogCollector;
        private readonly int _seed;

        public SimulationTickEngine(int seed, EventLogCollector eventLogCollector = null)
        {
            _seed = seed;
            _random = new Random(seed);
            _neuronSystem = new CharacterNeuronSystem(eventLogCollector);
            _jobSystem = new JobSystem();
            _officeItems = OfficeItemFactory.GenerateOfficeItems(_random, 12, Array.Empty<string>());
            _eventLogCollector = eventLogCollector;
        }

        public int AdvanceTick(DateTime simTime, int tickIndex, TaskModel task, IReadOnlyList<AgentRuntimeState> agents)
        {
            _eventLogCollector?.BeginTick("simulation", tickIndex, "SimulationTick", _seed);
            var hour = simTime.Hour;
            var minute = simTime.Minute;
            task.AdvanceElapsedHours(SimulationConstants.TickMinutes / 60f);
            var deadlinePressure = task.ComputeDeadlinePressure();

            var ownerIds = new List<string>(agents.Count);
            for (var i = 0; i < agents.Count; i++)
            {
                ownerIds.Add(agents[i].Id);
            }

            if (_officeItems.Count == 0)
            {
                _officeItems.AddRange(OfficeItemFactory.GenerateOfficeItems(_random, 12, ownerIds));
            }

            var jobs = _jobSystem.BuildJobs(simTime, task, agents);

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
                var assignedJob = _jobSystem.AssignBestJob(agent, jobs, _officeItems, out var jobTrace);
                _eventLogCollector?.RecordJobDecision(jobTrace);
                var intent = ResolveIntentFromJob(assignedJob, agent, tickIndex, hour, minute, loadRatio, deadlinePressure);

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
                    if (assignedJob != null && assignedJob.JobType == AtomicJobType.Work && agent.SubtaskWork <= 0)
                    {
                        agent.SubtaskWork = Math.Max(1, assignedJob.WorkUnits);
                    }

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
            _eventLogCollector?.RecordStateTransition(ProjectW.IngameCore.StateMachine.CoreLoopState.Resolve, ProjectW.IngameCore.StateMachine.CoreLoopState.Resolve);
            _eventLogCollector?.EndTick();
            return totalProgress;
        }

        private CharacterNeuronIntent ResolveIntentFromJob(
            AtomicJob assignedJob,
            AgentRuntimeState agent,
            int tickIndex,
            int hour,
            int minute,
            float loadRatio,
            float deadlinePressure)
        {
            if (assignedJob == null)
            {
                return _neuronSystem.EvaluateCore(new CoreNeuronContext(
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
            }

            switch (assignedJob.JobType)
            {
                case AtomicJobType.Eat:
                    return CharacterNeuronIntent.Eat;
                case AtomicJobType.Sleep:
                    return CharacterNeuronIntent.Sleep;
                default:
                    return CharacterNeuronIntent.Work;
            }
        }
    }
}
