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
            IReadOnlyList<EditorCurveBinding> mouth)
        {
            Blink = blink ?? Array.Empty<EditorCurveBinding>();
            Mouth = mouth ?? Array.Empty<EditorCurveBinding>();
        }

        public IReadOnlyList<EditorCurveBinding> Blink { get; }
        public IReadOnlyList<EditorCurveBinding> Mouth { get; }

        public static FaceExpressionAvatarBindings Empty { get; } =
            new FaceExpressionAvatarBindings(null, null);
    }

    internal static class GestureMatrixControllerWriter
    {
        internal const string LayerName = "ee4v Face Expressions";
        internal const string MenuLayerName = "ee4v Face Expression Menu";
        internal const string MenuParameter = "ee4v/FaceExpression";
        private const string GestureLeft = "GestureLeft";
        private const string GestureRight = "GestureRight";
        private const string StateTagPrefix = "ee4v-face:3:";
        private const string MenuTagPrefix = "ee4v-menu:1:";
        private static readonly string[] TrackingControlTypeNames =
        {
            "VRC.SDKBase.VRC_AnimatorTrackingControl",
            "VRC.SDK3.Avatars.Components.VRCAnimatorTrackingControl"
        };
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
            if (layer?.stateMachine == null)
            {
                layer = controller.layers.FirstOrDefault(
                    candidate => candidate.name == MenuLayerName);
            }

            var foundMenuState = ReadMenuEntries(
                layer?.stateMachine,
                result);
            if (!foundMenuState && layer?.name != MenuLayerName)
            {
                var legacyLayer = controller.layers.FirstOrDefault(
                    candidate => candidate.name == MenuLayerName);
                ReadMenuEntries(legacyLayer?.stateMachine, result);
            }

            return result;
        }

        private static bool ReadMenuEntries(
            AnimatorStateMachine stateMachine,
            ICollection<FaceExpressionMenuEntry> result)
        {
            var foundMenuState = false;
            if (stateMachine == null)
            {
                return false;
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

                foundMenuState = true;
                if (isExplicit)
                {
                    result.Add(entry);
                }
            }

            return foundMenuState;
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

            assignments = assignments ??
                new Dictionary<GestureCombination, FaceExpressionAssignment>();
            menuEntries = menuEntries ?? Array.Empty<FaceExpressionMenuEntry>();
            avatarBindings = avatarBindings ?? FaceExpressionAvatarBindings.Empty;
            EnsureParameter(controller, GestureLeft);
            EnsureParameter(controller, GestureRight);
            EnsureParameter(controller, MenuParameter);
            RemoveOwnedLayer(controller);
            RemoveLayer(controller, MenuLayerName);

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
            var allAssignments = effectiveAssignments.Values
                .Concat(effectiveMenuEntries.Select(item => item.Entry.Assignment));
            var bindings = CollectBindings(
                avatar,
                allAssignments,
                avatarBindings);
            var motions = new Dictionary<GestureCombination, AnimationClip>();
            var normalizedMotions =
                new Dictionary<NormalizedMotionKey, AnimationClip>();
            var usedGeneratedClipPaths = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (var pair in Order(configured))
            {
                motions[pair.Key] = GetOrCreateNormalizedMotion(
                    controller,
                    avatar,
                    effectiveAssignments[pair.Key],
                    bindings,
                    outputFolder,
                    normalizedMotions,
                    usedGeneratedClipPaths);
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

            var neutral = AddState(
                stateMachine,
                neutralKey,
                motions[neutralKey],
                configured[neutralKey],
                effectiveAssignments[neutralKey]);
            stateMachine.defaultState = neutral;

            foreach (var pair in Order(configured).Where(pair => !pair.Key.Equals(neutralKey)))
            {
                var state = AddState(
                    stateMachine,
                    pair.Key,
                    motions[pair.Key],
                    pair.Value,
                    effectiveAssignments[pair.Key]);
                AddAnyStateTransition(stateMachine, state, pair.Key);
            }

            AddAnyStateTransition(stateMachine, neutral, neutralKey);

            if (effectiveMenuEntries.Count > 0)
            {
                AddMenuStates(
                    stateMachine,
                    neutral,
                    controller,
                    avatar,
                    effectiveMenuEntries,
                    bindings,
                    outputFolder,
                    normalizedMotions,
                    usedGeneratedClipPaths);
            }

            DeleteUnusedGeneratedClips(
                controller,
                outputFolder,
                usedGeneratedClipPaths);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
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
            AnimationClip motion,
            FaceExpressionAssignment storedAssignment,
            FaceExpressionAssignment effectiveAssignment)
        {
            var state = stateMachine.AddState(
                ((int)combination.Left).ToString("00") + "-" +
                ((int)combination.Right).ToString("00") + " " +
                combination.Left + " + " + combination.Right);
            state.motion = motion;
            state.writeDefaultValues = false;
            state.tag = CreateTag(combination, storedAssignment);
            ConfigureTracking(state, effectiveAssignment);
            return state;
        }

        private static void AddAnyStateTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState state,
            GestureCombination combination)
        {
            var transition = stateMachine.AddAnyStateTransition(state);
            ConfigureTransition(transition);
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                AnimatorConditionMode.Equals,
                (int)combination.Left,
                GestureLeft);
            transition.AddCondition(
                AnimatorConditionMode.Equals,
                (int)combination.Right,
                GestureRight);
            transition.AddCondition(AnimatorConditionMode.Equals, 0f, MenuParameter);
        }

        private static void AddMenuStates(
            AnimatorStateMachine stateMachine,
            AnimatorState neutral,
            AnimatorController controller,
            GameObject avatar,
            IReadOnlyList<EffectiveMenuEntry> entries,
            IReadOnlyList<EditorCurveBinding> bindings,
            string outputFolder,
            IDictionary<NormalizedMotionKey, AnimationClip> normalizedMotions,
            ISet<string> usedGeneratedClipPaths)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var value = index + 1;
                var item = entries[index];
                var motion = GetOrCreateNormalizedMotion(
                    controller,
                    avatar,
                    item.Entry.Assignment,
                    bindings,
                    outputFolder,
                    normalizedMotions,
                    usedGeneratedClipPaths);
                var state = stateMachine.AddState(
                    "M" + value.ToString("000") + " " + item.Entry.Name);
                state.motion = motion;
                state.writeDefaultValues = false;
                state.tag = CreateMenuTag(item);
                ConfigureTracking(state, item.Entry.Assignment);

                var activate = stateMachine.AddAnyStateTransition(state);
                ConfigureTransition(activate);
                activate.canTransitionToSelf = false;
                activate.AddCondition(AnimatorConditionMode.Equals, value, MenuParameter);

                var reset = state.AddTransition(neutral);
                ConfigureTransition(reset);
                reset.AddCondition(AnimatorConditionMode.Equals, 0f, MenuParameter);
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
            RemoveLayer(controller, LayerName);
        }

        private static void RemoveLayer(AnimatorController controller, string layerName)
        {
            Undo.RegisterCompleteObjectUndo(controller, "Update Face Expression Layer");
            for (var index = controller.layers.Length - 1; index >= 0; index--)
            {
                var layer = controller.layers[index];
                if (layer.name != layerName)
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

        private static IReadOnlyList<EditorCurveBinding> CollectBindings(
            GameObject avatar,
            IEnumerable<FaceExpressionAssignment> assignments,
            FaceExpressionAvatarBindings avatarBindings)
        {
            var result = new Dictionary<string, EditorCurveBinding>(StringComparer.Ordinal);
            AddBindings(result, avatarBindings.Blink);
            AddBindings(result, avatarBindings.Mouth);
            foreach (var assignment in assignments)
            {
                if (assignment.Clip != null)
                {
                    AddBindings(result, AnimationUtility.GetCurveBindings(assignment.Clip));
                }
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
            string identity,
            string outputFolder)
        {
            var controllerPath = AssetDatabase.GetAssetPath(controller);
            var prefix = Path.GetFileNameWithoutExtension(controllerPath);
            var assetPath = outputFolder.TrimEnd('/') + "/" + prefix + " " + identity + ".anim";
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
                var curve = assignment.Clip == null
                    ? null
                    : AnimationUtility.GetEditorCurve(assignment.Clip, binding);
                var value = curve == null ? GetDefaultValue(avatar, binding) : curve.Evaluate(0f);
                AnimationUtility.SetEditorCurve(
                    generated,
                    binding,
                    new AnimationCurve(new Keyframe(0f, value)));
            }

            generated.frameRate = 60f;
            EditorUtility.SetDirty(generated);
            return generated;
        }

        private static void ConfigureTracking(
            AnimatorState state,
            FaceExpressionAssignment assignment)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => TrackingControlTypeNames.Select(name =>
                    assembly.GetType(name, false)))
                .FirstOrDefault(candidate => candidate != null);
            if (type == null)
            {
                return;
            }

            var behaviour = state.AddStateMachineBehaviour(type);
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

            field.SetValue(behaviour, Enum.Parse(field.FieldType, value));
            EditorUtility.SetDirty(behaviour);
        }

        private static AnimationClip GetOrCreateNormalizedMotion(
            AnimatorController controller,
            GameObject avatar,
            FaceExpressionAssignment assignment,
            IReadOnlyList<EditorCurveBinding> bindings,
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
                    "Motion " + (motions.Count + 1).ToString("000"),
                    outputFolder);
                motions.Add(key, motion);
            }

            usedGeneratedClipPaths.Add(AssetDatabase.GetAssetPath(motion));
            return motion;
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
                if (name.StartsWith(prefix + "Motion ", StringComparison.Ordinal) ||
                    name.StartsWith(prefix + "L", StringComparison.Ordinal) ||
                    name.StartsWith(prefix + "M", StringComparison.Ordinal))
                {
                    AssetDatabase.DeleteAsset(path);
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
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith(MenuTagPrefix, StringComparison.Ordinal))
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
                var leftGesture = pair.Key.Left == FaceGesture.Neutral
                    ? (FaceGesture?)null
                    : pair.Key.Left;
                var name = string.IsNullOrWhiteSpace(assignment.MenuName)
                    ? assignment.Clip.name
                    : assignment.MenuName.Trim();
                if (result.Any(item =>
                        item.LeftGesture == leftGesture &&
                        item.Entry.Name == name &&
                        item.Entry.Assignment.Clip == assignment.Clip))
                {
                    continue;
                }

                result.Add(new EffectiveMenuEntry(
                    new FaceExpressionMenuEntry(name, assignment),
                    false,
                    leftGesture));
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
            }

            private AnimationClip Clip { get; }

            public bool Equals(NormalizedMotionKey other)
            {
                return Clip == other.Clip;
            }

            public override bool Equals(object obj)
            {
                return obj is NormalizedMotionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return Clip == null ? 0 : Clip.GetInstanceID();
                }
            }
        }
    }
}
