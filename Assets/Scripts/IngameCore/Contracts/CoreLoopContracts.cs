using System.Collections.Generic;
using ProjectW.IngameCore.Config;

namespace ProjectW.IngameCore.Contracts
{
    public interface ICsvConfigProvider
    {
        SessionConfig LoadSessionConfig();
        IReadOnlyList<StateTransitionRuleRow> LoadStateTransitionRules();
        IReadOnlyList<CharacterProfileRow> LoadCharacterProfiles();
        IReadOnlyList<InterventionCommandRow> LoadInterventionRules();
        IReadOnlyList<TerminationRuleRow> LoadTerminationRules();
    }

    public interface IDataSnapshotProvider
    {
        object LoadSnapshot(string sessionId);
    }

    public interface ISeedProvider
    {
        int GetSeed(string sessionId, int tick);
    }
}
