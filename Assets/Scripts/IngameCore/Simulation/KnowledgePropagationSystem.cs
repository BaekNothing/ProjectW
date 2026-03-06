using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Simulation
{
    public sealed class KnowledgePropagationSystem
    {
        private static readonly string[] DefaultKnowledgeKeys =
        {
            "knowledge.observe.basics",
            "knowledge.labor.basics",
            "knowledge.routine.basics",
            "knowledge.reflex.basics"
        };

        public void ProcessTick(
            IReadOnlyList<AgentRuntimeState> agents,
            AffinitySystem affinitySystem,
            EventLogCollector eventLogCollector,
            Random random)
        {
            if (agents == null || agents.Count < 2 || random == null)
            {
                return;
            }

            for (var i = 0; i < agents.Count; i++)
            {
                var source = agents[i];
                if (source == null)
                {
                    continue;
                }

                for (var j = 0; j < agents.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    var target = agents[j];
                    if (target == null)
                    {
                        continue;
                    }

                    var path = ResolvePropagationPath(source, target);
                    var sourceType = ResolveSourceType(path);
                    var key = ResolveKnowledgeKey(source, target);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    var probability = ComputeSuccessProbability(source, target, affinitySystem, sourceType);
                    if (random.NextDouble() > probability)
                    {
                        eventLogCollector?.RecordKnowledgeFailure(
                            source.Id,
                            target.Id,
                            key,
                            sourceType,
                            probability,
                            BuildFailureReason(path, source, target));
                        continue;
                    }

                    var sourceConfidence = source.GetKnowledgeConfidence(key);
                    var beforeConfidence = target.GetKnowledgeConfidence(key);
                    var quality = ResolveQuality(path);
                    var transferGain = Math.Clamp(sourceConfidence * quality, 0f, 1f);
                    var distorted = false;
                    var reason = path;

                    if (sourceType == KnowledgeSourceType.Rumor)
                    {
                        var rumorAccuracy = 0.55f + (float)random.NextDouble() * 0.25f;
                        transferGain *= rumorAccuracy;
                        distorted = rumorAccuracy < 0.7f;
                        if (distorted)
                        {
                            reason = "rumor_distorted";
                        }
                    }

                    var afterConfidence = Math.Clamp(beforeConfidence + (1f - beforeConfidence) * transferGain, 0f, 1f);
                    target.SetKnowledgeConfidence(key, afterConfidence);

                    eventLogCollector?.RecordKnowledgeTransfer(
                        source.Id,
                        target.Id,
                        key,
                        sourceType,
                        success: true,
                        probability,
                        beforeConfidence,
                        afterConfidence,
                        distorted,
                        reason);

                    if (distorted)
                    {
                        eventLogCollector?.RecordKnowledgeFailure(
                            source.Id,
                            target.Id,
                            key,
                            sourceType,
                            probability,
                            "distortion_side_effect");
                    }
                }
            }
        }

        private static string ResolvePropagationPath(AgentRuntimeState source, AgentRuntimeState target)
        {
            var sameZone = source.Position == target.Position;
            var sourceWorking = source.TargetZone == (int)RoutineZone.Work;
            var targetWorking = target.TargetZone == (int)RoutineZone.Work;

            if (sameZone && sourceWorking && targetWorking)
            {
                return "training";
            }

            if (sourceWorking && targetWorking)
            {
                return "collaboration";
            }

            return "rumor";
        }

        private static KnowledgeSourceType ResolveSourceType(string path)
        {
            return path switch
            {
                "training" => KnowledgeSourceType.Training,
                "collaboration" => KnowledgeSourceType.Collaboration,
                _ => KnowledgeSourceType.Rumor
            };
        }

        private static float ResolveQuality(string path)
        {
            return path switch
            {
                "training" => 0.85f,
                "collaboration" => 0.65f,
                _ => 0.35f
            };
        }

        private static float ComputeSuccessProbability(
            AgentRuntimeState source,
            AgentRuntimeState target,
            AffinitySystem affinitySystem,
            KnowledgeSourceType sourceType)
        {
            var affinity = affinitySystem?.GetAffinity(source.Id, target.Id) ?? 0f;
            var affinityFactor = 0.5f + ((affinity + 100f) / 200f) * 0.5f;
            var distance = Math.Abs(source.Position - target.Position);
            var distanceFactor = 1f / (1f + (distance * 0.75f));

            var moodFactor = ((Math.Clamp(source.Happiness, 0f, 100f) + Math.Clamp(target.Happiness, 0f, 100f)) / 200f);
            var stressPenalty = (Math.Clamp(source.Stress, 0f, 100f) + Math.Clamp(target.Stress, 0f, 100f)) / 200f;
            var stressFactor = 1f - (stressPenalty * 0.45f);

            var sourceTypeFactor = sourceType switch
            {
                KnowledgeSourceType.Training => 0.85f,
                KnowledgeSourceType.Collaboration => 0.7f,
                KnowledgeSourceType.Rumor => 0.55f,
                _ => 0.65f
            };

            var probability = sourceTypeFactor * affinityFactor * distanceFactor * (0.55f + (moodFactor * 0.45f)) * stressFactor;
            return Math.Clamp(probability, 0.05f, 0.95f);
        }

        private static string ResolveKnowledgeKey(AgentRuntimeState source, AgentRuntimeState target)
        {
            string key = null;
            float bestGap = 0.01f;

            for (var i = 0; i < DefaultKnowledgeKeys.Length; i++)
            {
                var candidate = DefaultKnowledgeKeys[i];
                var sourceConfidence = source.GetKnowledgeConfidence(candidate);
                var targetConfidence = target.GetKnowledgeConfidence(candidate);
                var gap = sourceConfidence - targetConfidence;
                if (gap > bestGap)
                {
                    bestGap = gap;
                    key = candidate;
                }
            }

            return key;
        }

        private static string BuildFailureReason(string path, AgentRuntimeState source, AgentRuntimeState target)
        {
            var distance = Math.Abs(source.Position - target.Position);
            if (distance >= 2)
            {
                return path + "_distance";
            }

            if (source.Stress > 70f || target.Stress > 70f)
            {
                return path + "_stress";
            }

            if (source.Happiness < 30f || target.Happiness < 30f)
            {
                return path + "_mood";
            }

            return path + "_roll";
        }
    }
}
