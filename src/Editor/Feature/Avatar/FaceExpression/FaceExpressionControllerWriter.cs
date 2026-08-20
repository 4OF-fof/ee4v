using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal enum FaceGesture
    {
        Neutral = 0,
        Fist = 1,
        Open = 2,
        Point = 3,
        Victory = 4,
        RockNRoll = 5,
        HandGun = 6,
        ThumbsUp = 7
    }

    internal static class FaceExpressionControllerWriter
    {
        internal const string LayerName = "ee4v Face Expressions";
        private const string GestureLeft = "GestureLeft";
        private const string GestureRight = "GestureRight";
        private const string StateTagPrefix = "ee4v-face:";
        private const float ClipLength = 1f / 60f;

        public static bool OwnsLayer(AnimatorController controller)
        {
            return controller != null && controller.layers.Any(layer => layer.name == LayerName);
        }

        public static IReadOnlyDictionary<FaceGesture, AnimationClip> Read(AnimatorController controller)
        {
            var result = new Dictionary<FaceGesture, AnimationClip>();
            if (controller == null)
            {
                return result;
            }

            var layer = controller.layers.FirstOrDefault(candidate => candidate.name == LayerName);
            if (layer?.stateMachine == null)
            {
                return result;
            }

            foreach (var child in layer.stateMachine.states)
            {
                var state = child.state;
                if (!TryReadTag(state.tag, out var gesture, out var sourceGuid))
                {
                    continue;
                }

                var sourcePath = string.IsNullOrEmpty(sourceGuid)
                    ? null
                    : AssetDatabase.GUIDToAssetPath(sourceGuid);
                var source = string.IsNullOrEmpty(sourcePath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);
                result[gesture] = source;
            }

            return result;
        }

        public static void Apply(
            AnimatorController controller,
            GameObject avatar,
            IReadOnlyDictionary<FaceGesture, AnimationClip> assignments,
            string outputFolder)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            assignments = assignments ?? new Dictionary<FaceGesture, AnimationClip>();
            EnsureParameter(controller, GestureLeft);
            EnsureParameter(controller, GestureRight);
            RemoveOwnedLayer(controller);

            var bindings = CollectBindings(assignments.Values);
            var assignedGestures = assignments
                .Where(pair => pair.Key != FaceGesture.Neutral && pair.Value != null)
                .Select(pair => pair.Key)
                .OrderBy(gesture => (int)gesture)
                .ToArray();
            var generatedGestures = assignedGestures
                .Prepend(FaceGesture.Neutral)
                .ToArray();
            var motions = new Dictionary<FaceGesture, AnimationClip>();
            foreach (var gesture in generatedGestures)
            {
                assignments.TryGetValue(gesture, out var source);
                motions[gesture] = CreateNormalizedClip(
                    controller,
                    avatar,
                    source,
                    bindings,
                    gesture,
                    outputFolder);
            }

            var stateMachine = new AnimatorStateMachine { name = LayerName };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);
            Undo.RegisterCreatedObjectUndo(stateMachine, "Create Face Expression Layer");
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = LayerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            });

            var neutralSource = GetAssignment(assignments, FaceGesture.Neutral);
            var neutral = AddState(
                stateMachine,
                FaceGesture.Neutral,
                motions[FaceGesture.Neutral],
                neutralSource);
            stateMachine.defaultState = neutral;

            foreach (var gesture in assignedGestures)
            {
                var state = AddState(stateMachine, gesture, motions[gesture], assignments[gesture]);
                AddAnyStateTransition(stateMachine, state, GestureLeft, gesture, null);
                AddAnyStateTransition(stateMachine, state, GestureRight, gesture, assignedGestures);

                var reset = state.AddTransition(neutral);
                ConfigureTransition(reset);
                reset.AddCondition(AnimatorConditionMode.NotEqual, (int)gesture, GestureLeft);
                reset.AddCondition(AnimatorConditionMode.NotEqual, (int)gesture, GestureRight);
            }

            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip GetAssignment(
            IReadOnlyDictionary<FaceGesture, AnimationClip> assignments,
            FaceGesture gesture)
        {
            assignments.TryGetValue(gesture, out var clip);
            return clip;
        }

        private static AnimatorState AddState(
            AnimatorStateMachine stateMachine,
            FaceGesture gesture,
            AnimationClip motion,
            AnimationClip source)
        {
            var state = stateMachine.AddState(((int)gesture).ToString("00") + " " + gesture);
            state.motion = motion;
            state.writeDefaultValues = false;
            state.tag = CreateTag(gesture, source);
            return state;
        }

        private static void AddAnyStateTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState state,
            string parameter,
            FaceGesture gesture,
            IReadOnlyList<FaceGesture> leftPriority)
        {
            var transition = stateMachine.AddAnyStateTransition(state);
            ConfigureTransition(transition);
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.Equals, (int)gesture, parameter);
            if (leftPriority == null)
            {
                return;
            }

            for (var index = 0; index < leftPriority.Count; index++)
            {
                transition.AddCondition(
                    AnimatorConditionMode.NotEqual,
                    (int)leftPriority[index],
                    GestureLeft);
            }
        }

        private static void ConfigureTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.offset = 0f;
        }

        private static void EnsureParameter(AnimatorController controller, string name)
        {
            var parameter = controller.parameters.FirstOrDefault(candidate => candidate.name == name);
            if (parameter != null)
            {
                if (parameter.type != AnimatorControllerParameterType.Int)
                {
                    throw new InvalidOperationException(name + " must be an Int parameter.");
                }

                return;
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Int);
        }

        private static void RemoveOwnedLayer(AnimatorController controller)
        {
            Undo.RegisterCompleteObjectUndo(controller, "Update Face Expression Layer");
            for (var index = controller.layers.Length - 1; index >= 0; index--)
            {
                var layer = controller.layers[index];
                if (layer.name != LayerName)
                {
                    continue;
                }

                controller.RemoveLayer(index);
                if (layer.stateMachine != null)
                {
                    Undo.DestroyObjectImmediate(layer.stateMachine);
                }
            }
        }

        private static IReadOnlyList<EditorCurveBinding> CollectBindings(IEnumerable<AnimationClip> clips)
        {
            var result = new Dictionary<string, EditorCurveBinding>(StringComparer.Ordinal);
            foreach (var clip in clips.Where(candidate => candidate != null))
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (IsBlendShape(binding))
                    {
                        result[BindingKey(binding)] = binding;
                    }
                }
            }

            return result.Values
                .OrderBy(binding => binding.path, StringComparer.Ordinal)
                .ThenBy(binding => binding.propertyName, StringComparer.Ordinal)
                .ToArray();
        }

        private static AnimationClip CreateNormalizedClip(
            AnimatorController controller,
            GameObject avatar,
            AnimationClip source,
            IReadOnlyList<EditorCurveBinding> bindings,
            FaceGesture gesture,
            string outputFolder)
        {
            var controllerPath = AssetDatabase.GetAssetPath(controller);
            var prefix = Path.GetFileNameWithoutExtension(controllerPath);
            var fileName = prefix + " " + ((int)gesture).ToString("00") + " " + gesture + ".anim";
            var assetPath = outputFolder.TrimEnd('/') + "/" + fileName;
            var generated = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (generated == null)
            {
                generated = new AnimationClip
                {
                    name = Path.GetFileNameWithoutExtension(assetPath),
                    frameRate = 60f
                };
                AssetDatabase.CreateAsset(generated, assetPath);
                Undo.RegisterCreatedObjectUndo(generated, "Create Face Expression Clip");
            }
            else
            {
                Undo.RecordObject(generated, "Update Face Expression Clip");
                foreach (var oldBinding in AnimationUtility.GetCurveBindings(generated))
                {
                    AnimationUtility.SetEditorCurve(generated, oldBinding, null);
                }
            }

            foreach (var binding in bindings)
            {
                var curve = source == null ? null : AnimationUtility.GetEditorCurve(source, binding);
                var value = curve == null ? GetDefaultValue(avatar, binding) : curve.Evaluate(0f);
                AnimationUtility.SetEditorCurve(
                    generated,
                    binding,
                    AnimationCurve.Constant(0f, ClipLength, value));
            }

            generated.frameRate = 60f;
            EditorUtility.SetDirty(generated);
            return generated;
        }

        private static float GetDefaultValue(GameObject avatar, EditorCurveBinding binding)
        {
            var target = string.IsNullOrEmpty(binding.path)
                ? avatar.transform
                : avatar.transform.Find(binding.path);
            var renderer = target == null ? null : target.GetComponent<SkinnedMeshRenderer>();
            var mesh = renderer == null ? null : renderer.sharedMesh;
            if (mesh == null)
            {
                return 0f;
            }

            var shapeName = binding.propertyName.Substring("blendShape.".Length);
            var index = mesh.GetBlendShapeIndex(shapeName);
            return index < 0 ? 0f : renderer.GetBlendShapeWeight(index);
        }

        private static bool IsBlendShape(EditorCurveBinding binding)
        {
            return binding.type == typeof(SkinnedMeshRenderer) &&
                   binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal);
        }

        private static string BindingKey(EditorCurveBinding binding)
        {
            return binding.path + "\n" + binding.propertyName;
        }

        private static string CreateTag(FaceGesture gesture, AnimationClip source)
        {
            var path = source == null ? null : AssetDatabase.GetAssetPath(source);
            var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            return StateTagPrefix + (int)gesture + ":" + guid;
        }

        private static bool TryReadTag(string tag, out FaceGesture gesture, out string sourceGuid)
        {
            gesture = FaceGesture.Neutral;
            sourceGuid = string.Empty;
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith(StateTagPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var parts = tag.Substring(StateTagPrefix.Length).Split(':');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out var rawGesture) ||
                !Enum.IsDefined(typeof(FaceGesture), rawGesture))
            {
                return false;
            }

            gesture = (FaceGesture)rawGesture;
            sourceGuid = parts[1];
            return true;
        }
    }
}
