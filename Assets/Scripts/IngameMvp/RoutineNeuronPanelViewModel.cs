using System.Globalization;
using ProjectW.IngameCore.Simulation;

namespace ProjectW.IngameMvp
{
    public readonly struct RoutineNeuronPanelViewModel
    {
        public readonly string Title;
        public readonly string IntentLine;
        public readonly string ReasonLine;
        public readonly string ConditionLine;
        public readonly string GaugeLine;

        public RoutineNeuronPanelViewModel(string title, string intentLine, string reasonLine, string conditionLine, string gaugeLine)
        {
            Title = title;
            IntentLine = intentLine;
            ReasonLine = reasonLine;
            ConditionLine = conditionLine;
            GaugeLine = gaugeLine;
        }

        public static RoutineNeuronPanelViewModel FromSnapshot(CharacterNeuronSnapshot snapshot, string currentAction, string intendedAction)
        {
            return FromSnapshot(snapshot, currentAction, intendedAction, string.Empty, string.Empty);
        }

        public static RoutineNeuronPanelViewModel FromSnapshot(
            CharacterNeuronSnapshot snapshot,
            string currentAction,
            string intendedAction,
            string aptitudeLine,
            string subtaskLine)
        {
            var title = string.Format(CultureInfo.InvariantCulture, "Neuron | {0} | Tick {1}", snapshot.CharacterId, snapshot.Tick);
            var intentLine = string.Format(CultureInfo.InvariantCulture, "scheduled={0} intended={1} current={2}", snapshot.ScheduledIntent, intendedAction, currentAction);
            var reasonLine = string.Format(CultureInfo.InvariantCulture, "reason={0} latch={1}({2}) {3}", snapshot.DecisionReason, snapshot.HasLatch, snapshot.LatchedIntent, aptitudeLine ?? string.Empty).Trim();
            var conditionLine = string.Format(CultureInfo.InvariantCulture, "mealWindow={0} sleepWindow={1} hungry={2} stressed={3} {4}", snapshot.IsMealWindow, snapshot.IsSleepWindow, snapshot.IsHungry, snapshot.IsStressed, subtaskLine ?? string.Empty).Trim();
            var gaugeLine = string.Format(CultureInfo.InvariantCulture, "hunger={0:0.0}/{1:0.0} sleep={2:0.0}/{3:0.0} stress={4:0.0}/{5:0.0}",
                snapshot.Hunger,
                snapshot.HungerThreshold,
                snapshot.Sleep,
                snapshot.SleepThreshold,
                snapshot.Stress,
                snapshot.StressThreshold);
            return new RoutineNeuronPanelViewModel(title, intentLine, reasonLine, conditionLine, gaugeLine);
        }
    }
}
