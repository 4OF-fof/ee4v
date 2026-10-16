using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core;
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

    internal readonly struct GestureCombination : IEquatable<GestureCombination>
    {
        public GestureCombination(FaceGesture left, FaceGesture right)
        {
            Left = left;
            Right = right;
        }

        public FaceGesture Left { get; }
        public FaceGesture Right { get; }

        public bool Equals(GestureCombination other)
        {
            return Left == other.Left && Right == other.Right;
        }

        public override bool Equals(object obj)
        {
            return obj is GestureCombination other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Left * 397) ^ (int)Right;
        }
    }

    internal readonly struct FaceExpressionAssignment
    {
        public FaceExpressionAssignment(
            AnimationClip clip,
            bool enableBlink = true,
            bool fixMouth = false,
            string menuName = null)
        {
            Clip = clip;
            EnableBlink = enableBlink;
            FixMouth = fixMouth;
            MenuName = menuName ?? string.Empty;
        }

        public AnimationClip Clip { get; }
        public bool EnableBlink { get; }
        public bool FixMouth { get; }
        public string MenuName { get; }
        public bool IsDefault => Clip == null && EnableBlink && !FixMouth;

        public static FaceExpressionAssignment Default =>
            new FaceExpressionAssignment(null);
    }

    internal sealed class FaceExpressionMenuEntry
    {
        public FaceExpressionMenuEntry(string name, FaceExpressionAssignment assignment)
        {
            Name = name ?? string.Empty;
            Assignment = assignment;
        }

        public string Name { get; set; }
        public FaceExpressionAssignment Assignment { get; set; }
    }

    internal sealed class FaceExpressionConfiguration
    {
        public FaceExpressionConfiguration(
            IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> assignments,
            IReadOnlyList<FaceExpressionMenuEntry> menuEntries)
        {
            Assignments = assignments ??
                new Dictionary<GestureCombination, FaceExpressionAssignment>();
            MenuEntries = menuEntries ?? Array.Empty<FaceExpressionMenuEntry>();
        }

        public IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> Assignments { get; }
        public IReadOnlyList<FaceExpressionMenuEntry> MenuEntries { get; }
    }

    internal sealed class FaceExpressionAvatarBindings
    {
        public FaceExpressionAvatarBindings(
            IReadOnlyList<EditorCurveBinding> blink,
            IReadOnlyList<EditorCurveBinding> eyes,
            IReadOnlyList<EditorCurveBinding> mouth,
            IReadOnlyList<EditorCurveBinding> mouthMorph = null)
        {
            Blink = blink ?? Array.Empty<EditorCurveBinding>();
            Eyes = eyes ?? Array.Empty<EditorCurveBinding>();
            Mouth = mouth ?? Array.Empty<EditorCurveBinding>();
            MouthMorph = mouthMorph ?? Array.Empty<EditorCurveBinding>();
        }

        public IReadOnlyList<EditorCurveBinding> Blink { get; }
        public IReadOnlyList<EditorCurveBinding> Eyes { get; }
        public IReadOnlyList<EditorCurveBinding> Mouth { get; }
        public IReadOnlyList<EditorCurveBinding> MouthMorph { get; }

        public static FaceExpressionAvatarBindings Empty { get; } =
            new FaceExpressionAvatarBindings(null, null, null);
    }

    internal static class GestureMatrixControllerWriter
    {
        private static readonly Dictionary<string, Hash128> MotionSignatures = new Dictionary<string, Hash128>();
        internal const string LayerName = "ee4v Face Expressions";
        internal const string MenuParameter = "ee4v/FaceExpression";
        private const string VoiceParameter = "Voice";
        private const string GestureLeft = "GestureLeft";
        private const string GestureRight = "GestureRight";
        private const string StateTagPrefix = "ee4v-face:5:";
        private const string MenuTagPrefix = "ee4v-menu:3:";
        private const float VoiceThreshold = 0.01f;
        private const float VoiceThresholdStep = 0.0001f;
        private const int BlinkCountPerCycle = 12;
        private const float BlinkMinimumIntervalSeconds = 3f;
        private const float BlinkMaximumIntervalSeconds = 8f;
        private const float BlinkTransitionSeconds = 0.1f;
        private const float BlinkClosedSeconds = 0.05f;
        private static readonly string[] TrackingControlTypeNames =
        {
            "VRC.SDKBase.VRC_AnimatorTrackingControl",
            "VRC.SDK3.Avatars.Components.VRCAnimatorTrackingControl"
        };
        private static readonly Type TrackingControlType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => TrackingControlTypeNames.Select(name => assembly.GetType(name, false)))
            .FirstOrDefault(candidate => candidate != null);
        public static bool OwnsLayer(AnimatorController controller)
        {
            return controller != null && controller.layers.Any(layer => layer.name == LayerName);
        }

        public static IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> Read(
            AnimatorController controller)
        {
            return ReadMatrix(controller);
        }

        public static IReadOnlyList<FaceExpressionMenuEntry> ReadMenuEntries(
            AnimatorController controller)
        {
            var result = new List<FaceExpressionMenuEntry>();
            if (controller == null)
            {
                return result;
            }

            var layer = controller.layers.FirstOrDefault(
                candidate => candidate.name == LayerName);
            ReadMenuEntries(layer?.stateMachine, result);
            return result;
        }

        private static void ReadMenuEntries(
            AnimatorStateMachine stateMachine,
            ICollection<FaceExpressionMenuEntry> result)
        {
            if (stateMachine == null)
            {
                return;
            }

            foreach (var child in stateMachine.states.OrderBy(
                         item => item.state.name))
            {
                if (!TryReadMenuTag(
                        child.state.tag,
                        out var entry,
                        out var isExplicit))
                {
                    continue;
                }

                if (isExplicit)
                {
                    result.Add(entry);
                }
            }
        }

        public static IReadOnlyList<EffectiveMenuEntry> GetEffectiveMenuEntries(
            IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> assignments,
            IReadOnlyList<FaceExpressionMenuEntry> menuEntries)
        {
            return BuildEffectiveMenuEntries(assignments, menuEntries);
        }

        public static void Apply(
            AnimatorController controller,
            GameObject avatar,
            IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> assignments,
            IReadOnlyList<FaceExpressionMenuEntry> menuEntries,
            FaceExpressionAvatarBindings avatarBindings,
            string outputFolder,
            bool saveAssets = true)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            assignments = assignments ??
                new Dictionary<GestureCombination, FaceExpressionAssignment>();
            menuEntries = menuEntries ?? Array.Empty<FaceExpressionMenuEntry>();
            avatarBindings = avatarBindings ?? FaceExpressionAvatarBindings.Empty;
            EnsureParameter(controller, GestureLeft);
            EnsureParameter(controller, GestureRight);
            EnsureParameter(controller, MenuParameter);
            var hasMouthMorphs = avatarBindings.MouthMorph.Count > 0;
            if (hasMouthMorphs)
            {
                EnsureParameter(
                    controller,
                    VoiceParameter,
                    AnimatorControllerParameterType.Float);
            }
            var configured = new Dictionary<GestureCombination, FaceExpressionAssignment>();
            foreach (FaceGesture left in Enum.GetValues(typeof(FaceGesture)))
            {
                foreach (FaceGesture right in Enum.GetValues(typeof(FaceGesture)))
                {
                    var combination = new GestureCombination(left, right);
                    configured.Add(
                        combination,
                        GetAssignment(assignments, combination));
                }
            }

            var neutralKey = new GestureCombination(FaceGesture.Neutral, FaceGesture.Neutral);
            var neutralAssignment = configured[neutralKey];
            var effectiveAssignments = configured.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.IsDefault
                    ? neutralAssignment
                    : pair.Value);
            var effectiveMenuEntries = BuildEffectiveMenuEntries(assignments, menuEntries);
            var previousMachine = controller.layers.FirstOrDefault(layer => layer.name == LayerName)?.stateMachine;
            var previousStates = previousMachine == null ? new Dictionary<string, AnimatorState>() :
                previousMachine.states.Where(child => child.state != null).Select(child => child.state)
                    .GroupBy(state => state.name).ToDictionary(group => group.Key, group => group.First());
            var gestureStateNames = new HashSet<string>(configured.Keys.Select(GestureStateName));
            var reuseGraph = !FaceExpressionGenerationUndo.Enabled && previousMachine != null &&
                previousMachine.states.Length == previousStates.Count &&
                previousMachine.anyStateTransitions.Length == previousStates.Count &&
                gestureStateNames.All(previousStates.ContainsKey) &&
                previousStates.Values.All(state => gestureStateNames.Contains(state.name) || MenuStateValue(state) > 0);
            if (!reuseGraph) RemoveOwnedLayer(controller);
            var allAssignments = effectiveAssignments.Values
                .Concat(effectiveMenuEntries.Select(item => item.Entry.Assignment));
            var bindings = CollectBindings(
                avatar,
                allAssignments,
                avatarBindings);
            var motions = new Dictionary<GestureCombination, Motion>();
            var normalizedMotions =
                new Dictionary<NormalizedMotionKey, AnimationClip>();
            var mouthMotions = new Dictionary<AnimationClip, Motion>();
            var usedGeneratedClipPaths = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (var pair in Order(configured))
            {
                var assignment = effectiveAssignments[pair.Key];
                var normalized = GetOrCreateNormalizedMotion(
                    controller,
                    avatar,
                    assignment,
                    bindings,
                    avatarBindings,
                    outputFolder,
                    normalizedMotions,
                    usedGeneratedClipPaths);
                motions[pair.Key] = GetOrCreateMouthMotion(
                    controller,
                    avatar,
                    normalized,
                    assignment,
                    avatarBindings.MouthMorph,
                    outputFolder,
                    mouthMotions,
                    usedGeneratedClipPaths);
            }

            var stateMachine = reuseGraph ? previousMachine : new AnimatorStateMachine { name = LayerName };
            if (!reuseGraph)
            {
                AssetDatabase.AddObjectToAsset(stateMachine, controller);
                FaceExpressionGenerationUndo.RegisterCreatedObjectUndo(stateMachine, "Create Face Expression Layer");
                FaceExpressionGenerationUndo.AddLayer(controller, new AnimatorControllerLayer
                {
                    name = LayerName, defaultWeight = 1f, stateMachine = stateMachine
                });
            }

            var neutral = AddState(
                stateMachine,
                neutralKey,
                motions[neutralKey],
                configured[neutralKey],
                effectiveAssignments[neutralKey], reuseGraph ? previousStates[GestureStateName(neutralKey)] : null);
            stateMachine.defaultState = neutral;

            foreach (var pair in Order(configured).Where(pair => !pair.Key.Equals(neutralKey)))
            {
                var state = AddState(
                    stateMachine,
                    pair.Key,
                    motions[pair.Key],
                    pair.Value,
                    effectiveAssignments[pair.Key], reuseGraph ? previousStates[GestureStateName(pair.Key)] : null);
                if (!reuseGraph) AddAnyStateTransition(stateMachine, state, pair.Key);
            }

            if (!reuseGraph) AddAnyStateTransition(stateMachine, neutral, neutralKey);
            if (reuseGraph) RemoveUnusedMenuStates(stateMachine, effectiveMenuEntries.Count);

            if (effectiveMenuEntries.Count > 0)
            {
                AddMenuStates(
                    stateMachine,
                    neutral,
                    controller,
                    avatar,
                    effectiveMenuEntries,
                    bindings,
                    avatarBindings,
                    outputFolder,
                    normalizedMotions,
                    mouthMotions,
                    usedGeneratedClipPaths, reuseGraph);
            }

            var usedTrees = new HashSet<Motion>(mouthMotions.Values);
            foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller))
                         .OfType<BlendTree>().Where(tree => tree.name.StartsWith("ee4v Mouth Morph ", StringComparison.Ordinal)))
            {
                if (!usedTrees.Contains(tree)) FaceExpressionGenerationUndo.DestroyObjectImmediate(tree);
            }

            DeleteUnusedGeneratedClips(
                controller,
                outputFolder,
                usedGeneratedClipPaths);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
            foreach (var path in usedGeneratedClipPaths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (saveAssets) AssetDatabase.SaveAssetIfDirty(clip);
                else DeferredAssetSave.Queue(clip);
            }
            if (saveAssets) AssetDatabase.SaveAssetIfDirty(controller);
            else DeferredAssetSave.Queue(controller);
        }

        private static Dictionary<GestureCombination, FaceExpressionAssignment> ReadMatrix(
            AnimatorController controller)
        {
            var result = new Dictionary<GestureCombination, FaceExpressionAssignment>();
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
                if (TryReadTag(child.state.tag, out var combination, out var assignment))
                {
                    result[combination] = assignment;
                }
            }

            return result;
        }

        private static IEnumerable<KeyValuePair<GestureCombination, FaceExpressionAssignment>> Order(
            IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> assignments)
        {
            return assignments.OrderBy(pair => (int)pair.Key.Left)
                .ThenBy(pair => (int)pair.Key.Right);
        }

        private static FaceExpressionAssignment GetAssignment(
            IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> assignments,
            GestureCombination combination)
        {
            return assignments.TryGetValue(combination, out var assignment)
                ? assignment
                : FaceExpressionAssignment.Default;
        }

        private static AnimatorState AddState(
            AnimatorStateMachine stateMachine,
            GestureCombination combination,
            Motion motion,
            FaceExpressionAssignment storedAssignment,
            FaceExpressionAssignment effectiveAssignment,
            AnimatorState existingState = null)
        {
            var name = GestureStateName(combination);
            var state = existingState ?? FaceExpressionGenerationUndo.AddState(stateMachine, name);
            UpdateState(state, name, motion, CreateTag(combination, storedAssignment), effectiveAssignment);
            return state;
        }

        private static void UpdateState(AnimatorState state, string name, Motion motion, string tag,
            FaceExpressionAssignment assignment)
        {
            if (state.name != name || state.motion != motion || state.writeDefaultValues || state.tag != tag)
            {
                FaceExpressionGenerationUndo.RecordObject(state, "Update Face Expression State");
                state.name = name;
                state.motion = motion;
                state.writeDefaultValues = false;
                state.tag = tag;
                EditorUtility.SetDirty(state);
            }
            ConfigureTracking(state, assignment);
        }

        private static int MenuStateValue(AnimatorState state) => state.name.Length > 5 && state.name[0] == 'M' &&
            state.name[4] == ' ' && state.tag.StartsWith(MenuTagPrefix, StringComparison.Ordinal) &&
            int.TryParse(state.name.Substring(1, 3), out var value) ? value : 0;

        private static void RemoveUnusedMenuStates(AnimatorStateMachine machine, int count)
        {
            var removed = new HashSet<AnimatorState>(machine.states.Select(child => child.state)
                .Where(state => MenuStateValue(state) > count));
            if (removed.Count == 0) return;
            var transitions = machine.anyStateTransitions;
            machine.anyStateTransitions = transitions.Where(transition => !removed.Contains(transition.destinationState)).ToArray();
            machine.states = machine.states.Where(child => !removed.Contains(child.state)).ToArray();
            foreach (var transition in transitions.Where(transition => removed.Contains(transition.destinationState)))
                FaceExpressionGenerationUndo.DestroyObjectImmediate(transition);
            foreach (var state in removed)
            {
                foreach (var transition in state.transitions) FaceExpressionGenerationUndo.DestroyObjectImmediate(transition);
                foreach (var behaviour in state.behaviours) FaceExpressionGenerationUndo.DestroyObjectImmediate(behaviour);
                FaceExpressionGenerationUndo.DestroyObjectImmediate(state);
            }
        }

        private static string GestureStateName(GestureCombination combination) =>
            ((int)combination.Left).ToString("00") + "-" + ((int)combination.Right).ToString("00") + " " +
            combination.Left + " + " + combination.Right;

        private static void AddAnyStateTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState state,
            GestureCombination combination)
        {
            var transition = FaceExpressionGenerationUndo.AddAnyStateTransition(stateMachine, state);
            ConfigureTransition(transition);
            transition.canTransitionToSelf = false;
            FaceExpressionGenerationUndo.AddCondition(transition,
                AnimatorConditionMode.Equals,
                (int)combination.Left,
                GestureLeft);
            FaceExpressionGenerationUndo.AddCondition(transition,
                AnimatorConditionMode.Equals,
                (int)combination.Right,
                GestureRight);
            FaceExpressionGenerationUndo.AddCondition(transition, AnimatorConditionMode.Equals, 0f, MenuParameter);
        }

        private static void AddMenuStates(
            AnimatorStateMachine stateMachine,
            AnimatorState neutral,
            AnimatorController controller,
            GameObject avatar,
            IReadOnlyList<EffectiveMenuEntry> entries,
            IReadOnlyList<EditorCurveBinding> bindings,
            FaceExpressionAvatarBindings avatarBindings,
            string outputFolder,
            IDictionary<NormalizedMotionKey, AnimationClip> normalizedMotions,
            IDictionary<AnimationClip, Motion> mouthMotions,
            ISet<string> usedGeneratedClipPaths,
            bool reuseGraph = false)
        {
            var previousMenuStates = reuseGraph ? stateMachine.states.Select(child => child.state)
                .Where(state => MenuStateValue(state) > 0).ToDictionary(MenuStateValue) : new Dictionary<int, AnimatorState>();
            for (var index = 0; index < entries.Count; index++)
            {
                var value = index + 1;
                var item = entries[index];
                var normalized = GetOrCreateNormalizedMotion(
                    controller,
                    avatar,
                    item.Entry.Assignment,
                    bindings,
                    avatarBindings,
                    outputFolder,
                    normalizedMotions,
                    usedGeneratedClipPaths);
                var motion = GetOrCreateMouthMotion(
                    controller,
                    avatar,
                    normalized,
                    item.Entry.Assignment,
                    avatarBindings.MouthMorph,
                    outputFolder,
                    mouthMotions,
                    usedGeneratedClipPaths);
                var prefix = "M" + value.ToString("000") + " ";
                var exists = previousMenuStates.TryGetValue(value, out var state);
                if (!exists) state = FaceExpressionGenerationUndo.AddState(stateMachine, prefix + item.Entry.Name);
                UpdateState(state, prefix + item.Entry.Name, motion, CreateMenuTag(item), item.Entry.Assignment);
                if (exists) continue;

                var activate = FaceExpressionGenerationUndo.AddAnyStateTransition(stateMachine, state);
                ConfigureTransition(activate);
                activate.canTransitionToSelf = false;
                FaceExpressionGenerationUndo.AddCondition(activate, AnimatorConditionMode.Equals, value, MenuParameter);

                var reset = FaceExpressionGenerationUndo.AddTransition(state, neutral);
                ConfigureTransition(reset);
                FaceExpressionGenerationUndo.AddCondition(reset, AnimatorConditionMode.Equals, 0f, MenuParameter);
            }
        }

        private static void ConfigureTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.offset = 0f;
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type = AnimatorControllerParameterType.Int)
        {
            var parameter = controller.parameters.FirstOrDefault(candidate => candidate.name == name);
            if (parameter != null)
            {
                if (parameter.type != type)
                {
                    throw new InvalidOperationException(
                        name + " must be a " + type + " parameter.");
                }

                return;
            }

            FaceExpressionGenerationUndo.AddParameter(controller, name, type);
        }

        private static void RemoveOwnedLayer(AnimatorController controller)
        {
            FaceExpressionGenerationUndo.RegisterCompleteObjectUndo(controller, "Update Face Expression Layer");
            for (var index = controller.layers.Length - 1; index >= 0; index--)
            {
                var layer = controller.layers[index];
                if (layer.name != LayerName)
                {
                    continue;
                }

                FaceExpressionGenerationUndo.RemoveLayer(controller, index);
                if (FaceExpressionGenerationUndo.Enabled && layer.stateMachine != null)
                {
                    FaceExpressionGenerationUndo.DestroyObjectImmediate(layer.stateMachine);
                }
            }
        }

        private static IReadOnlyList<EditorCurveBinding> CollectBindings(
            GameObject avatar,
            IEnumerable<FaceExpressionAssignment> assignments,
            FaceExpressionAvatarBindings avatarBindings)
        {
            var result = new Dictionary<string, EditorCurveBinding>(StringComparer.Ordinal);
            AddBindings(result, avatarBindings.Blink);
            AddBindings(result, avatarBindings.Eyes);
            AddBindings(result, avatarBindings.Mouth);
            AddBindings(result, avatarBindings.MouthMorph);
            foreach (var clip in assignments.Select(assignment => assignment.Clip).Where(clip => clip != null).Distinct())
            {
                AddBindings(result, AnimationUtility.GetCurveBindings(clip));
            }

            var rendererPaths = new HashSet<string>(
                result.Values
                    .Where(IsBlendShape)
                    .Select(binding => binding.path),
                StringComparer.Ordinal);
            var body = FaceExpressionClipEditor.FindBodyRenderer(avatar);
            if (body != null)
            {
                rendererPaths.Add(AnimationUtility.CalculateTransformPath(
                    body.transform,
                    avatar.transform));
            }

            foreach (var rendererPath in rendererPaths)
            {
                AddRendererBindings(
                    result,
                    avatar,
                    rendererPath);
            }

            return result.Values
                .OrderBy(binding => binding.path, StringComparer.Ordinal)
                .ThenBy(binding => binding.propertyName, StringComparer.Ordinal)
                .ToArray();
        }

        private static void AddBindings(
            IDictionary<string, EditorCurveBinding> result,
            IEnumerable<EditorCurveBinding> bindings)
        {
            foreach (var binding in bindings.Where(IsBlendShape))
            {
                result[BindingKey(binding)] = binding;
            }
        }

        private static void AddRendererBindings(
            IDictionary<string, EditorCurveBinding> result,
            GameObject avatar,
            string rendererPath)
        {
            var target = string.IsNullOrEmpty(rendererPath)
                ? avatar.transform
                : avatar.transform.Find(rendererPath);
            var renderer = target == null
                ? null
                : target.GetComponent<SkinnedMeshRenderer>();
            var mesh = renderer == null ? null : renderer.sharedMesh;
            if (mesh == null)
            {
                return;
            }

            for (var index = 0; index < mesh.blendShapeCount; index++)
            {
                var binding = EditorCurveBinding.FloatCurve(
                    rendererPath,
                    typeof(SkinnedMeshRenderer),
                    "blendShape." + mesh.GetBlendShapeName(index));
                result[BindingKey(binding)] = binding;
            }
        }

        private static AnimationClip CreateNormalizedClip(
            AnimatorController controller,
            GameObject avatar,
            FaceExpressionAssignment assignment,
            IReadOnlyList<EditorCurveBinding> bindings,
            FaceExpressionAvatarBindings avatarBindings,
            string identity,
            string outputFolder)
        {
            var controllerPath = AssetDatabase.GetAssetPath(controller);
            var prefix = Path.GetFileNameWithoutExtension(controllerPath);
            var assetPath = outputFolder.TrimEnd('/') + "/" + prefix + " " + identity + ".anim";
            var generated = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            var defaultValues = bindings.Select(binding => GetDefaultValue(avatar, binding)).ToArray();
            var signature = Hash128.Compute(avatar.GetInstanceID() + ":" +
                (assignment.Clip == null ? "" : EditorJsonUtility.ToJson(assignment.Clip)) + ":" +
                assignment.EnableBlink + ":" + assignment.FixMouth + ":" +
                string.Join(";", bindings.Select((binding, index) => BindingKey(binding) + ":" +
                    defaultValues[index].ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + ":" +
                string.Join(";", avatarBindings.Blink.Select(BindingKey)) + ":" +
                string.Join(";", avatarBindings.Eyes.Select(BindingKey)) + ":" +
                string.Join(";", avatarBindings.Mouth.Select(BindingKey)));
            if (generated != null && MotionSignatures.TryGetValue(assetPath, out var saved) && saved == signature)
                return generated;
            var created = generated == null;
            if (created)
            {
                generated = new AnimationClip
                {
                    name = Path.GetFileNameWithoutExtension(assetPath),
                    frameRate = 60f
                };
            }
            else
            {
                FaceExpressionGenerationUndo.RecordObject(generated, "Update Face Expression Clip");
            }

            var previousBindings = AnimationUtility.GetCurveBindings(generated);
            var generatedBindings = new List<EditorCurveBinding>(bindings.Count + previousBindings.Length);
            var generatedCurves = new List<AnimationCurve>(bindings.Count + previousBindings.Length);

            var blinkKeys = new HashSet<string>(
                avatarBindings.Blink.Select(BindingKey),
                StringComparer.Ordinal);
            var eyeKeys = new HashSet<string>(
                avatarBindings.Eyes.Select(BindingKey),
                StringComparer.Ordinal);
            var mouthKeys = new HashSet<string>(
                avatarBindings.Mouth.Select(BindingKey),
                StringComparer.Ordinal);
            var animateBlink = assignment.EnableBlink && blinkKeys.Count > 0;
            var blinkSchedule = animateBlink ? CreateBlinkSchedule() : null;
            var sourceLooping = assignment.Clip != null &&
                                AnimationUtility.GetAnimationClipSettings(
                                    assignment.Clip).loopTime;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                var key = BindingKey(binding);
                if (!assignment.FixMouth && mouthKeys.Contains(key))
                {
                    continue;
                }

                var curve = assignment.Clip == null
                    ? null
                    : AnimationUtility.GetEditorCurve(assignment.Clip, binding);
                var value = curve == null ? defaultValues[index] : curve.Evaluate(0f);
                AnimationCurve generatedCurve;
                if (animateBlink && blinkKeys.Contains(key))
                {
                    generatedCurve = CreateBlinkCurve(
                        curve,
                        curve == null ? 0f : Mathf.Clamp(value, 0f, 100f),
                        100f,
                        blinkSchedule);
                }
                else if (animateBlink && curve != null && eyeKeys.Contains(key))
                {
                    generatedCurve = CreateBlinkCurve(
                        curve,
                        value,
                        0f,
                        blinkSchedule);
                }
                else
                {
                    generatedCurve = curve == null
                        ? new AnimationCurve(new Keyframe(0f, value))
                        : CopyCurve(curve);
                }

                generatedBindings.Add(binding);
                generatedCurves.Add(generatedCurve);
            }
            var retainedBindings = new HashSet<EditorCurveBinding>(generatedBindings);
            foreach (var previous in previousBindings)
            {
                if (retainedBindings.Contains(previous)) continue;
                generatedBindings.Add(previous);
                generatedCurves.Add(null);
            }
            AnimationUtility.SetEditorCurves(generated, generatedBindings.ToArray(), generatedCurves.ToArray());

            var settings = AnimationUtility.GetAnimationClipSettings(generated);
            settings.loopTime = animateBlink || sourceLooping;
            AnimationUtility.SetAnimationClipSettings(generated, settings);
            generated.frameRate = 60f;
            if (created)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                FaceExpressionGenerationUndo.RegisterCreatedObjectUndo(generated, "Create Face Expression Clip");
            }
            else EditorUtility.SetDirty(generated);
            MotionSignatures[assetPath] = signature;
            return generated;
        }

        private static BlinkSchedule CreateBlinkSchedule()
        {
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            var starts = new float[BlinkCountPerCycle];
            var pulseDuration = BlinkTransitionSeconds * 2f +
                                BlinkClosedSeconds + 1f / 60f;
            starts[0] = BlinkMinimumIntervalSeconds +
                        (BlinkMaximumIntervalSeconds -
                         BlinkMinimumIntervalSeconds -
                         pulseDuration) *
                        (float)random.NextDouble();
            for (var index = 1; index < starts.Length; index++)
            {
                starts[index] = starts[index - 1] + RandomBlinkInterval(random);
            }

            var minimumWrapInterval = starts[0] + pulseDuration;
            var wrapInterval = minimumWrapInterval +
                               (BlinkMaximumIntervalSeconds - minimumWrapInterval) *
                               (float)random.NextDouble();
            return new BlinkSchedule(
                starts,
                starts[starts.Length - 1] + wrapInterval - starts[0]);
        }

        private static float RandomBlinkInterval(System.Random random)
        {
            return BlinkMinimumIntervalSeconds +
                   (BlinkMaximumIntervalSeconds - BlinkMinimumIntervalSeconds) *
                   (float)random.NextDouble();
        }

        private static AnimationCurve CreateBlinkCurve(
            AnimationCurve source,
            float openValue,
            float closedValue,
            BlinkSchedule schedule)
        {
            var baseCurve = source == null
                ? new AnimationCurve(new Keyframe(0f, openValue))
                : CopyCurve(source);
            var keys = source == null || IsConstantCurve(source)
                ? new List<Keyframe>()
                : source.keys.ToList();
            SetKey(keys, 0f, baseCurve.Evaluate(0f));
            foreach (var closeStart in schedule.CloseStarts)
            {
                var closedStart = closeStart + BlinkTransitionSeconds;
                var openStart = closedStart + BlinkClosedSeconds;
                SetKey(keys, closeStart, baseCurve.Evaluate(closeStart));
                SetKey(keys, closedStart, closedValue);
                SetKey(keys, openStart, closedValue);
                SetKey(
                    keys,
                    openStart + BlinkTransitionSeconds,
                    baseCurve.Evaluate(openStart + BlinkTransitionSeconds));
            }

            SetKey(keys, schedule.Duration, baseCurve.Evaluate(schedule.Duration));
            var curve = new AnimationCurve(
                keys.OrderBy(key => key.time).ToArray())
            {
                preWrapMode = baseCurve.preWrapMode,
                postWrapMode = baseCurve.postWrapMode
            };
            for (var index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(
                    curve,
                    index,
                    AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(
                    curve,
                    index,
                    AnimationUtility.TangentMode.Linear);
            }

            return curve;
        }

        private static bool IsConstantCurve(AnimationCurve curve)
        {
            if (curve == null || curve.length < 2)
            {
                return true;
            }

            var value = curve.keys[0].value;
            return curve.keys.All(key => Mathf.Approximately(key.value, value));
        }

        private static AnimationCurve CopyCurve(AnimationCurve source)
        {
            return new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
        }

        private static void SetKey(
            IList<Keyframe> keys,
            float time,
            float value)
        {
            for (var index = 0; index < keys.Count; index++)
            {
                if (!Mathf.Approximately(keys[index].time, time))
                {
                    continue;
                }

                var key = keys[index];
                key.value = value;
                keys[index] = key;
                return;
            }

            keys.Add(new Keyframe(time, value));
        }

        private sealed class BlinkSchedule
        {
            internal BlinkSchedule(float[] closeStarts, float duration)
            {
                CloseStarts = closeStarts;
                Duration = duration;
            }

            internal IReadOnlyList<float> CloseStarts { get; }
            internal float Duration { get; }
        }

        private static void ConfigureTracking(
            AnimatorState state,
            FaceExpressionAssignment assignment)
        {
            var type = TrackingControlType;
            if (type == null)
            {
                return;
            }

            var behaviour = state.behaviours.FirstOrDefault(candidate => candidate != null && candidate.GetType() == type) ??
                FaceExpressionGenerationUndo.AddBehaviour(state, type);
            SetTrackingField(
                behaviour,
                "trackingEyes",
                assignment.EnableBlink ? "Tracking" : "Animation");
            SetTrackingField(
                behaviour,
                "trackingMouth",
                assignment.FixMouth ? "Animation" : "Tracking");
        }

        private static void SetTrackingField(
            StateMachineBehaviour behaviour,
            string fieldName,
            string value)
        {
            var field = behaviour.GetType().GetField(fieldName);
            if (field == null || !field.FieldType.IsEnum)
            {
                return;
            }

            var next = Enum.Parse(field.FieldType, value);
            if (Equals(field.GetValue(behaviour), next)) return;
            FaceExpressionGenerationUndo.RecordObject(behaviour, "Update Face Expression Tracking");
            field.SetValue(behaviour, next);
            EditorUtility.SetDirty(behaviour);
        }

        private static AnimationClip GetOrCreateNormalizedMotion(
            AnimatorController controller,
            GameObject avatar,
            FaceExpressionAssignment assignment,
            IReadOnlyList<EditorCurveBinding> bindings,
            FaceExpressionAvatarBindings avatarBindings,
            string outputFolder,
            IDictionary<NormalizedMotionKey, AnimationClip> motions,
            ISet<string> usedGeneratedClipPaths)
        {
            var key = new NormalizedMotionKey(assignment);
            if (!motions.TryGetValue(key, out var motion))
            {
                motion = CreateNormalizedClip(
                    controller,
                    avatar,
                    assignment,
                    bindings,
                    avatarBindings,
                    "Motion " + key.Identity,
                    outputFolder);
                motions.Add(key, motion);
            }

            usedGeneratedClipPaths.Add(AssetDatabase.GetAssetPath(motion));
            return motion;
        }

        private static Motion GetOrCreateMouthMotion(
            AnimatorController controller,
            GameObject avatar,
            AnimationClip normalized,
            FaceExpressionAssignment assignment,
            IReadOnlyList<EditorCurveBinding> mouthMorphBindings,
            string outputFolder,
            IDictionary<AnimationClip, Motion> motions,
            ISet<string> usedGeneratedClipPaths)
        {
            if (assignment.FixMouth || mouthMorphBindings.Count == 0)
            {
                return normalized;
            }

            if (motions.TryGetValue(normalized, out var motion))
            {
                return motion;
            }

            var neutralized = CreateMouthNeutralizedClip(
                avatar,
                normalized,
                mouthMorphBindings,
                outputFolder);
            usedGeneratedClipPaths.Add(AssetDatabase.GetAssetPath(neutralized));

            var treeName = "ee4v Mouth Morph " + normalized.name;
            var tree = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller))
                .OfType<BlendTree>().FirstOrDefault(candidate => candidate.name == treeName);
            if (tree == null)
            {
                tree = new BlendTree
                {
                    name = treeName, blendType = BlendTreeType.Simple1D,
                    blendParameter = VoiceParameter, useAutomaticThresholds = false
                };
                tree.AddChild(normalized, 0f);
                tree.AddChild(normalized, VoiceThreshold);
                tree.AddChild(neutralized, VoiceThreshold + VoiceThresholdStep);
                tree.AddChild(neutralized, 1f);
                AssetDatabase.AddObjectToAsset(tree, controller);
                FaceExpressionGenerationUndo.RegisterCreatedObjectUndo(tree, "Create Mouth Morph Blend Tree");
                EditorUtility.SetDirty(tree);
            }
            motions.Add(normalized, tree);
            return tree;
        }

        private static AnimationClip CreateMouthNeutralizedClip(
            GameObject avatar,
            AnimationClip source,
            IReadOnlyList<EditorCurveBinding> bindings,
            string outputFolder)
        {
            var assetPath = outputFolder.TrimEnd('/') + "/" + source.name +
                            " Mouth Neutral.anim";
            var generated = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            var defaultValues = bindings.Select(binding => GetDefaultValue(avatar, binding)).ToArray();
            var signature = Hash128.Compute(EditorJsonUtility.ToJson(source) + ":" +
                string.Join(";", bindings.Select((binding, index) => BindingKey(binding) + ":" +
                    defaultValues[index].ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
            if (generated != null && MotionSignatures.TryGetValue(assetPath, out var saved) && saved == signature)
                return generated;
            var created = generated == null;
            if (created)
            {
                generated = new AnimationClip();
            }
            else
            {
                FaceExpressionGenerationUndo.RecordObject(generated, "Update Mouth Neutral Face Expression Clip");
            }

            EditorUtility.CopySerialized(source, generated);
            generated.name = Path.GetFileNameWithoutExtension(assetPath);
            AnimationUtility.SetEditorCurves(generated, bindings.ToArray(), defaultValues.Select(value =>
                AnimationCurve.Constant(0f, Mathf.Max(1f / generated.frameRate, source.length), value)).ToArray());

            if (created)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                FaceExpressionGenerationUndo.RegisterCreatedObjectUndo(generated, "Create Mouth Neutral Face Expression Clip");
            }
            else EditorUtility.SetDirty(generated);
            MotionSignatures[assetPath] = signature;
            return generated;
        }

        private static void DeleteUnusedGeneratedClips(
            AnimatorController controller,
            string outputFolder,
            ISet<string> usedPaths)
        {
            var controllerPath = AssetDatabase.GetAssetPath(controller);
            var prefix = Path.GetFileNameWithoutExtension(controllerPath) + " ";
            var folder = outputFolder.TrimEnd('/');
            foreach (var guid in AssetDatabase.FindAssets(
                         "t:AnimationClip",
                         new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(
                        Path.GetDirectoryName(path)?.Replace('\\', '/'),
                        folder,
                        StringComparison.Ordinal) ||
                    usedPaths.Contains(path))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith(prefix + "Motion ", StringComparison.Ordinal))
                {
                    AssetDatabase.DeleteAsset(path);
                    MotionSignatures.Remove(path);
                }
            }
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

        private static string CreateTag(
            GestureCombination combination,
            FaceExpressionAssignment assignment)
        {
            var path = assignment.Clip == null
                ? null
                : AssetDatabase.GetAssetPath(assignment.Clip);
            var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            return StateTagPrefix + (int)combination.Left + ":" +
                   (int)combination.Right + ":" + guid + ":" +
                   (assignment.EnableBlink ? "1" : "0") + ":" +
                   (assignment.FixMouth ? "1" : "0") + ":" +
                   Uri.EscapeDataString(assignment.MenuName);
        }

        private static bool TryReadTag(
            string tag,
            out GestureCombination combination,
            out FaceExpressionAssignment assignment)
        {
            combination = default;
            assignment = FaceExpressionAssignment.Default;
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            if (!tag.StartsWith(StateTagPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var parts = tag.Substring(StateTagPrefix.Length).Split(':');
            if (parts.Length != 6 ||
                !TryReadGesture(parts[0], out var left) ||
                !TryReadGesture(parts[1], out var right) ||
                !TryReadBoolean(parts[3], out var enableBlink) ||
                !TryReadBoolean(parts[4], out var fixMouth))
            {
                return false;
            }

            combination = new GestureCombination(left, right);
            assignment = new FaceExpressionAssignment(
                LoadClip(parts[2]),
                enableBlink,
                fixMouth,
                Uri.UnescapeDataString(parts[5]));
            return true;
        }

        private static bool TryReadGesture(string value, out FaceGesture gesture)
        {
            gesture = FaceGesture.Neutral;
            if (!int.TryParse(value, out var rawGesture) ||
                !Enum.IsDefined(typeof(FaceGesture), rawGesture))
            {
                return false;
            }

            gesture = (FaceGesture)rawGesture;
            return true;
        }

        private static bool TryReadBoolean(string value, out bool result)
        {
            result = value == "1";
            return result || value == "0";
        }

        private static string CreateMenuTag(EffectiveMenuEntry item)
        {
            var assignment = item.Entry.Assignment;
            var path = assignment.Clip == null
                ? null
                : AssetDatabase.GetAssetPath(assignment.Clip);
            var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            return MenuTagPrefix + Uri.EscapeDataString(item.Entry.Name) + ":" + guid + ":" +
                   (assignment.EnableBlink ? "1" : "0") + ":" +
                   (assignment.FixMouth ? "1" : "0") + ":" +
                   (item.IsExplicit ? "1" : "0");
        }

        private static bool TryReadMenuTag(
            string tag,
            out FaceExpressionMenuEntry entry,
            out bool isExplicit)
        {
            entry = null;
            isExplicit = false;
            if (string.IsNullOrEmpty(tag) ||
                !tag.StartsWith(MenuTagPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var parts = tag.Substring(MenuTagPrefix.Length).Split(':');
            if (parts.Length != 5 ||
                !TryReadBoolean(parts[2], out var enableBlink) ||
                !TryReadBoolean(parts[3], out var fixMouth) ||
                !TryReadBoolean(parts[4], out isExplicit))
            {
                return false;
            }

            entry = new FaceExpressionMenuEntry(
                Uri.UnescapeDataString(parts[0]),
                new FaceExpressionAssignment(
                    LoadClip(parts[1]),
                    enableBlink,
                    fixMouth));
            return true;
        }

        private static AnimationClip LoadClip(string guid)
        {
            var path = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        private static IReadOnlyList<EffectiveMenuEntry> BuildEffectiveMenuEntries(
            IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> assignments,
            IReadOnlyList<FaceExpressionMenuEntry> menuEntries)
        {
            var result = new List<EffectiveMenuEntry>();
            foreach (var pair in Order(
                         assignments ?? new Dictionary<GestureCombination, FaceExpressionAssignment>())
                     .Where(item => item.Value.Clip != null))
            {
                var assignment = pair.Value;
                var name = pair.Key.Left.ToString().ToLowerInvariant() + "+" +
                           pair.Key.Right.ToString().ToLowerInvariant();

                result.Add(new EffectiveMenuEntry(
                    new FaceExpressionMenuEntry(name, assignment),
                    false,
                    pair.Key.Left));
            }

            foreach (var entry in (menuEntries ?? Array.Empty<FaceExpressionMenuEntry>())
                         .Where(item => item != null && item.Assignment.Clip != null))
            {
                var name = string.IsNullOrWhiteSpace(entry.Name)
                    ? entry.Assignment.Clip.name
                    : entry.Name.Trim();
                result.Add(new EffectiveMenuEntry(
                    new FaceExpressionMenuEntry(name, entry.Assignment),
                    true,
                    null));
            }

            return result.Take(255).ToArray();
        }

        internal sealed class EffectiveMenuEntry
        {
            public EffectiveMenuEntry(
                FaceExpressionMenuEntry entry,
                bool isExplicit,
                FaceGesture? leftGesture)
            {
                Entry = entry;
                IsExplicit = isExplicit;
                LeftGesture = leftGesture;
            }

            public FaceExpressionMenuEntry Entry { get; }
            public string Name => Entry.Name;
            public FaceExpressionAssignment Assignment => Entry.Assignment;
            public bool IsExplicit { get; }
            public FaceGesture? LeftGesture { get; }
        }

        private readonly struct NormalizedMotionKey : IEquatable<NormalizedMotionKey>
        {
            public NormalizedMotionKey(FaceExpressionAssignment assignment)
            {
                Clip = assignment.Clip;
                EnableBlink = assignment.EnableBlink;
                FixMouth = assignment.FixMouth;
            }

            private AnimationClip Clip { get; }
            private bool EnableBlink { get; }
            private bool FixMouth { get; }

            internal string Identity => (Clip == null ? "Default" : AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                Clip, out string guid, out long localId) ? guid + "-" + localId : "Instance-" + Clip.GetInstanceID()) +
                (EnableBlink ? " B1" : " B0") + (FixMouth ? " M1" : " M0");

            public bool Equals(NormalizedMotionKey other)
            {
                return Clip == other.Clip &&
                       EnableBlink == other.EnableBlink &&
                       FixMouth == other.FixMouth;
            }

            public override bool Equals(object obj)
            {
                return obj is NormalizedMotionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = Clip == null ? 0 : Clip.GetInstanceID();
                    hash = (hash * 397) ^ EnableBlink.GetHashCode();
                    return (hash * 397) ^ FixMouth.GetHashCode();
                }
            }
        }
    }
}
