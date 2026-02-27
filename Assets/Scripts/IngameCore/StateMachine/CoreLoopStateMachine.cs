using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.StateMachine
{
    public enum CoreLoopState
    {
        Plan,
        Drop,
        AutoNarrative,
        CaptainIntervention,
        NightDream,
        Resolve,
        NextCycle,
        SessionEnd
    }

    public readonly struct TransitionDecision
    {
        public CoreLoopState NextState { get; }
        public bool AppliedGuard { get; }
        public string ErrorCode { get; }

        public TransitionDecision(CoreLoopState nextState, bool appliedGuard, string errorCode = null)
        {
            NextState = nextState;
            AppliedGuard = appliedGuard;
            ErrorCode = errorCode;
        }
    }

    public static class CoreLoopStateMachine
    {
        private static readonly HashSet<(CoreLoopState from, CoreLoopState to)> InvalidTransitions = new HashSet<(CoreLoopState from, CoreLoopState to)>()
        {
            (CoreLoopState.Plan, CoreLoopState.AutoNarrative),
            (CoreLoopState.AutoNarrative, CoreLoopState.Resolve),
            (CoreLoopState.Resolve, CoreLoopState.Plan)
        };

        public static TransitionDecision EvaluateTransition(CoreLoopState currentState, CoreLoopState requestedState, bool guard)
        {
            if (!guard)
            {
                return new TransitionDecision(currentState, false, "E-STATE-101");
            }

            if (InvalidTransitions.Contains((currentState, requestedState)))
            {
                return new TransitionDecision(currentState, false, "E-STATE-199");
            }

            return new TransitionDecision(requestedState, true);
        }
    }
}
