using UnityEngine;

namespace ProjectW.IngameMvp
{
    [RequireComponent(typeof(Animator))]
    public sealed class RoutineCharacterAnimatorDriver : MonoBehaviour
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int CurrentActionHash = Animator.StringToHash("CurrentAction");
        private static readonly int IntendedActionHash = Animator.StringToHash("IntendedAction");
        private static readonly int FacingXHash = Animator.StringToHash("FacingX");

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void ApplyState(RoutineActionType currentAction, RoutineActionType intendedAction, bool isMoving, float speed, float facingX)
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_animator == null)
            {
                return;
            }

            // Avoid Unity warnings when no controller is bound yet.
            if (_animator.runtimeAnimatorController == null)
            {
                return;
            }

            _animator.SetBool(IsMovingHash, isMoving);
            _animator.SetFloat(SpeedHash, Mathf.Max(0f, speed));
            _animator.SetInteger(CurrentActionHash, (int)currentAction);
            _animator.SetInteger(IntendedActionHash, (int)intendedAction);
            _animator.SetFloat(FacingXHash, Mathf.Abs(facingX) < 0.0001f ? 1f : Mathf.Sign(facingX));
        }
    }
}
