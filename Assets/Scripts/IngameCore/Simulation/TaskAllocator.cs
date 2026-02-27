using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Simulation
{
    public static class TaskAllocator
    {
        public static void AssignWorkChunks(TaskModel task, IReadOnlyList<AgentRuntimeState> agents, Random random)
        {
            var assigned = 0;
            for (var i = 0; i < agents.Count; i++)
            {
                assigned += Math.Max(0, agents[i].SubtaskWork);
            }

            var unassigned = Math.Max(0, task.RemainingWork - assigned);
            if (unassigned <= 0)
            {
                return;
            }

            var freeAgents = new List<AgentRuntimeState>();
            for (var i = 0; i < agents.Count; i++)
            {
                if (agents[i].SubtaskWork <= 0)
                {
                    freeAgents.Add(agents[i]);
                }
            }

            if (freeAgents.Count == 0)
            {
                return;
            }

            for (var idx = 0; idx < freeAgents.Count; idx++)
            {
                var agent = freeAgents[idx];
                var agentsLeft = freeAgents.Count - idx;
                if (unassigned <= 0)
                {
                    break;
                }

                var maxForThis = Math.Max(1, unassigned - (agentsLeft - 1));
                var chunkUpper = Math.Min(25, maxForThis);
                var fallbackLower = Math.Min(Math.Max(1, task.TotalWork / 6), chunkUpper);
                var chunkLower = agentsLeft > 1 ? 1 : fallbackLower;
                var chunk = random.Next(chunkLower, chunkUpper + 1);
                agent.SubtaskWork = chunk;
                unassigned -= chunk;
            }
        }
    }
}
