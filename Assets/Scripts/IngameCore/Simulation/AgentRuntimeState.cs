using System;

namespace ProjectW.IngameCore.Simulation
{
    [Serializable]
    public sealed class AgentRuntimeState
    {
        public string Id;
        public int SubtaskWork;

        public float Stress = 0f;
        public float Happiness = 70f;
        public float Satiety = 70f;

        public bool OvertimeState;
        public float BurnoutLevel;
        public float RestEfficiency = 1f;
        public int ConsecutiveOvertime;

        public int Position = (int)RoutineZone.Sleep;
        public int TargetZone = (int)RoutineZone.Sleep;

        public int InspirationCooldown;
        public int LastInspirationTick;

        public AgentRuntimeState(string id, int inspirationCooldown)
        {
            Id = id;
            InspirationCooldown = Math.Max(1, inspirationCooldown);
        }

        public bool IsBurnedOut() => BurnoutLevel > SimulationConstants.BurnoutThreshold;

        public bool CanOvertime()
        {
            return Happiness > SimulationConstants.OvertimeHappinessMin && !IsBurnedOut();
        }

        public bool DecideOvertime(float loadRatio, float deadlinePressure)
        {
            if (!CanOvertime())
            {
                OvertimeState = false;
                return false;
            }

            var overtimeNeed = loadRatio > SimulationConstants.LoadRatioThreshold
                               || deadlinePressure > SimulationConstants.DeadlinePressureThreshold;
            if (!overtimeNeed)
            {
                OvertimeState = false;
                return false;
            }

            var burnoutPenalty = BurnoutLevel * SimulationConstants.BurnoutPenaltyFactor;
            var socialLoss = ConsecutiveOvertime * SimulationConstants.SocialLossFactor;
            var utility = deadlinePressure - burnoutPenalty - socialLoss;

            OvertimeState = utility > 0f;
            return OvertimeState;
        }

        public void SetTargetZone(int zone)
        {
            if (zone < (int)RoutineZone.Sleep)
            {
                TargetZone = (int)RoutineZone.Sleep;
                return;
            }

            if (zone > (int)RoutineZone.Eat)
            {
                TargetZone = (int)RoutineZone.Eat;
                return;
            }

            TargetZone = zone;
        }

        public bool MoveOneTick()
        {
            if (Position < TargetZone)
            {
                Position += 1;
                return true;
            }

            if (Position > TargetZone)
            {
                Position -= 1;
                return true;
            }

            return false;
        }

        public int Work(int tick, bool isOvertime, Random random)
        {
            if (SubtaskWork <= 0)
            {
                return 0;
            }

            var progress = random.Next(1, 6);
            var speedFactor = IsBurnedOut() ? 0.7f : 1f;
            progress = Math.Max(1, (int)(progress * speedFactor));

            if (tick - LastInspirationTick > InspirationCooldown)
            {
                var inspirationProbability = IsBurnedOut() ? 0.1f : 0.2f;
                if (random.NextDouble() < inspirationProbability)
                {
                    progress += random.Next(5, 16);
                    LastInspirationTick = tick;
                }
            }

            var actual = Math.Min(progress, SubtaskWork);
            SubtaskWork -= actual;

            Stress += actual * (isOvertime ? 0.2f : 0.1f);
            Happiness = Math.Max(0f, Happiness - (actual * (isOvertime ? 0.08f : 0.03f)));

            if (isOvertime)
            {
                BurnoutLevel += SimulationConstants.BurnoutRate;
                ConsecutiveOvertime += 1;
                RestEfficiency = Math.Max(
                    SimulationConstants.RestEfficiencyMin,
                    RestEfficiency - SimulationConstants.RestEfficiencyDecay);
            }
            else
            {
                ConsecutiveOvertime = 0;
            }

            return actual;
        }

        public void Rest()
        {
            Stress = Math.Max(0f, Stress - (0.5f * RestEfficiency));
            Happiness = Math.Min(100f, Happiness + (0.2f * RestEfficiency));
            BurnoutLevel = Math.Max(0f, BurnoutLevel - (0.1f * RestEfficiency));
            OvertimeState = false;
            ConsecutiveOvertime = 0;
        }

        public void Eat()
        {
            Satiety = Math.Min(100f, Satiety + 35f);
            Happiness = Math.Min(100f, Happiness + 0.4f);
            Stress = Math.Max(0f, Stress - 0.2f);
        }
    }
}
