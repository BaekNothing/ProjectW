using System;
using System.Collections.Generic;

namespace ProjectW.Outgame
{
    public enum MissionType
    {
        ResourceSweep = 0,
        Recon = 1,
        SafetyPatrol = 2
    }

    [Serializable]
    public sealed class OutgameSessionSetup
    {
        public List<string> SelectedCharacterIds = new List<string>();
        public MissionType InitialMissionType = MissionType.Recon;
        public int ResourcePriority = 50;
        public int SafetyPriority = 50;

        public OutgameSessionSetup Clone()
        {
            return new OutgameSessionSetup
            {
                SelectedCharacterIds = new List<string>(SelectedCharacterIds ?? new List<string>()),
                InitialMissionType = InitialMissionType,
                ResourcePriority = ResourcePriority,
                SafetyPriority = SafetyPriority
            };
        }

        public static OutgameSessionSetup CreateDefault()
        {
            return new OutgameSessionSetup
            {
                SelectedCharacterIds = new List<string> { "Character_A", "Character_B", "Character_C" },
                InitialMissionType = MissionType.Recon,
                ResourcePriority = 50,
                SafetyPriority = 50
            };
        }
    }

    [Serializable]
    public sealed class SessionResultSummary
    {
        public string TerminationReasonCode;
        public float MissionProgressRatio;
        public int SurvivingCharacterCount;
        public int TickIndex;
        public string SessionId;
    }

    public static class SessionFlowRuntimeContext
    {
        private static OutgameSessionSetup _pendingSetup;
        private static SessionResultSummary _lastResult;

        public static OutgameSessionSetup PendingSetup => _pendingSetup;
        public static SessionResultSummary LastResult => _lastResult;

        public static void SetPendingSetup(OutgameSessionSetup setup)
        {
            _pendingSetup = (setup ?? OutgameSessionSetup.CreateDefault()).Clone();
        }

        public static OutgameSessionSetup ConsumePendingSetupOrDefault()
        {
            var resolved = (_pendingSetup ?? OutgameSessionSetup.CreateDefault()).Clone();
            _pendingSetup = null;
            return resolved;
        }

        public static void ClearPendingSetup()
        {
            _pendingSetup = null;
        }

        public static void SetLastResult(SessionResultSummary result)
        {
            _lastResult = result;
        }

        public static void ClearLastResult()
        {
            _lastResult = null;
        }
    }
}
