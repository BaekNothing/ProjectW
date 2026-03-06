namespace ProjectW.IngameCore.Simulation
{
    public static class SimulationConstants
    {
        public const int WorkStartHour = 9;
        public const int WorkEndHour = 18;
        public const int TickMinutes = 15;

        public const float LoadRatioThreshold = 1.1f;
        public const float DeadlinePressureThreshold = 1.2f;
        public const float OvertimeHappinessMin = 25f;

        public const float BurnoutThreshold = 35f;
        public const float BurnoutRate = 0.8f;
        public const float BurnoutPenaltyFactor = 0.03f;
        public const float SocialLossFactor = 0.5f;

        public const float RestEfficiencyDecay = 0.05f;
        public const float RestEfficiencyMin = 0.3f;

        public const float IsolationWorkPenaltyFactor = 0.35f;
        public const float GroupthinkWorkPenaltyFactor = 0.2f;

        public const float RelationshipClosenessDistance = 1.25f;
        public const float RelationshipMissionBlockedAffinityDelta = -0.8f;
        public const float RelationshipMissionBlockedMoodPenalty = -2.5f;
        public const float RelationshipMissionSyncAffinityDelta = 0.2f;
        public const float RelationshipMissionSyncMoodBonus = 0.4f;
        public const float RelationshipReworkBlameAffinityDelta = -1.8f;
        public const float RelationshipReworkBlameMoodPenalty = -1.2f;
        public const float RelationshipReworkHelpAffinityDelta = 1.4f;
        public const float RelationshipReworkHelpMoodBonus = 1.0f;

        public const float WorkMoodFactorMin = 0.65f;
        public const float WorkMoodFactorMax = 1.2f;
        public const float WorkAffinityFactorMin = 0.8f;
        public const float WorkAffinityFactorMax = 1.15f;
        public const float WorkKnowledgeFitFactorMin = 0.72f;
        public const float WorkKnowledgeFitFactorMax = 1.24f;
        public const float WorkIsolationFactorMin = 0.72f;
        public const float WorkIsolationFactorMax = 1.08f;
        public const float WorkFactionBiasFactorMin = 0.75f;
        public const float WorkFactionBiasFactorMax = 1.08f;
        public const float WorkEfficiencyMinClamp = 0.45f;
        public const float WorkEfficiencyMaxClamp = 1.35f;

        public const float CollaborationAcceptanceBase = 0.92f;
        public const float CollaborationLowMoodThreshold = 35f;
        public const float CollaborationHighIsolationThreshold = 0.62f;
        public const float CollaborationLowMoodPenalty = 0.25f;
        public const float CollaborationHighIsolationPenalty = 0.3f;

        public const float InformationShareBase = 0.9f;
        public const float InformationShareLowMoodPenalty = 0.2f;
        public const float InformationShareHighIsolationPenalty = 0.22f;

        public const float RumorDependenceBase = 0.08f;
        public const float RumorDependenceLowMoodBoost = 0.22f;
        public const float RumorDependenceHighIsolationBoost = 0.28f;

        public const float KnowledgeTrainingQuality = 0.85f;
        public const float KnowledgeCollaborationQuality = 0.65f;
        public const float KnowledgeRumorQuality = 0.35f;
        public const float RumorAccuracyMin = 0.55f;
        public const float RumorAccuracyRange = 0.25f;
    }
}
