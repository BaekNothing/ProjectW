using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Simulation
{
    public enum AtomicJobType
    {
        Work,
        Eat,
        Sleep
    }

    public sealed class AtomicJob
    {
        public string JobId;
        public AtomicJobType JobType;
        public string ZoneKey;
        public NeedRequirement Requirement;
        public int WorkUnits;
        public bool Claimed;
    }

    public sealed class JobSystem
    {
        public List<AtomicJob> BuildJobs(DateTime simTime, TaskModel task, IReadOnlyList<AgentRuntimeState> agents)
        {
            var jobs = new List<AtomicJob>();
            var workJobs = BuildWorkJobs(task, agents);
            jobs.AddRange(workJobs);

            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent.Satiety <= 30f || IsMealWindow(simTime.Hour, simTime.Minute))
                {
                    jobs.Add(new AtomicJob
                    {
                        JobId = $"eat-{agent.Id}-{simTime:HHmm}",
                        JobType = AtomicJobType.Eat,
                        ZoneKey = "eatzone",
                        Requirement = new NeedRequirement("eat", "eatzone", new[] { "table", "tray", "cup" }),
                        WorkUnits = 1
                    });
                }

                var sleepingWindow = simTime.Hour >= 22 || simTime.Hour < 7;
                if (agent.Stress <= 35f || sleepingWindow)
                {
                    jobs.Add(new AtomicJob
                    {
                        JobId = $"sleep-{agent.Id}-{simTime:HHmm}",
                        JobType = AtomicJobType.Sleep,
                        ZoneKey = "sleepzone",
                        Requirement = new NeedRequirement("sleep", "sleepzone", new[] { "bed", "pillow", "blanket" }),
                        WorkUnits = 1
                    });
                }
            }

            return jobs;
        }

        public AtomicJob AssignBestJob(AgentRuntimeState agent, List<AtomicJob> jobs, IReadOnlyList<WorldItem> officeItems)
        {
            AtomicJob bestFallback = null;
            for (var i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                if (job.Claimed)
                {
                    continue;
                }

                if (job.Requirement != null && !job.Requirement.IsSatisfied(officeItems, agent.Id))
                {
                    continue;
                }

                if (job.JobType == AtomicJobType.Work)
                {
                    job.Claimed = true;
                    return job;
                }

                if (bestFallback == null)
                {
                    bestFallback = job;
                }
            }

            if (bestFallback != null)
            {
                bestFallback.Claimed = true;
            }

            return bestFallback;
        }

        private static List<AtomicJob> BuildWorkJobs(TaskModel task, IReadOnlyList<AgentRuntimeState> agents)
        {
            var jobs = new List<AtomicJob>();
            var assigned = 0;
            for (var i = 0; i < agents.Count; i++)
            {
                assigned += Math.Max(0, agents[i].SubtaskWork);
            }

            var remaining = Math.Max(0, task.RemainingWork - assigned);
            if (remaining <= 0)
            {
                return jobs;
            }

            var freeAgentCount = 0;
            for (var i = 0; i < agents.Count; i++)
            {
                if (agents[i].SubtaskWork <= 0)
                {
                    freeAgentCount += 1;
                }
            }

            if (freeAgentCount <= 0)
            {
                return jobs;
            }

            var perJob = Math.Max(1, remaining / freeAgentCount);
            for (var i = 0; i < freeAgentCount; i++)
            {
                var units = i == freeAgentCount - 1 ? Math.Max(1, remaining - (perJob * i)) : perJob;
                jobs.Add(new AtomicJob
                {
                    JobId = $"work-{i}",
                    JobType = AtomicJobType.Work,
                    ZoneKey = "workzone",
                    Requirement = new NeedRequirement("work", "workzone", new[] { "desk", "computer" }),
                    WorkUnits = units
                });
            }

            return jobs;
        }

        private static bool IsMealWindow(int hour, int minute)
        {
            return (hour == 8 && minute == 0)
                   || (hour == 12 && minute == 0)
                   || (hour == 18 && minute == 0);
        }
    }
}
