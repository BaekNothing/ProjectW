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
    }
}
