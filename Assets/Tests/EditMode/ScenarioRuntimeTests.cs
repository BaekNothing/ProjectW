using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectW.IngameCore.CaseReview;

namespace ProjectW.Tests.EditMode
{
    public sealed class ScenarioRuntimeTests
    {
        [Test]
        public void Scheduler_ReturnsLoopBoundaryCandidates_WhenConditionsMatch()
        {
            var scenario = Scenario("scenario.audit", triggerMode: ScenarioTriggerMode.LoopBoundary);
            scenario.Conditions.Add(new ScenarioCondition
            {
                Key = ScenarioConditionKey.Tag,
                Value = "audit",
                Comparison = ScenarioComparison.Exists
            });
            var scheduler = new ScenarioScheduler(new FixedScenarioProvider(scenario), new ScenarioPlaybackStore());

            var candidates = scheduler.GetLoopBoundaryCandidates(ScenarioTiming.Night, Context(tags: new[] { "audit" }));

            Assert.AreEqual("scenario.audit", candidates.Single().EventId);
        }

        [Test]
        public void Scheduler_BlocksAuthoringOnlyEventsWithoutConditions()
        {
            var scheduler = new ScenarioScheduler(
                new FixedScenarioProvider(Scenario("scenario.no-condition")),
                new ScenarioPlaybackStore());

            var candidates = scheduler.GetLoopBoundaryCandidates(ScenarioTiming.Night, Context());

            Assert.IsEmpty(candidates);
        }

        [Test]
        public void Scheduler_RespectsExplicitLocationAndPriority()
        {
            var outing = Scenario("scenario.outing", priority: 5, triggerMode: ScenarioTriggerMode.Explicit);
            outing.Locations.Add(ScenarioExplicitLocation.CharacterOuting);
            outing.Conditions.Add(new ScenarioCondition { Key = ScenarioConditionKey.Tag, Value = "social", Comparison = ScenarioComparison.Exists });
            var boss = Scenario("scenario.boss", priority: 10, triggerMode: ScenarioTriggerMode.Explicit);
            boss.Locations.Add(ScenarioExplicitLocation.BossCall);
            boss.Conditions.Add(new ScenarioCondition { Key = ScenarioConditionKey.Tag, Value = "social", Comparison = ScenarioComparison.Exists });
            var scheduler = new ScenarioScheduler(new FixedScenarioProvider(outing, boss), new ScenarioPlaybackStore());

            var candidates = scheduler.GetExplicitCandidates(
                ScenarioTiming.Night,
                Context(tags: new[] { "social" }),
                ScenarioExplicitLocation.CharacterOuting);

            Assert.AreEqual("scenario.outing", candidates.Single().EventId);
        }

        [Test]
        public void Scheduler_StoresCompletionAndAppliesOneShotPolicy()
        {
            var scenario = Scenario("scenario.once");
            scenario.Conditions.Add(new ScenarioCondition { Key = ScenarioConditionKey.Tag, Value = "audit", Comparison = ScenarioComparison.Exists });
            var store = new ScenarioPlaybackStore();
            var scheduler = new ScenarioScheduler(new FixedScenarioProvider(scenario), store);

            scheduler.MarkCompleted(scenario, day: 1);
            var candidates = scheduler.GetLoopBoundaryCandidates(ScenarioTiming.Night, Context(day: 2, tags: new[] { "audit" }));

            Assert.IsEmpty(candidates);
            Assert.IsTrue(store.GetOrCreate("scenario.once").Completed);
        }

        [Test]
        public void Scheduler_AppliesCooldownUntilFutureDay()
        {
            var scenario = Scenario("scenario.cooldown");
            scenario.Replay.OneShot = false;
            scenario.Replay.CooldownDays = 2;
            scenario.Conditions.Add(new ScenarioCondition { Key = ScenarioConditionKey.Tag, Value = "audit", Comparison = ScenarioComparison.Exists });
            var scheduler = new ScenarioScheduler(new FixedScenarioProvider(scenario), new ScenarioPlaybackStore());

            scheduler.MarkSkipped(scenario, day: 3);
            var blocked = scheduler.GetLoopBoundaryCandidates(ScenarioTiming.Night, Context(day: 4, tags: new[] { "audit" }));
            var available = scheduler.GetLoopBoundaryCandidates(ScenarioTiming.Night, Context(day: 5, tags: new[] { "audit" }));

            Assert.IsEmpty(blocked);
            Assert.AreEqual("scenario.cooldown", available.Single().EventId);
        }

        [Test]
        public void PlaybackSession_ClickCompletesTypewriterBeforeAdvancingLine()
        {
            var scenario = Scenario("scenario.playback");
            scenario.MutableLines.Add(new ScenarioScriptLine { LineId = "L1", TextKey = "First line" });
            scenario.MutableLines.Add(new ScenarioScriptLine { LineId = "L2", TextKey = "Second line" });
            var session = new ScenarioPlaybackSession(scenario, "ko", "KR");

            session.AdvanceTypewriter(5);
            Assert.AreEqual("First", session.VisibleText);
            Assert.IsFalse(session.IsLineComplete);

            session.Click();
            Assert.AreEqual("First line", session.VisibleText);
            Assert.IsTrue(session.IsLineComplete);

            session.Click();
            Assert.AreEqual("", session.VisibleText);
            Assert.AreEqual("Second line", session.CurrentLine.Text);
            Assert.IsFalse(session.IsEventComplete);
        }

        [Test]
        public void PlaybackSession_AutoPlayStopsOnChoices()
        {
            var scenario = Scenario("scenario.choice");
            scenario.MutableLines.Add(new ScenarioScriptLine { LineId = "L1", TextKey = "Choose", Choices = new List<ScenarioChoice> { new() { ChoiceId = "yes" } } });
            scenario.MutableLines.Add(new ScenarioScriptLine { LineId = "L2", TextKey = "After choice" });
            var session = new ScenarioPlaybackSession(scenario, "ko", "KR");

            session.SetAutoPlay(true);
            session.TickAutoPlay();
            session.TickAutoPlay();

            Assert.AreEqual("Choose", session.VisibleText);
            Assert.AreEqual("L1", session.CurrentLine.Source.LineId);
        }

        [Test]
        public void PlaybackSession_LaysOutPortraitsByEqualBands()
        {
            var scenario = Scenario("scenario.portraits");
            scenario.MutableLines.Add(new ScenarioScriptLine
            {
                LineId = "L1",
                TextKey = "Line",
                PortraitIds = new List<string> { "A", "B", "C" }
            });

            var session = new ScenarioPlaybackSession(scenario, "ko", "KR");

            Assert.AreEqual(3, session.StageState.Portraits.Count);
            AssertFloat(0.25f, session.StageState.FindPortrait("A").NormalizedX);
            AssertFloat(0.5f, session.StageState.FindPortrait("B").NormalizedX);
            AssertFloat(0.75f, session.StageState.FindPortrait("C").NormalizedX);
        }

        [Test]
        public void PlaybackSession_MarksPortraitMovementWhenLineLayoutChanges()
        {
            var scenario = Scenario("scenario.move");
            scenario.MutableLines.Add(new ScenarioScriptLine
            {
                LineId = "L1",
                TextKey = "First",
                PortraitIds = new List<string> { "A", "B" }
            });
            scenario.MutableLines.Add(new ScenarioScriptLine
            {
                LineId = "L2",
                TextKey = "Second",
                PortraitIds = new List<string> { "A", "B", "C" }
            });
            var session = new ScenarioPlaybackSession(scenario, "ko", "KR");

            session.Click();
            session.Click();

            var a = session.StageState.FindPortrait("A");
            var b = session.StageState.FindPortrait("B");
            var c = session.StageState.FindPortrait("C");
            Assert.IsTrue(a.IsMoving);
            Assert.IsTrue(b.IsMoving);
            Assert.IsFalse(c.IsMoving);
            Assert.IsTrue(c.IsNewlyJoined);
            Assert.IsFalse(a.IsNewlyJoined);
            AssertFloat(1f / 3f, a.PreviousNormalizedX);
            AssertFloat(0.25f, a.NormalizedX);
            AssertFloat(2f / 3f, b.PreviousNormalizedX);
            AssertFloat(0.5f, b.NormalizedX);
        }

        [Test]
        public void PlaybackSession_DimsNonSpeakerPortraitsWhenSpeakerIsVisible()
        {
            var scenario = Scenario("scenario.focus");
            scenario.MutableLines.Add(new ScenarioScriptLine
            {
                LineId = "L1",
                SpeakerId = "B",
                TextKey = "Line",
                PortraitIds = new List<string> { "A", "B", "C" }
            });

            var session = new ScenarioPlaybackSession(scenario, "ko", "KR");

            Assert.IsTrue(session.StageState.FindPortrait("B").IsFocused);
            Assert.IsFalse(session.StageState.FindPortrait("B").IsDimmed);
            Assert.IsTrue(session.StageState.FindPortrait("A").IsDimmed);
            Assert.IsTrue(session.StageState.FindPortrait("C").IsDimmed);
        }

        private static ScenarioEventContext Context(int day = 1, params string[] tags)
        {
            return new ScenarioEventContext
            {
                Day = day,
                Slot = Slot.Evening,
                Tags = tags.ToList()
            };
        }

        private static FakeScenarioDefinition Scenario(
            string eventId,
            int priority = 0,
            ScenarioTriggerMode triggerMode = ScenarioTriggerMode.LoopBoundary)
        {
            return new FakeScenarioDefinition
            {
                EventIdValue = eventId,
                TimingValue = ScenarioTiming.Night,
                PriorityValue = priority,
                PlaybackStateKeyValue = eventId,
                TriggerModeValue = triggerMode
            };
        }

        private static void AssertFloat(float expected, float actual)
        {
            Assert.AreEqual(expected, actual, 0.0001f);
        }

        private sealed class FixedScenarioProvider : IScenarioEventProvider
        {
            private readonly List<IScenarioEventDefinition> scenarios;

            public FixedScenarioProvider(params IScenarioEventDefinition[] scenarios)
            {
                this.scenarios = scenarios.ToList();
            }

            public IEnumerable<IScenarioEventDefinition> GetEvents(ScenarioTiming timing, ScenarioEventContext context)
            {
                return scenarios.Where(scenario => scenario.Timing == timing);
            }
        }

        private sealed class FakeScenarioDefinition : IScenarioEventDefinition
        {
            public string EventIdValue = "";
            public ScenarioTiming TimingValue;
            public int PriorityValue;
            public string PlaybackStateKeyValue = "";
            public ScenarioTriggerMode TriggerModeValue;
            public List<ScenarioExplicitLocation> Locations = new();
            public List<ScenarioCondition> Conditions = new();
            public List<ScenarioStateEffect> Entry = new();
            public List<ScenarioStateEffect> Exit = new();
            public ScenarioReplayPolicy Replay = new();
            public List<ScenarioScriptLine> MutableLines = new();

            public string EventId => EventIdValue;
            public ScenarioTiming Timing => TimingValue;
            public int Priority => PriorityValue;
            public string PlaybackStateKey => PlaybackStateKeyValue;
            public ScenarioTriggerMode TriggerMode => TriggerModeValue;
            public IReadOnlyList<ScenarioExplicitLocation> AllowedExplicitLocations => Locations;
            public IReadOnlyList<ScenarioCondition> TriggerConditions => Conditions;
            public IReadOnlyList<ScenarioStateEffect> EntryCosts => Entry;
            public IReadOnlyList<ScenarioStateEffect> ExitEffects => Exit;
            public ScenarioReplayPolicy ReplayPolicy => Replay;
            public LocalizedTextTable TextTable => null;
            public IReadOnlyList<ScenarioScriptLine> Lines => MutableLines;

            public ScenarioResolvedLine ResolveLine(int index, string languageKey, string countryCode = "")
            {
                if (index < 0 || index >= MutableLines.Count)
                {
                    return new ScenarioResolvedLine(null, "");
                }

                var line = MutableLines[index];
                return new ScenarioResolvedLine(line, line.TextKey);
            }
        }
    }
}
