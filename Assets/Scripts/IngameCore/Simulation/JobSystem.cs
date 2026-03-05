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

    public sealed class JobDecisionTrace
    {
        public string DecisionTraceId;
        public string SelectedActionId;
        public List<string> CandidateActions = new List<string>();
        public List<float> CandidateScores = new List<float>();
        public List<string> BlockedActions = new List<string>();
    }

    public sealed class JobSystem
    {
        public List<AtomicJob> BuildJobs(DateTime simTime, TaskModel task, IReadOnlyList<AgentRuntimeState> agents)
        {
            return BuildJobs(simTime, task, agents, null, null);
        }

        public List<AtomicJob> BuildJobs(
            DateTime simTime,
            TaskModel task,
            IReadOnlyList<AgentRuntimeState> agents,
            TaskGenerationRuleSet taskRuleSet,
            Random random)
        {
            var jobs = new List<AtomicJob>();
            var workJobs = BuildWorkJobs(task, agents, taskRuleSet, random);
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
            return AssignBestJob(agent, jobs, officeItems, out _);
        }

        public AtomicJob AssignBestJob(AgentRuntimeState agent, List<AtomicJob> jobs, IReadOnlyList<WorldItem> officeItems, out JobDecisionTrace trace)
        {
            trace = new JobDecisionTrace
            {
                DecisionTraceId = $"dec-{agent.Id}-{jobs.Count}",
                SelectedActionId = null
            };

            AtomicJob bestFallback = null;
            for (var i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                if (job.Claimed)
                {
                    trace.BlockedActions.Add(job.JobId);
                    continue;
                }

                trace.CandidateActions.Add(job.JobId);
                trace.CandidateScores.Add(ScoreJob(agent, job));

                if (job.Requirement != null && !job.Requirement.IsSatisfied(officeItems, agent.Id))
                {
                    trace.BlockedActions.Add(job.JobId);
                    continue;
                }

                if (job.JobType == AtomicJobType.Work)
                {
                    job.Claimed = true;
                    trace.SelectedActionId = job.JobId;
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
                trace.SelectedActionId = bestFallback.JobId;
            }

            return bestFallback;
        }

        private static float ScoreJob(AgentRuntimeState agent, AtomicJob job)
        {
            switch (job.JobType)
            {
                case AtomicJobType.Work:
                    return 1f + Math.Max(0, job.WorkUnits) * 0.01f;
                case AtomicJobType.Eat:
                    return Math.Max(0f, (100f - agent.Satiety) / 100f);
                case AtomicJobType.Sleep:
                    return Math.Max(0f, (100f - agent.Stress) / 100f);
                default:
                    return 0f;
            }
        }

        private static List<AtomicJob> BuildWorkJobs(
            TaskModel task,
            IReadOnlyList<AgentRuntimeState> agents,
            TaskGenerationRuleSet taskRuleSet,
            Random random)
        {
            var jobs = new List<AtomicJob>();
            if (task == null || agents == null || agents.Count == 0)
            {
                return jobs;
            }

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

            var jobCount = Math.Max(1, Math.Min(remaining, freeAgentCount));
            var rng = random ?? new Random(1);
            var jitterRatio = Math.Max(0d, taskRuleSet != null ? taskRuleSet.WorkUnitJitterRatio : 0.2d);
            var allocation = AllocateWorkUnits(remaining, jobCount, jitterRatio, rng);
            var plannedTemplates = new TaskTemplateRule[jobCount];
            var plannedUnits = new int[jobCount];
            for (var i = 0; i < jobCount; i++)
            {
                var template = PickTemplate(taskRuleSet, rng);
                var units = allocation[i];
                if (template != null)
                {
                    var minUnits = 1;
                    var maxUnits = Math.Max(minUnits, template.MaxWorkUnits);
                    units = Math.Max(minUnits, Math.Min(maxUnits, units));
                }

                plannedTemplates[i] = template;
                plannedUnits[i] = units;
            }

            var sumUnits = 0;
            for (var i = 0; i < plannedUnits.Length; i++)
            {
                sumUnits += Math.Max(1, plannedUnits[i]);
            }

            var delta = remaining - sumUnits;
            plannedUnits[plannedUnits.Length - 1] = Math.Max(1, plannedUnits[plannedUnits.Length - 1] + delta);

            for (var i = 0; i < jobCount; i++)
            {
                var template = plannedTemplates[i];
                jobs.Add(new AtomicJob
                {
                    JobId = template != null
                        ? $"work-{template.TemplateId}-{i}"
                        : $"work-{i}",
                    JobType = AtomicJobType.Work,
                    ZoneKey = string.IsNullOrWhiteSpace(template?.ZoneKey) ? "workzone" : template.ZoneKey.Trim(),
                    Requirement = BuildRequirementFromTemplate(template),
                    WorkUnits = plannedUnits[i]
                });
            }

            return jobs;
        }

        private static NeedRequirement BuildRequirementFromTemplate(TaskTemplateRule template)
        {
            if (template == null)
            {
                return new NeedRequirement("work", "workzone", new[] { "desk", "computer" });
            }

            var needKey = string.IsNullOrWhiteSpace(template.NeedKey) ? "work" : template.NeedKey.Trim();
            var zoneKey = string.IsNullOrWhiteSpace(template.ZoneKey) ? "workzone" : template.ZoneKey.Trim();
            var tags = template.RequiredTags ?? Array.Empty<string>();
            return new NeedRequirement(needKey, zoneKey, tags);
        }

        private static TaskTemplateRule PickTemplate(TaskGenerationRuleSet ruleSet, Random random)
        {
            if (ruleSet == null || !ruleSet.HasTemplates())
            {
                return null;
            }

            var templates = ruleSet.Templates;
            var total = 0f;
            for (var i = 0; i < templates.Count; i++)
            {
                total += Math.Max(0f, templates[i]?.Weight ?? 0f);
            }

            if (total <= 0.0001f)
            {
                return templates[random.Next(0, templates.Count)];
            }

            var roll = (float)random.NextDouble() * total;
            var sum = 0f;
            for (var i = 0; i < templates.Count; i++)
            {
                sum += Math.Max(0f, templates[i]?.Weight ?? 0f);
                if (roll <= sum)
                {
                    return templates[i];
                }
            }

            return templates[templates.Count - 1];
        }

        private static int[] AllocateWorkUnits(int totalUnits, int workerCount, double jitterRatio, Random random)
        {
            var allocation = new int[Math.Max(1, workerCount)];
            var baseUnits = Math.Max(1, totalUnits / Math.Max(1, workerCount));
            var consumed = 0;

            for (var i = 0; i < allocation.Length; i++)
            {
                var remainingWorkers = allocation.Length - i;
                var remainingUnits = Math.Max(1, totalUnits - consumed);
                if (remainingWorkers == 1)
                {
                    allocation[i] = remainingUnits;
                    consumed += allocation[i];
                    continue;
                }

                var jitter = (random.NextDouble() * 2d) - 1d;
                var candidate = (int)Math.Round(baseUnits * (1d + (jitterRatio * jitter)));
                var minForThis = 1;
                var maxForThis = Math.Max(1, remainingUnits - (remainingWorkers - 1));
                allocation[i] = Math.Max(minForThis, Math.Min(maxForThis, candidate));
                consumed += allocation[i];
            }

            var diff = totalUnits - consumed;
            allocation[allocation.Length - 1] = Math.Max(1, allocation[allocation.Length - 1] + diff);
            return allocation;
        }

        private static bool IsMealWindow(int hour, int minute)
        {
            return (hour == 8 && minute == 0)
                   || (hour == 12 && minute == 0)
                   || (hour == 18 && minute == 0);
        }
    }
}
