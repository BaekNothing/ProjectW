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

        public GameStartupContext(GameObject host, string patchVersion, string dataPath, Action markHealthy)
        {
            Host = host;
            PatchVersion = patchVersion;
            DataPath = dataPath;
            this.markHealthy = markHealthy;
        }

        public void MarkHealthy() => markHealthy?.Invoke();
    }

    public interface IGameEntry
    {
        void Start(GameStartupContext context);
    }
}
