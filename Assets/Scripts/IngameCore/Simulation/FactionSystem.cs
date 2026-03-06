using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectW.IngameCore.Simulation
{
    public enum FactionEventType
    {
        Formed,
        Split,
        Conflict
    }

    public readonly struct FactionEvent
    {
        public readonly FactionEventType EventType;
        public readonly string Description;

        public FactionEvent(FactionEventType eventType, string description)
        {
            EventType = eventType;
            Description = description ?? string.Empty;
        }
    }

    public sealed class FactionTickSnapshot
    {
        public IReadOnlyDictionary<string, string> AgentToFactionId { get; }
        public IReadOnlyDictionary<string, float> IsolationByAgent { get; }
        public IReadOnlyDictionary<string, float> GroupthinkByFaction { get; }
        public IReadOnlyList<FactionEvent> Events { get; }

        public FactionTickSnapshot(
            IReadOnlyDictionary<string, string> agentToFactionId,
            IReadOnlyDictionary<string, float> isolationByAgent,
            IReadOnlyDictionary<string, float> groupthinkByFaction,
            IReadOnlyList<FactionEvent> events)
        {
            AgentToFactionId = agentToFactionId ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IsolationByAgent = isolationByAgent ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            GroupthinkByFaction = groupthinkByFaction ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            Events = events ?? Array.Empty<FactionEvent>();
        }
    }

    public sealed class FactionSystem
    {
        private const float DefaultEdgeThreshold = 12f;
        private readonly Dictionary<string, HashSet<string>> _previousFactionMembers = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public FactionTickSnapshot ProcessTick(IReadOnlyList<AgentRuntimeState> agents, AffinitySystem affinitySystem)
        {
            if (agents == null || agents.Count == 0)
            {
                return new FactionTickSnapshot(null, null, null, null);
            }

            var validAgents = agents.Where(agent => agent != null && !string.IsNullOrWhiteSpace(agent.Id)).ToList();
            var index = new Dictionary<string, AgentRuntimeState>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < validAgents.Count; i++)
            {
                index[validAgents[i].Id] = validAgents[i];
            }

            var components = BuildComponents(validAgents, affinitySystem);
            var factionMembers = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var agentToFaction = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var isolationMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var groupthinkMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                var factionId = BuildFactionId(component);
                factionMembers[factionId] = new HashSet<string>(component, StringComparer.OrdinalIgnoreCase);

                var cohesion = ComputeInternalCohesion(component, affinitySystem);
                var groupthinkPenalty = Math.Clamp((cohesion - 65f) / 35f, 0f, 1f);
                groupthinkMap[factionId] = groupthinkPenalty;

                for (var m = 0; m < component.Count; m++)
                {
                    var agentId = component[m];
                    agentToFaction[agentId] = factionId;
                    isolationMap[agentId] = ComputeIsolationScore(agentId, validAgents, affinitySystem);

                    if (!index.TryGetValue(agentId, out var agent))
                    {
                        continue;
                    }

                    agent.FactionId = factionId;
                    agent.IsolationScore = isolationMap[agentId];
                    agent.GroupthinkPenalty = groupthinkPenalty;
                }
            }

            var events = BuildEvents(factionMembers, affinitySystem);
            _previousFactionMembers.Clear();
            foreach (var pair in factionMembers)
            {
                _previousFactionMembers[pair.Key] = new HashSet<string>(pair.Value, StringComparer.OrdinalIgnoreCase);
            }

            return new FactionTickSnapshot(agentToFaction, isolationMap, groupthinkMap, events);
        }

        private static List<List<string>> BuildComponents(IReadOnlyList<AgentRuntimeState> agents, AffinitySystem affinitySystem)
        {
            var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < agents.Count; i++)
            {
                adjacency[agents[i].Id] = new List<string>();
            }

            for (var i = 0; i < agents.Count; i++)
            {
                for (var j = i + 1; j < agents.Count; j++)
                {
                    var a = agents[i].Id;
                    var b = agents[j].Id;
                    var affinity = ComputeMutualAffinity(a, b, affinitySystem);
                    if (affinity < DefaultEdgeThreshold)
                    {
                        continue;
                    }

                    adjacency[a].Add(b);
                    adjacency[b].Add(a);
                }
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var components = new List<List<string>>();

            for (var i = 0; i < agents.Count; i++)
            {
                var root = agents[i].Id;
                if (visited.Contains(root))
                {
                    continue;
                }

                var queue = new Queue<string>();
                var component = new List<string>();
                queue.Enqueue(root);
                visited.Add(root);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);

                    var neighbors = adjacency[current];
                    for (var n = 0; n < neighbors.Count; n++)
                    {
                        var next = neighbors[n];
                        if (visited.Add(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }

                component.Sort(StringComparer.OrdinalIgnoreCase);
                components.Add(component);
            }

            components.Sort((left, right) => string.Compare(left[0], right[0], StringComparison.OrdinalIgnoreCase));
            return components;
        }

        private static string BuildFactionId(IReadOnlyList<string> members)
        {
            if (members == null || members.Count <= 0)
            {
                return "faction:solo";
            }

            return members.Count == 1 ? $"solo:{members[0]}" : $"faction:{members[0]}";
        }

        private static float ComputeMutualAffinity(string left, string right, AffinitySystem affinitySystem)
        {
            var lr = affinitySystem?.GetAffinity(left, right) ?? 0f;
            var rl = affinitySystem?.GetAffinity(right, left) ?? 0f;
            return (lr + rl) * 0.5f;
        }

        private static float ComputeIsolationScore(string agentId, IReadOnlyList<AgentRuntimeState> agents, AffinitySystem affinitySystem)
        {
            var count = Math.Max(1, agents.Count - 1);
            var affinitySum = 0f;
            var connected = 0;

            for (var i = 0; i < agents.Count; i++)
            {
                var otherId = agents[i].Id;
                if (string.Equals(agentId, otherId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var mutualAffinity = ComputeMutualAffinity(agentId, otherId, affinitySystem);
                affinitySum += mutualAffinity;
                if (mutualAffinity >= DefaultEdgeThreshold)
                {
                    connected += 1;
                }
            }

            var averageAffinity = affinitySum / count;
            var lowAffinityScore = Math.Clamp((30f - averageAffinity) / 80f, 0f, 1f);
            var sparseConnectionScore = 1f - (connected / (float)count);
            return Math.Clamp((lowAffinityScore * 0.6f) + (sparseConnectionScore * 0.4f), 0f, 1f);
        }

        private static float ComputeInternalCohesion(IReadOnlyList<string> members, AffinitySystem affinitySystem)
        {
            if (members == null || members.Count < 2)
            {
                return 0f;
            }

            var sum = 0f;
            var pairCount = 0;
            for (var i = 0; i < members.Count; i++)
            {
                for (var j = i + 1; j < members.Count; j++)
                {
                    sum += ComputeMutualAffinity(members[i], members[j], affinitySystem);
                    pairCount += 1;
                }
            }

            return pairCount <= 0 ? 0f : sum / pairCount;
        }

        private List<FactionEvent> BuildEvents(IReadOnlyDictionary<string, HashSet<string>> currentFactions, AffinitySystem affinitySystem)
        {
            var events = new List<FactionEvent>();
            var currentSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in currentFactions)
            {
                var signature = BuildSignature(pair.Value);
                currentSignatures.Add(signature);

                if (!_previousFactionMembers.Values.Any(previous => string.Equals(BuildSignature(previous), signature, StringComparison.OrdinalIgnoreCase))
                    && pair.Value.Count >= 2)
                {
                    events.Add(new FactionEvent(FactionEventType.Formed, $"{pair.Key} formed ({pair.Value.Count})"));
                }
            }

            foreach (var previous in _previousFactionMembers)
            {
                if (previous.Value.Count < 3)
                {
                    continue;
                }

                var previousSignature = BuildSignature(previous.Value);
                if (currentSignatures.Contains(previousSignature))
                {
                    continue;
                }

                var overlapCount = currentFactions.Values.Count(currentMembers => currentMembers.Overlaps(previous.Value));
                if (overlapCount >= 2)
                {
                    events.Add(new FactionEvent(FactionEventType.Split, $"{previous.Key} split into {overlapCount} clusters"));
                }
            }

            var factionList = currentFactions.ToList();
            for (var i = 0; i < factionList.Count; i++)
            {
                for (var j = i + 1; j < factionList.Count; j++)
                {
                    if (factionList[i].Value.Count == 1 && factionList[j].Value.Count == 1)
                    {
                        continue;
                    }

                    var crossAffinity = ComputeCrossAffinity(factionList[i].Value, factionList[j].Value, affinitySystem);
                    if (crossAffinity <= -35f)
                    {
                        events.Add(new FactionEvent(FactionEventType.Conflict, $"{factionList[i].Key}↔{factionList[j].Key} tension {crossAffinity:0}"));
                    }
                }
            }

            return events;
        }

        private static float ComputeCrossAffinity(HashSet<string> left, HashSet<string> right, AffinitySystem affinitySystem)
        {
            var sum = 0f;
            var count = 0;

            foreach (var l in left)
            {
                foreach (var r in right)
                {
                    sum += ComputeMutualAffinity(l, r, affinitySystem);
                    count += 1;
                }
            }

            return count <= 0 ? 0f : sum / count;
        }

        private static string BuildSignature(HashSet<string> members)
        {
            var ordered = members.OrderBy(member => member, StringComparer.OrdinalIgnoreCase);
            return string.Join("|", ordered);
        }
    }
}
