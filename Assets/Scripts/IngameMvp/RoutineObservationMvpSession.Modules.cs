using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProjectW.IngameCore;
using ProjectW.IngameCore.Config;
using ProjectW.IngameCore.StateMachine;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectW.IngameMvp
{
    public sealed class CoreLoopResolveContext
    {
        public RoutineActionType Action;
        public string ZoneName;
        public CoreLoopState RequestedNextState = CoreLoopState.NextCycle;
    }

    public sealed class CoreLoopOrchestrator
    {
        public void Execute(
            int hour,
            int minute,
            ref RoutineActionType defaultAction,
            ref string zoneName,
            Func<CoreLoopState> currentState,
            Action<CoreLoopState, CoreLoopState, Func<bool>> executeState,
            Func<bool> handlePlan,
            Func<bool> handleDrop,
            Func<bool> handleAutoNarrative,
            Func<bool> handleCaptainIntervention,
            Func<bool> handleNightDream,
            Func<int, int, CoreLoopResolveContext, bool> handleResolve,
            Func<bool> handleNextCycle,
            Action stopSession)
        {
            var resolveContext = new CoreLoopResolveContext
            {
                Action = defaultAction,
                ZoneName = zoneName
            };

            executeState(CoreLoopState.Plan, CoreLoopState.Drop, handlePlan);
            executeState(CoreLoopState.Drop, CoreLoopState.AutoNarrative, handleDrop);
            executeState(CoreLoopState.AutoNarrative, CoreLoopState.CaptainIntervention, handleAutoNarrative);
            executeState(CoreLoopState.CaptainIntervention, CoreLoopState.NightDream, handleCaptainIntervention);
            executeState(CoreLoopState.NightDream, CoreLoopState.Resolve, handleNightDream);

            if (currentState() == CoreLoopState.Resolve)
            {
                var resolveGuard = handleResolve(hour, minute, resolveContext);
                executeState(CoreLoopState.Resolve, resolveContext.RequestedNextState, () => resolveGuard);
            }

            if (resolveContext.RequestedNextState == CoreLoopState.NextCycle)
            {
                executeState(CoreLoopState.NextCycle, CoreLoopState.Plan, handleNextCycle);
            }
            else if (currentState() == CoreLoopState.SessionEnd)
            {
                stopSession();
            }

            defaultAction = resolveContext.Action;
            zoneName = resolveContext.ZoneName;
        }
    }

    public sealed class CharacterActionResolver
    {
        private readonly float _gaugeMax;

        public CharacterActionResolver(float gaugeMax)
        {
            _gaugeMax = gaugeMax;
        }

        public void ApplyNeedsAndProgress(
            RoutineCharacterBinding binding,
            RoutineActionType action,
            bool canResolveNeed,
            bool canPerformAction,
            Action<RoutineCharacterBinding, int> applyDynamicSubtaskProgress)
        {
            if (action != RoutineActionType.Move && !canPerformAction)
            {
                action = RoutineActionType.Move;
            }

            switch (action)
            {
                case RoutineActionType.Move:
                    binding.hunger -= binding.hungerDecayPerTick * 0.5f;
                    binding.sleep -= binding.sleepDecayPerTick * 0.35f;
                    binding.stress -= binding.stressDecayPerTick * 0.3f;
                    break;
                case RoutineActionType.Mission:
                    binding.hunger -= binding.hungerDecayPerTick;
                    binding.sleep -= binding.sleepDecayPerTick;
                    binding.stress -= binding.stressDecayPerTick;
                    if (canResolveNeed)
                    {
                        binding.missionTicks += 1;
                        applyDynamicSubtaskProgress(binding, 1);
                        if (binding.missionTicks >= 100)
                        {
                            binding.completedWorkCount += 1;
                            binding.missionTicks = 0;
                            Debug.Log(string.Format(
                                CultureInfo.InvariantCulture,
                                "[RoutineWork] actor={0} completed={1} -> assigned_new_work",
                                binding.actor != null ? binding.actor.name : "Unknown",
                                binding.completedWorkCount));
                        }
                    }
                    break;
                case RoutineActionType.Sleep:
                    binding.hunger -= binding.hungerDecayPerTick * 0.35f;
                    if (canResolveNeed)
                    {
                        binding.sleep += binding.sleepRecoverPerSleep;
                        binding.stress += binding.stressRecoverPerSleep;
                    }
                    break;
                default:
                    if (canResolveNeed)
                    {
                        binding.hunger += binding.hungerRecoverPerMeal;
                        binding.stress += binding.stressRecoverPerMeal;
                    }
                    binding.sleep -= binding.sleepDecayPerTick * 0.5f;
                    break;
            }

            binding.hunger = Mathf.Clamp(binding.hunger, 0f, _gaugeMax);
            binding.sleep = Mathf.Clamp(binding.sleep, 0f, _gaugeMax);
            binding.stress = Mathf.Clamp(binding.stress, 0f, _gaugeMax);
        }
    }

    public sealed class SessionEndPersistenceFacade
    {
        public bool Evaluate(
            IReadOnlyList<RoutineCharacterBinding> characters,
            float missionProgressRatio,
            string recentRejectedInterventionReason,
            IngameCsvConfigSet runtimeConfigSet,
            Func<SessionEndResult, SessionSnapshotDto> buildSessionSnapshot,
            Action<string, string> setDashboardContext,
            Action<string> emitSessionEndedOnce,
            Action stopSession,
            out SessionEndResult sessionEndResult,
            out SnapshotPersistenceResult persistenceResult,
            out bool sessionEndRequested)
        {
            bool objectiveComplete = missionProgressRatio >= 1f;
            bool totalWipe = characters.Count > 0;
            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                var active = binding.hunger > 0.01f || binding.sleep > 0.01f || binding.stress > 0.01f;
                if (active)
                {
                    totalWipe = false;
                    break;
                }
            }

            bool emergencyExtract = recentRejectedInterventionReason == "emergency_extract";
            sessionEndResult = SessionEndResolver.ResolveSessionEnd(totalWipe, emergencyExtract, objectiveComplete);
            persistenceResult = default;
            if (!sessionEndResult.IsEnd)
            {
                sessionEndRequested = false;
                return true;
            }

            var runtimeConfig = runtimeConfigSet?.SessionConfig;
            var persistenceConfig = new ProjectW.IngameCore.SessionConfig(
                runtimeConfig != null ? runtimeConfig.MaxPersistRetry : 0,
                runtimeConfig != null ? runtimeConfig.PersistRetryBackoffMs : 0);

            var snapshot = buildSessionSnapshot(sessionEndResult);
            var writer = new JsonSnapshotWriter(maxSnapshotsPerSession: 3);
            var service = new SnapshotPersistenceService(writer);
            persistenceResult = service.PersistWithRetry(snapshot, persistenceConfig);

            if (!persistenceResult.Success)
            {
                setDashboardContext("Persistence", $"{persistenceResult.State}:{persistenceResult.ErrorCode}");
                emitSessionEndedOnce("PERSISTENCE_ERROR");
                stopSession();
                sessionEndRequested = false;
                return false;
            }

            setDashboardContext("Termination", sessionEndResult.EndReasonCode);
            setDashboardContext("Persistence", persistenceResult.State.ToString());
            emitSessionEndedOnce(null);
            sessionEndRequested = true;
            return true;
        }
    }

    public sealed class IngameDashboardPresenter
    {
        public void Update(
            int dayIndex,
            int halfDayIndex,
            int tickInHalfDay,
            string timeText,
            Text goalText,
            Text progressText,
            Text situationText,
            Text currentTimeText,
            string dashboardGoalTitle,
            int dashboardMissionGoalTicks,
            int totalMissionTicks,
            float missionProgressRatio,
            int movingCount,
            int characterCount,
            float avgHunger,
            float avgSleep,
            float avgStress,
            float avgPerformance,
            string situationSummary,
            Action updateChronicleUi)
        {
            var percentage = missionProgressRatio * 100f;

            if (goalText != null)
            {
                goalText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Goal: {0} ({1}/{2})",
                    dashboardGoalTitle,
                    totalMissionTicks,
                    Mathf.Max(1, dashboardMissionGoalTicks));
            }

            if (progressText != null)
            {
                progressText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Progress: {0:0}% | Move:{1}/{2} | Avg H/S/T: {3:0}/{4:0}/{5:0} | Perf:{6:0.0}",
                    percentage,
                    movingCount,
                    characterCount,
                    avgHunger,
                    avgSleep,
                    avgStress,
                    avgPerformance);
            }

            if (situationText != null)
            {
                situationText.text = situationSummary;
            }

            if (currentTimeText != null)
            {
                currentTimeText.text = timeText;
            }

            updateChronicleUi();
        }

        public string BuildSituationSummary(
            int pendingInterventionCount,
            int lastAppliedInterventionTick,
            string recentRejectedInterventionReason,
            IReadOnlyDictionary<string, string> dashboardContext)
        {
            var summary = new StringBuilder(256);
            summary.AppendFormat(
                CultureInfo.InvariantCulture,
                "Interventions pending:{0}, lastApplied:{1}, latestReject:{2}",
                pendingInterventionCount,
                lastAppliedInterventionTick < 0 ? "N/A" : lastAppliedInterventionTick.ToString(CultureInfo.InvariantCulture),
                recentRejectedInterventionReason);

            if (dashboardContext != null && dashboardContext.TryGetValue("FactionEvents", out var factionSummary) && !string.IsNullOrWhiteSpace(factionSummary))
            {
                summary.AppendLine();
                summary.AppendFormat(CultureInfo.InvariantCulture, "Faction: {0}", factionSummary);
            }

            if (dashboardContext != null && dashboardContext.Count > 0)
            {
                var orderedKeys = new List<string>(dashboardContext.Keys);
                orderedKeys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < orderedKeys.Count; i++)
                {
                    var key = orderedKeys[i];
                    if (string.Equals(key, "Chronicle", StringComparison.Ordinal)
                        || string.Equals(key, "FactionEvents", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    summary.AppendLine();
                    summary.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1}", key, dashboardContext[key]);
                }
            }

            return summary.ToString();
        }
    }
}
