using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProjectW.IngameMvp;

namespace ProjectW.Tests.EditMode
{
    public class IngameMvpRunnerTests
    {
        [Test]
        public void Core_StepsThroughRequiredOrder_AndEndsSession()
        {
            var config = new SessionConfig
            {
                sessionId = "test_session",
                tickSeconds = 2f,
                maxDecisionRetry = 3,
                maxPersistRetry = 3,
                persistRetryBackoffMs = 500
            };

            var rules = new List<StateTransitionRuleRow>
            {
                Rule(LoopState.Plan, LoopState.Drop),
                Rule(LoopState.Drop, LoopState.AutoNarrative),
                Rule(LoopState.AutoNarrative, LoopState.CaptainIntervention),
                Rule(LoopState.CaptainIntervention, LoopState.NightDream),
                Rule(LoopState.NightDream, LoopState.Resolve)
            };

            var terminations = new List<TerminationRuleRow>
            {
                new TerminationRuleRow
                {
                    ruleId = "TR-001",
                    conditionType = "ObjectiveComplete",
                    thresholdExpr = "tick_index>=5",
                    resultCode = "OBJ_DONE",
                    enabled = true,
                    priority = 1
                }
            };

            var core = new IngameMvpCore(config, rules, terminations);
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(core.Step(out var _));
            }

            Assert.AreEqual(LoopState.SessionEnd, core.CurrentState);
        }

        [Test]
        public void Core_RejectsInvalidTransition_WithStateError()
        {
            var config = new SessionConfig
            {
                sessionId = "test_session",
                tickSeconds = 2f,
                maxDecisionRetry = 3,
                maxPersistRetry = 3,
                persistRetryBackoffMs = 500
            };

            var rules = new List<StateTransitionRuleRow>
            {
                Rule(LoopState.Plan, LoopState.AutoNarrative)
            };

            var core = new IngameMvpCore(config, rules, new List<TerminationRuleRow>());
            var stepped = core.Step(out var errorCode);

            Assert.IsFalse(stepped);
            Assert.AreEqual("E-STATE-199", errorCode);
            Assert.AreEqual(LoopState.Plan, core.CurrentState);
        }

        [Test]
        public void CsvProvider_LoadsValidCsvSet()
        {
            string folder = CreateTempFolder();
            try
            {
                WriteAllRequiredCsv(folder, validCharacterHeader: true);
                var provider = new IngameCsvConfigProvider();

                var result = provider.LoadAll(folder);

                Assert.IsTrue(result.success);
                Assert.AreEqual(1, result.characters.Count);
                Assert.AreEqual(5, result.transitions.Count);
            }
            finally
            {
                Cleanup(folder);
            }
        }

        [Test]
        public void CsvProvider_ReturnsMissingColumnError_WhenRequiredColumnIsMissing()
        {
            string folder = CreateTempFolder();
            try
            {
                WriteAllRequiredCsv(folder, validCharacterHeader: false);
                var provider = new IngameCsvConfigProvider();

                var result = provider.LoadAll(folder);

                Assert.IsFalse(result.success);
                Assert.AreEqual("E-CSV-002", result.errorCode);
            }
            finally
            {
                Cleanup(folder);
            }
        }

        private static StateTransitionRuleRow Rule(LoopState from, LoopState to)
        {
            return new StateTransitionRuleRow
            {
                fromState = from,
                toState = to,
                entryCondition = "true",
                exitCondition = "true",
                guard = "true",
                priority = 1,
                enabled = true
            };
        }

        private static string CreateTempFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ProjectW_IngameMvpTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void WriteAllRequiredCsv(string folder, bool validCharacterHeader)
        {
            File.WriteAllText(Path.Combine(folder, "SessionConfig.csv"),
                "session_id,tick_seconds,max_decision_retry,max_persist_retry,persist_retry_backoff_ms\n" +
                "mvp_sample,2,3,3,500\n");

            File.WriteAllText(Path.Combine(folder, "StateTransitionRules.csv"),
                "from_state,to_state,entry_condition,exit_condition,guard,priority,enabled\n" +
                "Plan,Drop,true,true,true,1,true\n" +
                "Drop,AutoNarrative,true,true,true,1,true\n" +
                "AutoNarrative,CaptainIntervention,true,true,true,1,true\n" +
                "CaptainIntervention,NightDream,true,true,true,1,true\n" +
                "NightDream,Resolve,true,true,true,1,true\n");

            if (validCharacterHeader)
            {
                File.WriteAllText(Path.Combine(folder, "CharacterProfiles.csv"),
                    "character_id,trait_weights_json,stress,health,trauma_level,enabled\n" +
                    "char_001,{\"brave\":1},5,90,1,true\n");
            }
            else
            {
                File.WriteAllText(Path.Combine(folder, "CharacterProfiles.csv"),
                    "character_id,trait_weights_json,stress,trauma_level,enabled\n" +
                    "char_001,{\"brave\":1},5,1,true\n");
            }

            File.WriteAllText(Path.Combine(folder, "TerminationRules.csv"),
                "rule_id,condition_type,threshold_expr,result_code,enabled,priority\n" +
                "term_001,ObjectiveComplete,tick_index>=3,OBJ,true,1\n");

            File.WriteAllText(Path.Combine(folder, "InterventionCommands.csv"),
                "command_id,issued_tick,apply_tick,command_type,target_scope,payload_json,priority,supersedes_command_id\n" +
                "cmd_001,0,1,ObjectPriority,global,{},1,\n");
        }

        private static void Cleanup(string folder)
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
