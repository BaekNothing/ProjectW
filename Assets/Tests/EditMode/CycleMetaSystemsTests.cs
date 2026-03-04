using NUnit.Framework;
using ProjectW.IngameCore.Config;
using ProjectW.IngameCore.Meta;
using System.Collections.Generic;

namespace ProjectW.Tests.EditMode
{
    public class CycleMetaSystemsTests
    {
        [Test]
        public void ChronicleSummarizer_IncludesRequiredCategoriesAndLimitsCount()
        {
            var events = new List<ChronicleEvent>
            {
                new ChronicleEvent { Category = ChronicleEventCategory.Progress, Description = "Routine stabilized", Severity = 1 },
                new ChronicleEvent { Category = ChronicleEventCategory.RelationshipChange, Description = "A-B trust dropped", Severity = 3 },
                new ChronicleEvent { Category = ChronicleEventCategory.Intervention, Description = "Intervention failed", Severity = 4, InterventionSucceeded = false },
                new ChronicleEvent { Category = ChronicleEventCategory.Termination, Description = "Terminated by timeout", Severity = 5, TerminationReason = "TIMEOUT" },
                new ChronicleEvent { Category = ChronicleEventCategory.Risk, Description = "Stress spike", Severity = 2 }
            };

            var summary = ChronicleSummarizer.Summarize(events, 3, 5);

            Assert.That(summary.KeyEvents.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(summary.KeyEvents.Count, Is.LessThanOrEqualTo(5));
            Assert.That(summary.RawSummary, Does.Contain("RelationshipChange"));
            Assert.That(summary.RawSummary, Does.Contain("Intervention"));
            Assert.That(summary.RawSummary, Does.Contain("Termination"));
        }

        [Test]
        public void MetaChoice_UpdatesSessionConfigAndCharacterBias()
        {
            var state = new SessionMetaState();
            state.ApplyChoice(MetaChoiceCatalog.ResolveOrDefault("meta.fast_tick"));
            state.ApplyChoice(MetaChoiceCatalog.ResolveOrDefault("meta.stress_guard"));

            var config = new SessionConfig { TickSeconds = 1f };
            state.ApplyToSessionConfig(config);

            Assert.AreEqual(0.9f, config.TickSeconds, 0.001f);
            Assert.AreEqual(10f, state.StressBiasDelta, 0.001f);
        }

        [Test]
        public void PlaytestSurvey_HasFixedFiveQuestionsAndReplaySignal()
        {
            var survey = new PlaytestSurveyForm();
            Assert.AreEqual(5, PlaytestSurveyForm.FixedQuestions.Count);

            survey.SetResponse(0, 5);
            survey.SetResponse(1, 4);
            survey.SetResponse(2, 4);
            survey.SetResponse(3, 3);
            survey.SetResponse(4, 5);

            Assert.IsTrue(survey.WantsReplay());
            Assert.That(survey.ToCompactText(), Does.Contain("Q1:5"));
        }
    }
}
