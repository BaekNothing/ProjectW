using System;
using ProjectW.IngameCore.CaseReview;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    public sealed class CaseReviewSessionController : MonoBehaviour
    {
        [SerializeField] private int seed = 1042;
        [SerializeField] private bool autoInitializeOnAwake = true;
        [SerializeField] private bool useTimePressure;
        [SerializeField] private CaseReviewDatabase database;

        private GameState _state;
        private DispatchResult _lastResult;
        private string _lastOutput = string.Empty;

        public GameState State => _state;
        public DispatchResult LastResult => _lastResult;
        public string LastOutput => _lastOutput;
        public bool IsInitialized => _state != null;

        private void Awake()
        {
            if (autoInitializeOnAwake)
            {
                Initialize();
            }
        }

        public void Initialize()
        {
            Initialize(seed);
        }

        public void Initialize(int initialSeed)
        {
            var config = new GameConfig
            {
                UseTimePressure = useTimePressure,
                InitialData = ResolveDatabase()?.ToSeedData()
            };

            _state = CaseReviewGame.Init(config, initialSeed);
            _lastResult = null;
            _lastOutput = string.Empty;
        }

        public DispatchResult DispatchCommand(string command, int wallclockDeltaSec = 0)
        {
            EnsureInitialized();
            _lastResult = CaseReviewGame.Dispatch(_state, command ?? string.Empty, wallclockDeltaSec);
            _lastOutput = string.Join(Environment.NewLine, _lastResult.Lines);
            return _lastResult;
        }

        public string Snapshot()
        {
            EnsureInitialized();
            return CaseReviewGame.Snapshot(_state);
        }

        public void RestoreSnapshot(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Snapshot json must not be empty.", nameof(json));
            }

            _state = CaseReviewGame.Restore(json);
            _lastResult = null;
            _lastOutput = string.Empty;
        }

        private void EnsureInitialized()
        {
            if (_state == null)
            {
                Initialize();
            }
        }

        private CaseReviewDatabase ResolveDatabase()
        {
            if (database != null)
            {
                return database;
            }

            database = Resources.Load<CaseReviewDatabase>("CaseReview/CaseReviewDatabase");
            return database;
        }
    }
}
