using System.Collections.Generic;

namespace ProjectW.IngameCore.Contracts
{
    public interface ICsvConfigProvider
    {
        object LoadSessionConfig();
        IReadOnlyList<object> LoadStateTransitionRules();
        IReadOnlyList<object> LoadInterventionRules();
        IReadOnlyList<object> LoadTerminationRules();
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
