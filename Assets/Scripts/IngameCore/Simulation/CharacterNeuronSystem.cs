using System;

namespace ProjectW.IngameCore.Simulation
{
    public enum CharacterNeuronIntent
    {
        Work,
        Eat,
        Sleep
    }

    [Serializable]
    public struct CharacterNeuronSnapshot
    {
        public string CharacterId;
        public int Tick;
        public CharacterNeuronIntent ScheduledIntent;
        public CharacterNeuronIntent IntendedIntent;
        public string DecisionReason;
        public bool IsMealWindow;
        public bool IsSleepWindow;
        public bool IsHungry;
        public bool IsStressed;
        public bool HasLatch;
        public CharacterNeuronIntent LatchedIntent;
        public float Hunger;
        public float Sleep;
        public float Stress;
        public float HungerThreshold;
        public float SleepThreshold;
        public float StressThreshold;
    }

    public readonly struct RoutineNeuronContext
    {
        public readonly string CharacterId;
        public readonly int Tick;
        public readonly int Hour;
        public readonly int Minute;
        public readonly int RoutineOffsetTicks;
        public readonly float Hunger;
        public readonly float Sleep;
        public readonly float Stress;
        public readonly float HungerThreshold;
        public readonly float SleepThreshold;
        public readonly float StressThreshold;
        public readonly bool HasLatchedNeed;
        public readonly CharacterNeuronIntent LatchedNeed;

        public RoutineNeuronContext(
            string characterId,
            int tick,
            int hour,
            int minute,
            int routineOffsetTicks,
            float hunger,
            float sleep,
            float stress,
            float hungerThreshold,
            float sleepThreshold,
            float stressThreshold,
            bool hasLatchedNeed,
            CharacterNeuronIntent latchedNeed)
        {
            CharacterId = characterId;
            Tick = tick;
            Hour = hour;
            Minute = minute;
            RoutineOffsetTicks = routineOffsetTicks;
            Hunger = hunger;
            Sleep = sleep;
            Stress = stress;
            HungerThreshold = hungerThreshold;
            SleepThreshold = sleepThreshold;
            StressThreshold = stressThreshold;
            HasLatchedNeed = hasLatchedNeed;
            LatchedNeed = latchedNeed;
        }
    }

    public readonly struct CoreNeuronContext
    {
        public readonly string CharacterId;
        public readonly int Tick;
        public readonly int Hour;
        public readonly int Minute;
        public readonly float Satiety;
        public readonly float SatietyThreshold;
        public readonly float Burnout;
        public readonly float BurnoutThreshold;
        public readonly float LoadRatio;
        public readonly float DeadlinePressure;
        public readonly bool CanOvertime;

        public CoreNeuronContext(
            string characterId,
            int tick,
            int hour,
            int minute,
            float satiety,
            float satietyThreshold,
            float burnout,
            float burnoutThreshold,
            float loadRatio,
            float deadlinePressure,
            bool canOvertime)
        {
            CharacterId = characterId;
            Tick = tick;
            Hour = hour;
            Minute = minute;
            Satiety = satiety;
            SatietyThreshold = satietyThreshold;
            Burnout = burnout;
            BurnoutThreshold = burnoutThreshold;
            LoadRatio = loadRatio;
            DeadlinePressure = deadlinePressure;
            CanOvertime = canOvertime;
        }
    }

    public sealed class CharacterNeuronSystem
    {
        private readonly EventLogCollector eventLogCollector;

        public CharacterNeuronSystem(EventLogCollector eventLogCollector = null)
        {
            this.eventLogCollector = eventLogCollector;
        }

        public CharacterNeuronSnapshot EvaluateRoutine(RoutineNeuronContext context)
        {
            var scheduled = ResolveScheduledIntent(context.Hour, context.Minute, context.RoutineOffsetTicks, out var isMealWindow, out var isSleepWindow);
            var isHungry = context.Hunger <= context.HungerThreshold;
            var isStressed = context.Stress <= context.StressThreshold;

            var intent = scheduled;
            var reason = "schedule_default";

            if (isSleepWindow)
            {
                intent = CharacterNeuronIntent.Sleep;
                reason = "scheduled_sleep_window";
            }
            else if (isMealWindow)
            {
                intent = CharacterNeuronIntent.Eat;
                reason = "scheduled_meal_window";
            }
            else if (context.Hour >= 20)
            {
                intent = CharacterNeuronIntent.Sleep;
                reason = "after_work_sleep";
            }
            else
            {
                intent = CharacterNeuronIntent.Work;
                reason = "work_time_mission";
            }

            if (context.HasLatchedNeed)
            {
                intent = context.LatchedNeed;
                reason = "latched_need";
            }
            else if (isHungry)
            {
                intent = CharacterNeuronIntent.Eat;
                reason = "hungry_need";
            }
            else if (isStressed)
            {
                intent = CharacterNeuronIntent.Sleep;
                reason = "stressed_need";
            }

            return new CharacterNeuronSnapshot
            {
                CharacterId = context.CharacterId,
                Tick = context.Tick,
                ScheduledIntent = scheduled,
                IntendedIntent = intent,
                DecisionReason = reason,
                IsMealWindow = isMealWindow,
                IsSleepWindow = isSleepWindow,
                IsHungry = isHungry,
                IsStressed = isStressed,
                HasLatch = context.HasLatchedNeed,
                LatchedIntent = context.LatchedNeed,
                Hunger = context.Hunger,
                Sleep = context.Sleep,
                Stress = context.Stress,
                HungerThreshold = context.HungerThreshold,
                SleepThreshold = context.SleepThreshold,
                StressThreshold = context.StressThreshold
            };
        }

        public CharacterNeuronIntent EvaluateCore(CoreNeuronContext context)
        {
            var inWorkHours = context.Hour >= SimulationConstants.WorkStartHour && context.Hour < SimulationConstants.WorkEndHour;
            var isMealTime = IsCoreMealTime(context.Hour, context.Minute);

            if (isMealTime || context.Satiety <= context.SatietyThreshold)
            {
                eventLogCollector?.RecordNeuronDecision(CharacterNeuronIntent.Eat);
                return CharacterNeuronIntent.Eat;
            }

            if (inWorkHours)
            {
                eventLogCollector?.RecordNeuronDecision(CharacterNeuronIntent.Work);
                return CharacterNeuronIntent.Work;
            }

            if (!context.CanOvertime)
            {
                eventLogCollector?.RecordNeuronDecision(CharacterNeuronIntent.Sleep);
                return CharacterNeuronIntent.Sleep;
            }

            var overtimeNeed = context.LoadRatio > SimulationConstants.LoadRatioThreshold
                               || context.DeadlinePressure > SimulationConstants.DeadlinePressureThreshold;
            if (!overtimeNeed)
            {
                eventLogCollector?.RecordNeuronDecision(CharacterNeuronIntent.Sleep);
                return CharacterNeuronIntent.Sleep;
            }

            var burnoutPenalty = context.Burnout * SimulationConstants.BurnoutPenaltyFactor;
            var utility = context.DeadlinePressure - burnoutPenalty;
            var intent = utility > 0f ? CharacterNeuronIntent.Work : CharacterNeuronIntent.Sleep;
            eventLogCollector?.RecordNeuronDecision(intent);
            return intent;
        }

        private static CharacterNeuronIntent ResolveScheduledIntent(int hour, int minute, int routineOffsetTicks, out bool isMealWindow, out bool isSleepWindow)
        {
            var baseMinutes = ((hour * 60) + minute) % 1440;
            var adjustedMinutes = (baseMinutes + (routineOffsetTicks * 15)) % 1440;
            if (adjustedMinutes < 0)
            {
                adjustedMinutes += 1440;
            }

            var adjustedHour = (adjustedMinutes / 60) % 24;
            var adjustedMinute = adjustedMinutes % 60;

            isSleepWindow = adjustedHour < 8 || adjustedHour >= 22;
            isMealWindow = adjustedMinute == 0 && (adjustedHour == 8 || adjustedHour == 12 || adjustedHour == 18);

            if (isSleepWindow)
            {
                return CharacterNeuronIntent.Sleep;
            }

            if (isMealWindow)
            {
                return CharacterNeuronIntent.Eat;
            }

            return CharacterNeuronIntent.Work;
        }

        private static bool IsCoreMealTime(int hour, int minute)
        {
            return (hour == 7 && minute == 0)
                   || (hour == 12 && minute == 30)
                   || (hour == 19 && minute == 0);
        }
    }
}
