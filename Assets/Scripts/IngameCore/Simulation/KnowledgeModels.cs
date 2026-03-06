using System;

namespace ProjectW.IngameCore.Simulation
{
    public enum KnowledgeType
    {
        Procedural = 0,
        Domain = 1,
        Tool = 2,
        Rumor = 3
    }

    [Serializable]
    public sealed class KnowledgeEntry
    {
        public string Key;
        public KnowledgeType Type = KnowledgeType.Domain;
        public float Difficulty = 0.5f;
        public float DecayRate = 0.01f;
    }

    public enum KnowledgeSourceType
    {
        Training = 0,
        Rumor = 1,
        Observation = 2,
        Collaboration = 3
    }
}
