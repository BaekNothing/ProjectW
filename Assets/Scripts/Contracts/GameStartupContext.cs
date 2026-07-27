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

        public GameStartupContext(GameObject host, string patchVersion, string dataPath, IStringStorage storage, Action markHealthy)
        {
            Host = host;
            PatchVersion = patchVersion;
            DataPath = dataPath;
            Storage = storage;
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
}
