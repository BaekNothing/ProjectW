using System.Collections.Generic;
using NUnit.Framework;
using ProjectW.IngameCore.Config;
using ProjectW.IngameCore.Contracts;

namespace ProjectW.Tests.EditMode
{
    public class IngameCsvConfigTests
    {
        [Test]
        public void Bootstrap_LoadsInSsotOrder_AndStopsAfterFailure()
        {
            var provider = new OrderedFailureProvider(failAt: "TerminationRules");
            var bootstrap = new IngameCsvBootstrapService(provider, new StubSnapshotProbe(false));

            var result = bootstrap.Load();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CsvErrorCodes.TypeMismatch, result.ErrorCode);
            CollectionAssert.AreEqual(
                new[] { "SessionConfig", "StateTransitionRules", "CharacterProfiles", "TerminationRules" },
                provider.Calls);
        }

        [Test]
        public void Bootstrap_EntersReadOnlyRecoveryMode_WhenSnapshotExists()
        {
            var provider = new OrderedFailureProvider(failAt: "SessionConfig");
            var bootstrap = new IngameCsvBootstrapService(provider, new StubSnapshotProbe(true));

            var result = bootstrap.Load();

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.IsReadOnlyRecoveryMode);
            Assert.AreEqual(CsvErrorCodes.TypeMismatch, result.ErrorCode);
        }

        private sealed class StubSnapshotProbe : ISnapshotRecoveryProbe
        {
            private readonly bool hasSnapshot;

            public StubSnapshotProbe(bool hasSnapshot)
            {
                this.hasSnapshot = hasSnapshot;
            }

            public bool HasSnapshot(string sessionId)
            {
                return hasSnapshot;
            }
        }

        private sealed class OrderedFailureProvider : ICsvConfigProvider
        {
            private readonly string failAt;
            public List<string> Calls { get; } = new List<string>();

            public OrderedFailureProvider(string failAt)
            {
                this.failAt = failAt;
            }

            public SessionConfig LoadSessionConfig()
            {
                Calls.Add("SessionConfig");
                ThrowIfMatch("SessionConfig");
                return new SessionConfig { SessionId = "default", TickSeconds = 1f };
            }

            public IReadOnlyList<StateTransitionRuleRow> LoadStateTransitionRules()
            {
                Calls.Add("StateTransitionRules");
                ThrowIfMatch("StateTransitionRules");
                return new List<StateTransitionRuleRow>();
            }

            public IReadOnlyList<CharacterProfileRow> LoadCharacterProfiles()
            {
                Calls.Add("CharacterProfiles");
                ThrowIfMatch("CharacterProfiles");
                return new List<CharacterProfileRow>();
            }

            public IReadOnlyList<InterventionCommandRow> LoadInterventionRules()
            {
                Calls.Add("InterventionCommands");
                ThrowIfMatch("InterventionCommands");
                return new List<InterventionCommandRow>();
            }

            public IReadOnlyList<TerminationRuleRow> LoadTerminationRules()
            {
                Calls.Add("TerminationRules");
                ThrowIfMatch("TerminationRules");
                return new List<TerminationRuleRow>();
            }

            private void ThrowIfMatch(string key)
            {
                if (failAt == key)
                {
                    throw new CsvValidationException(CsvErrorCodes.TypeMismatch, key + " failed.");
                }
            }
        }
    }
}
