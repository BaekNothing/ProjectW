using NUnit.Framework;
using ProjectW.IngameCore.Simulation;

namespace ProjectW.Tests.EditMode
{
    public class CharacterNeuronSystemTests
    {
        [Test]
        public void RoutineNeuron_HungryNeedOverridesWorkIntent()
        {
            var system = new CharacterNeuronSystem();
            var snapshot = system.EvaluateRoutine(new RoutineNeuronContext(
                "A",
                10,
                10,
                15,
                0,
                hunger: 20f,
                sleep: 80f,
                stress: 80f,
                hungerThreshold: 30f,
                sleepThreshold: 25f,
                stressThreshold: 35f,
                hasLatchedNeed: false,
                latchedNeed: CharacterNeuronIntent.Work));

            Assert.AreEqual(CharacterNeuronIntent.Eat, snapshot.IntendedIntent);
            Assert.AreEqual("hungry_need", snapshot.DecisionReason);
        }

        [Test]
        public void CoreNeuron_PicksWorkInWorkHours()
        {
            var system = new CharacterNeuronSystem();
            var intent = system.EvaluateCore(new CoreNeuronContext(
                "A",
                1,
                11,
                0,
                satiety: 80f,
                satietyThreshold: 25f,
                burnout: 0f,
                burnoutThreshold: SimulationConstants.BurnoutThreshold,
                loadRatio: 1.2f,
                deadlinePressure: 0.7f,
                canOvertime: true));

            Assert.AreEqual(CharacterNeuronIntent.Work, intent);
        }
    }
}
