using System;
using UnityEngine;

namespace ProjectW.Contracts
{
    public sealed class GameStartupContext
    {
        private readonly Action markHealthy;
        public GameObject Host { get; }
        public string PatchVersion { get; }
        public string DataPath { get; }
        public IStringStorage Storage { get; }
        public IPatchDiagnostics PatchDiagnostics { get; }

        public GameStartupContext(
            GameObject host,
            string patchVersion,
            string dataPath,
            IStringStorage storage,
            IPatchDiagnostics patchDiagnostics,
            Action markHealthy)
        {
            Host = host;
            PatchVersion = patchVersion;
            DataPath = dataPath;
            Storage = storage;
            PatchDiagnostics = patchDiagnostics;
            this.markHealthy = markHealthy;
        }

        public void MarkHealthy() => markHealthy?.Invoke();
    }

    public interface IGameEntry
    {
        void Start(GameStartupContext context);
    }

    public interface IStringStorage
    {
        bool TryGetString(string key, out string value);
        void SetString(string key, string value);
        void DeleteKey(string key);
    }

    public interface IPatchDiagnostics
    {
        string ActiveVersion { get; }
        string InstalledVersion { get; }
        string Status { get; }
        string LastPatchResult { get; }
        PatchDiagnosticEntry[] GetLogs();
        void ClearLogs();
    }

    public sealed class PatchDiagnosticEntry
    {
        public string Type { get; }
        public string Message { get; }
        public string StackTrace { get; }

        public PatchDiagnosticEntry(string type, string message, string stackTrace)
        {
            Type = type;
            Message = message;
            StackTrace = stackTrace;
        }
    }
}
