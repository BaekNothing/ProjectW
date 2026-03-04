using System.Collections.Generic;

namespace ProjectW.IngameCore.Contracts
{
    public interface ICsvConfigProvider
    {
        ProjectW.IngameCore.Config.SessionConfig LoadSessionConfig();
        IReadOnlyList<ProjectW.IngameCore.Config.StateTransitionRuleRow> LoadStateTransitionRules();
        IReadOnlyList<ProjectW.IngameCore.Config.CharacterProfileRow> LoadCharacterProfiles();
        IReadOnlyList<ProjectW.IngameCore.Config.InterventionCommandRow> LoadInterventionRules();
        IReadOnlyList<ProjectW.IngameCore.Config.TerminationRuleRow> LoadTerminationRules();
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
