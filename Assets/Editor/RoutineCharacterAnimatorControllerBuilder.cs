#if UNITY_EDITOR
using System.IO;
using ProjectW.IngameMvp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectW.EditorTools
{
    public static class RoutineCharacterAnimatorControllerBuilder
    {
        private const string OutputDir = "Assets/Resources/AnimatorControllers";
        private const string ClipsDir = "Assets/Resources/AnimatorControllers/Clips";
        private const string ControllerPath = OutputDir + "/routine_character_default.controller";

        [MenuItem("ProjectW/Animation/Create Default Character Animator Controller")]
        public static void CreateOrUpdateDefaultController()
        {
            EnsureDirectories();

            var idleClip = EnsureClip("routine_idle");
            var moveClip = EnsureClip("routine_move");
            var workClip = EnsureClip("routine_work");
            var eatClip = EnsureClip("routine_eat");
            var sleepClip = EnsureClip("routine_sleep");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            controller.parameters = new[]
            {
                new AnimatorControllerParameter { name = "IsMoving", type = AnimatorControllerParameterType.Bool, defaultBool = false },
                new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 0f },
                new AnimatorControllerParameter { name = "CurrentAction", type = AnimatorControllerParameterType.Int, defaultInt = (int)RoutineActionType.Move },
                new AnimatorControllerParameter { name = "IntendedAction", type = AnimatorControllerParameterType.Int, defaultInt = (int)RoutineActionType.Move },
                new AnimatorControllerParameter { name = "FacingX", type = AnimatorControllerParameterType.Float, defaultFloat = 1f }
            };

            var rootStateMachine = controller.layers[0].stateMachine;
            rootStateMachine.states = new ChildAnimatorState[0];
            rootStateMachine.anyStateTransitions = new AnimatorStateTransition[0];

            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;
            var moveState = rootStateMachine.AddState("Move");
            moveState.motion = moveClip;
            var workState = rootStateMachine.AddState("Work");
            workState.motion = workClip;
            var eatState = rootStateMachine.AddState("Eat");
            eatState.motion = eatClip;
            var sleepState = rootStateMachine.AddState("Sleep");
            sleepState.motion = sleepClip;
            rootStateMachine.defaultState = idleState;

            AddActionTransition(rootStateMachine, moveState, RoutineActionType.Move);
            AddActionTransition(rootStateMachine, workState, RoutineActionType.Mission);
            AddActionTransition(rootStateMachine, eatState, RoutineActionType.Eat);
            AddActionTransition(rootStateMachine, eatState, RoutineActionType.Breakfast);
            AddActionTransition(rootStateMachine, eatState, RoutineActionType.Lunch);
            AddActionTransition(rootStateMachine, eatState, RoutineActionType.Dinner);
            AddActionTransition(rootStateMachine, sleepState, RoutineActionType.Sleep);

            var toIdle = rootStateMachine.AddAnyStateTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.05f;
            toIdle.canTransitionToSelf = false;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            toIdle.AddCondition(AnimatorConditionMode.Equals, (int)RoutineActionType.Move, "CurrentAction");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectW] Default character animator controller generated at " + ControllerPath);
        }

        private static void AddActionTransition(AnimatorStateMachine root, AnimatorState state, RoutineActionType action)
        {
            var transition = root.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.Equals, (int)action, "CurrentAction");
        }

        private static AnimationClip EnsureClip(string clipName)
        {
            var path = ClipsDir + "/" + clipName + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = clipName };
                AssetDatabase.CreateAsset(clip, path);
            }

            ConfigureClipCurves(clip, clipName);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void ConfigureClipCurves(AnimationClip clip, string clipName)
        {
            if (clip == null)
            {
                return;
            }

            clip.ClearCurves();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var lower = clipName.ToLowerInvariant();
            if (lower.Contains("move"))
            {
                var curve = new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.12f, 0.92f),
                    new Keyframe(0.24f, 1.08f),
                    new Keyframe(0.36f, 1f));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.y"), curve);
                return;
            }

            if (lower.Contains("work"))
            {
                var curve = new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.25f, 1.04f),
                    new Keyframe(0.5f, 1f));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.y"), curve);
                return;
            }

            if (lower.Contains("eat"))
            {
                var curve = new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.18f, 0.95f),
                    new Keyframe(0.36f, 1f));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.y"), curve);
                return;
            }

            if (lower.Contains("sleep"))
            {
                var curve = AnimationCurve.Constant(0f, 0.5f, 0.84f);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.y"), curve);
                return;
            }

            var idleCurve = AnimationCurve.Constant(0f, 0.5f, 1f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.y"), idleCurve);
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }

            if (!Directory.Exists(ClipsDir))
            {
                Directory.CreateDirectory(ClipsDir);
            }
        }
    }
}
#endif
