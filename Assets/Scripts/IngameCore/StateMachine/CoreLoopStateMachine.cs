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

    public sealed class CoreLoopExecutionContext
    {
        public Action<CoreLoopState> EntryHook { get; }
        public Action<CoreLoopState> UpdateHook { get; }
        public Action<CoreLoopState> ExitHook { get; }
        public Func<CoreLoopState, CoreLoopState, bool> GuardHook { get; }
        public Action<CoreLoopState, CoreLoopState, string> OnFailHook { get; }

        public CoreLoopExecutionContext(
            Action<CoreLoopState> entryHook = null,
            Action<CoreLoopState> updateHook = null,
            Action<CoreLoopState> exitHook = null,
            Func<CoreLoopState, CoreLoopState, bool> guardHook = null,
            Action<CoreLoopState, CoreLoopState, string> onFailHook = null)
        {
            EntryHook = entryHook;
            UpdateHook = updateHook;
            ExitHook = exitHook;
            GuardHook = guardHook;
            OnFailHook = onFailHook;
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

        public static TransitionDecision EvaluateTransition(
            CoreLoopState currentState,
            CoreLoopState requestedState,
            CoreLoopExecutionContext context)
        {
            context?.UpdateHook?.Invoke(currentState);
            bool guard = context?.GuardHook?.Invoke(currentState, requestedState) ?? true;

            var decision = EvaluateTransition(currentState, requestedState, guard);
            if (!string.IsNullOrEmpty(decision.ErrorCode))
            {
                context?.OnFailHook?.Invoke(currentState, requestedState, decision.ErrorCode);
                return decision;
            }

            if (decision.NextState != currentState)
            {
                context?.ExitHook?.Invoke(currentState);
                context?.EntryHook?.Invoke(decision.NextState);
            }

            return decision;
        }
    }
}
