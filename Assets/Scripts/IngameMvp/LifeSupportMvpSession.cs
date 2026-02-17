using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    public enum MvpLocation
    {
        ControlCenter,
        Cafeteria,
        Dormitory,
        ReturnRoute
    }

    public enum MvpSessionPhase
    {
        RepairingLifeSupport,
        Returning,
        Returned
    }

    [Serializable]
    public class LifeSupportMvpConfig
    {
        public int requiredRepairTicks = 50;
        public int halfDayTicks = 30;
        public int disasterCheckIntervalTicks = 10;
        public float disasterChance = 0.15f;
        public float disasterLossRate = 0.10f;
        public int returnTicks = 10;
    }

    public class LifeSupportMvpStepResult
    {
        public readonly List<string> logs = new List<string>();
        public bool disasterOccurred;
    }

    public class LifeSupportMvpCore
    {
        private readonly LifeSupportMvpConfig _config;
        private readonly System.Random _rng;

        public int TickIndex { get; private set; }
        public float RepairProgressTicks { get; private set; }
        public int ReturnProgressTicks { get; private set; }
        public int MealsTakenInHalfDay { get; private set; }
        public bool SleptInHalfDay { get; private set; }
        public MvpLocation CurrentLocation { get; private set; } = MvpLocation.ControlCenter;
        public MvpSessionPhase CurrentPhase { get; private set; } = MvpSessionPhase.RepairingLifeSupport;

        public bool IsComplete => CurrentPhase == MvpSessionPhase.Returned;

        public LifeSupportMvpCore(LifeSupportMvpConfig config, int seed)
        {
            _config = config;
            _rng = new System.Random(seed);
        }

        public LifeSupportMvpStepResult Step()
        {
            var result = new LifeSupportMvpStepResult();
            if (IsComplete)
            {
                result.logs.Add($"[MVP] Tick {TickIndex}: already returned.");
                return result;
            }

            TickIndex += 1;
            int slot = ((TickIndex - 1) % _config.halfDayTicks) + 1;

            if (CurrentPhase == MvpSessionPhase.RepairingLifeSupport)
            {
                ExecuteRoutine(slot, result);
                ExecuteDisasterCheckIfNeeded(result);
                ExecuteRepairCompletionCheck(result);
            }
            else if (CurrentPhase == MvpSessionPhase.Returning)
            {
                ExecuteReturningStep(result);
            }

            if (slot == _config.halfDayTicks)
            {
                result.logs.Add($"[MVP] Half-day summary: meals={MealsTakenInHalfDay}/3, slept={SleptInHalfDay}.");
                MealsTakenInHalfDay = 0;
                SleptInHalfDay = false;
            }

            return result;
        }

        private void ExecuteRoutine(int slot, LifeSupportMvpStepResult result)
        {
            if (slot == 8)
            {
                CurrentLocation = MvpLocation.Cafeteria;
                MealsTakenInHalfDay += 1;
                result.logs.Add($"[MVP] Tick {TickIndex}: breakfast at cafeteria.");
                return;
            }

            if (slot == 16)
            {
                CurrentLocation = MvpLocation.Cafeteria;
                MealsTakenInHalfDay += 1;
                result.logs.Add($"[MVP] Tick {TickIndex}: lunch at cafeteria.");
                return;
            }

            if (slot == 24)
            {
                CurrentLocation = MvpLocation.Cafeteria;
                MealsTakenInHalfDay += 1;
                result.logs.Add($"[MVP] Tick {TickIndex}: dinner at cafeteria.");
                return;
            }

            if (slot == 28)
            {
                CurrentLocation = MvpLocation.Dormitory;
                SleptInHalfDay = true;
                result.logs.Add($"[MVP] Tick {TickIndex}: sleep at dormitory.");
                return;
            }

            CurrentLocation = MvpLocation.ControlCenter;
            RepairProgressTicks += 1f;
            result.logs.Add($"[MVP] Tick {TickIndex}: repair at control center ({RepairProgressTicks:0.##}/{_config.requiredRepairTicks}).");
        }

        private void ExecuteDisasterCheckIfNeeded(LifeSupportMvpStepResult result)
        {
            if (TickIndex % _config.disasterCheckIntervalTicks != 0)
            {
                return;
            }

            float roll = (float)_rng.NextDouble();
            if (roll >= _config.disasterChance)
            {
                result.logs.Add($"[MVP] Tick {TickIndex}: disaster check passed (roll={roll:0.000}).");
                return;
            }

            result.disasterOccurred = true;
            float before = RepairProgressTicks;
            RepairProgressTicks = Mathf.Max(0f, RepairProgressTicks * (1f - _config.disasterLossRate));
            result.logs.Add($"[MVP] Tick {TickIndex}: DISASTER! progress lost 10% ({before:0.##} -> {RepairProgressTicks:0.##}).");
        }

        private void ExecuteRepairCompletionCheck(LifeSupportMvpStepResult result)
        {
            if (RepairProgressTicks < _config.requiredRepairTicks)
            {
                return;
            }

            CurrentPhase = MvpSessionPhase.Returning;
            CurrentLocation = MvpLocation.ReturnRoute;
            result.logs.Add($"[MVP] Tick {TickIndex}: life support repaired. return started (0/{_config.returnTicks}).");
        }

        private void ExecuteReturningStep(LifeSupportMvpStepResult result)
        {
            CurrentLocation = MvpLocation.ReturnRoute;
            ReturnProgressTicks += 1;
            result.logs.Add($"[MVP] Tick {TickIndex}: returning ({ReturnProgressTicks}/{_config.returnTicks}).");

            if (ReturnProgressTicks >= _config.returnTicks)
            {
                CurrentPhase = MvpSessionPhase.Returned;
                result.logs.Add($"[MVP] Tick {TickIndex}: RETURN COMPLETE.");
            }
        }
    }

    public class LifeSupportMvpSession : MonoBehaviour
    {
        [SerializeField] private bool autoRunOnStart = true;
        [SerializeField] private float tickIntervalSeconds = 0.1f;
        [SerializeField] private int randomSeed = 42;
        [SerializeField] private LifeSupportMvpConfig config = new LifeSupportMvpConfig();

        private LifeSupportMvpCore _core;
        private Coroutine _runningCoroutine;

        public LifeSupportMvpCore Core => _core;

        private void Start()
        {
            if (!autoRunOnStart)
            {
                return;
            }

            StartSession();
        }

        public void StartSession()
        {
            if (_core == null)
            {
                _core = new LifeSupportMvpCore(config, randomSeed);
                Debug.Log("[MVP] Session started: objective=Repair life support(50 ticks), disaster=15% each 10 ticks, return=10 ticks.");
            }

            if (_runningCoroutine == null)
            {
                _runningCoroutine = StartCoroutine(RunLoop());
            }
        }

        public LifeSupportMvpStepResult RunSingleTick()
        {
            if (_core == null)
            {
                _core = new LifeSupportMvpCore(config, randomSeed);
                Debug.Log("[MVP] Session started manually.");
            }

            var step = _core.Step();
            foreach (var log in step.logs)
            {
                Debug.Log(log);
            }

            return step;
        }

        private IEnumerator RunLoop()
        {
            while (_core != null && !_core.IsComplete)
            {
                yield return new WaitForSeconds(tickIntervalSeconds);
                RunSingleTick();
            }

            _runningCoroutine = null;
        }
    }
}
