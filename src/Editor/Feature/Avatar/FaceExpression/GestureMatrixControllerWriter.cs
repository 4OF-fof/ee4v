using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ee4v.FaceExpression
{
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
            bool fixMouth = false)
        {
            Clip = clip;
            EnableBlink = enableBlink;
            FixMouth = fixMouth;
        }

        public AnimationClip Clip { get; }
        public bool EnableBlink { get; }
        public bool FixMouth { get; }
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
        internal const string LayerName = FaceExpressionControllerWriter.LayerName;
        internal const string MenuLayerName = "ee4v Face Expression Menu";
        internal const string MenuParameter = "ee4v/FaceExpression";
        private const string GestureLeft = "GestureLeft";
        private const string GestureRight = "GestureRight";
        private const string StateTagPrefix = "ee4v-face:2:";
        private const string MenuTagPrefix = "ee4v-menu:1:";
        private const float ClipLength = 1f / 60f;

        public static bool OwnsLayer(AnimatorController controller)
        {
            return controller != null && controller.layers.Any(layer => layer.name == LayerName);
        }

        public static IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> Read(
            AnimatorController controller)
        {
            var result = ReadMatrix(controller);
            if (result.Count > 0 || controller == null)
            {
                return result;
            }

            ExpandLegacyAssignments(FaceExpressionControllerWriter.Read(controller), result);
            return result;
        }

        public static IReadOnlyList<FaceExpressionMenuEntry> ReadMenuEntries(
            AnimatorController controller)
        {
            var result = new List<FaceExpressionMenuEntry>();
            if (controller == null)
            {
                return result;
            }

            var layer = controller.layers.FirstOrDefault(candidate => candidate.name == MenuLayerName);
            if (layer?.stateMachine == null)
            {
                return result;
            }

            foreach (var child in layer.stateMachine.states.OrderBy(item => item.state.name))
            {
                if (TryReadMenuTag(child.state.tag, out var entry, out var isExplicit) && isExplicit)
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        public static IReadOnlyList<FaceExpressionMenuEntry> GetEffectiveMenuEntries(
            IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> assignments,
            IReadOnlyList<FaceExpressionMenuEntry> menuEntries)
        {
            return BuildEffectiveMenuEntries(assignments, menuEntries)
                .Select(item => item.Entry)
                .ToArray();
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

            var configured = assignments
                .Where(pair => !pair.Value.IsDefault)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var neutralKey = new GestureCombination(FaceGesture.Neutral, FaceGesture.Neutral);
            if (!configured.ContainsKey(neutralKey))
            {
                configured.Add(neutralKey, GetAssignment(assignments, neutralKey));
            }

            var effectiveMenuEntries = BuildEffectiveMenuEntries(assignments, menuEntries);
            var allAssignments = configured.Values
                .Concat(effectiveMenuEntries.Select(item => item.Entry.Assignment));
            var bindings = CollectBindings(allAssignments, avatarBindings);
            var blinkKeys = CreateBindingKeys(avatarBindings.Blink);
            var mouthKeys = CreateBindingKeys(avatarBindings.Mouth);
            var motions = new Dictionary<GestureCombination, AnimationClip>();
            foreach (var pair in Order(configured))
            {
                motions[pair.Key] = CreateNormalizedClip(
                    controller,
                    avatar,
                    pair.Value,
                    bindings,
                    blinkKeys,
                    mouthKeys,
                    "L" + ((int)pair.Key.Left).ToString("00") +
                    " R" + ((int)pair.Key.Right).ToString("00"),
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

            var neutral = AddState(
                stateMachine,
                neutralKey,
                motions[neutralKey],
                configured[neutralKey]);
            stateMachine.defaultState = neutral;

            foreach (var pair in Order(configured).Where(pair => !pair.Key.Equals(neutralKey)))
            {
                var state = AddState(stateMachine, pair.Key, motions[pair.Key], pair.Value);
                AddAnyStateTransition(stateMachine, state, pair.Key);
                AddResetTransition(state, neutral, GestureLeft, pair.Key.Left);
                AddResetTransition(state, neutral, GestureRight, pair.Key.Right);
            }

            AddMenuOverrideState(stateMachine, neutral);
            if (effectiveMenuEntries.Count > 0)
            {
                AddMenuLayer(
                    controller,
                    avatar,
                    effectiveMenuEntries,
                    bindings,
                    blinkKeys,
                    mouthKeys,
                    outputFolder);
            }

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
            FaceExpressionAssignment assignment)
        {
            var state = stateMachine.AddState(
                ((int)combination.Left).ToString("00") + "-" +
                ((int)combination.Right).ToString("00") + " " +
                combination.Left + " + " + combination.Right);
            state.motion = motion;
            state.writeDefaultValues = false;
            state.tag = CreateTag(combination, assignment);
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

        private static void AddResetTransition(
            AnimatorState state,
            AnimatorState neutral,
            string parameter,
            FaceGesture gesture)
        {
            var reset = state.AddTransition(neutral);
            ConfigureTransition(reset);
            reset.AddCondition(AnimatorConditionMode.NotEqual, (int)gesture, parameter);
        }

        private static void AddMenuOverrideState(
            AnimatorStateMachine stateMachine,
            AnimatorState neutral)
        {
            var state = stateMachine.AddState("Menu Override");
            state.writeDefaultValues = false;
            var activate = stateMachine.AddAnyStateTransition(state);
            ConfigureTransition(activate);
            activate.canTransitionToSelf = false;
            activate.AddCondition(AnimatorConditionMode.NotEqual, 0f, MenuParameter);

            var reset = state.AddTransition(neutral);
            ConfigureTransition(reset);
            reset.AddCondition(AnimatorConditionMode.Equals, 0f, MenuParameter);
        }

        private static void AddMenuLayer(
            AnimatorController controller,
            GameObject avatar,
            IReadOnlyList<EffectiveMenuEntry> entries,
            IReadOnlyList<EditorCurveBinding> bindings,
            ISet<string> blinkKeys,
            ISet<string> mouthKeys,
            string outputFolder)
        {
            var stateMachine = new AnimatorStateMachine { name = MenuLayerName };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);
            Undo.RegisterCreatedObjectUndo(stateMachine, "Create Face Expression Menu Layer");
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = MenuLayerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            });

            var off = stateMachine.AddState("000 Gesture Assignments");
            off.writeDefaultValues = false;
            stateMachine.defaultState = off;
            for (var index = 0; index < entries.Count; index++)
            {
                var value = index + 1;
                var item = entries[index];
                var motion = CreateNormalizedClip(
                    controller,
                    avatar,
                    item.Entry.Assignment,
                    bindings,
                    blinkKeys,
                    mouthKeys,
                    "M" + value.ToString("000"),
                    outputFolder);
                var state = stateMachine.AddState(
                    value.ToString("000") + " " + item.Entry.Name);
                state.motion = motion;
                state.writeDefaultValues = false;
                state.tag = CreateMenuTag(item);

                var activate = stateMachine.AddAnyStateTransition(state);
                ConfigureTransition(activate);
                activate.canTransitionToSelf = false;
                activate.AddCondition(AnimatorConditionMode.Equals, value, MenuParameter);

                var reset = state.AddTransition(off);
                ConfigureTransition(reset);
                reset.AddCondition(AnimatorConditionMode.NotEqual, value, MenuParameter);
            }

            EditorUtility.SetDirty(stateMachine);
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
            IEnumerable<FaceExpressionAssignment> assignments,
            FaceExpressionAvatarBindings avatarBindings)
        {
            var result = new Dictionary<string, EditorCurveBinding>(StringComparer.Ordinal);
            foreach (var assignment in assignments)
            {
                if (assignment.Clip != null)
                {
                    AddBindings(result, AnimationUtility.GetCurveBindings(assignment.Clip));
                }

                if (!assignment.EnableBlink)
                {
                    AddBindings(result, avatarBindings.Blink);
                }

                if (assignment.FixMouth)
                {
                    AddBindings(result, avatarBindings.Mouth);
                }
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

        private static HashSet<string> CreateBindingKeys(
            IEnumerable<EditorCurveBinding> bindings)
        {
            return new HashSet<string>(bindings.Select(BindingKey), StringComparer.Ordinal);
        }

        private static AnimationClip CreateNormalizedClip(
            AnimatorController controller,
            GameObject avatar,
            FaceExpressionAssignment assignment,
            IReadOnlyList<EditorCurveBinding> bindings,
            ISet<string> blinkKeys,
            ISet<string> mouthKeys,
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
                var key = BindingKey(binding);
                if ((blinkKeys.Contains(key) && assignment.EnableBlink) ||
                    (mouthKeys.Contains(key) && !assignment.FixMouth))
                {
                    continue;
                }

                var curve = assignment.Clip == null
                    ? null
                    : AnimationUtility.GetEditorCurve(assignment.Clip, binding);
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
                   (assignment.FixMouth ? "1" : "0");
        }

        private static bool TryReadTag(
            string tag,
            out GestureCombination combination,
            out FaceExpressionAssignment assignment)
        {
            combination = default;
            assignment = FaceExpressionAssignment.Default;
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith(StateTagPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var parts = tag.Substring(StateTagPrefix.Length).Split(':');
            if (parts.Length != 5 ||
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
                fixMouth);
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

        private static void ExpandLegacyAssignments(
            IReadOnlyDictionary<FaceGesture, AnimationClip> legacy,
            IDictionary<GestureCombination, FaceExpressionAssignment> result)
        {
            foreach (FaceGesture left in Enum.GetValues(typeof(FaceGesture)))
            {
                foreach (FaceGesture right in Enum.GetValues(typeof(FaceGesture)))
                {
                    AnimationClip clip = null;
                    if (left != FaceGesture.Neutral)
                    {
                        legacy.TryGetValue(left, out clip);
                    }

                    if (clip == null && right != FaceGesture.Neutral)
                    {
                        legacy.TryGetValue(right, out clip);
                    }

                    if (clip == null)
                    {
                        legacy.TryGetValue(FaceGesture.Neutral, out clip);
                    }

                    if (clip != null)
                    {
                        result[new GestureCombination(left, right)] =
                            new FaceExpressionAssignment(clip);
                    }
                }
            }
        }

        private static IReadOnlyList<EffectiveMenuEntry> BuildEffectiveMenuEntries(
            IReadOnlyDictionary<GestureCombination, FaceExpressionAssignment> assignments,
            IReadOnlyList<FaceExpressionMenuEntry> menuEntries)
        {
            var result = new List<EffectiveMenuEntry>();
            foreach (var assignment in Order(
                         assignments ?? new Dictionary<GestureCombination, FaceExpressionAssignment>())
                     .Select(pair => pair.Value)
                     .Where(value => value.Clip != null))
            {
                if (result.Any(item =>
                        item.Entry.Assignment.Clip == assignment.Clip &&
                        item.Entry.Assignment.EnableBlink == assignment.EnableBlink &&
                        item.Entry.Assignment.FixMouth == assignment.FixMouth))
                {
                    continue;
                }

                result.Add(new EffectiveMenuEntry(
                    new FaceExpressionMenuEntry(assignment.Clip.name, assignment),
                    false));
            }

            foreach (var entry in (menuEntries ?? Array.Empty<FaceExpressionMenuEntry>())
                         .Where(item => item != null && item.Assignment.Clip != null))
            {
                var name = string.IsNullOrWhiteSpace(entry.Name)
                    ? entry.Assignment.Clip.name
                    : entry.Name.Trim();
                result.Add(new EffectiveMenuEntry(
                    new FaceExpressionMenuEntry(name, entry.Assignment),
                    true));
            }

            return result.Take(255).ToArray();
        }

        private sealed class EffectiveMenuEntry
        {
            public EffectiveMenuEntry(FaceExpressionMenuEntry entry, bool isExplicit)
            {
                Entry = entry;
                IsExplicit = isExplicit;
            }

            public FaceExpressionMenuEntry Entry { get; }
            public bool IsExplicit { get; }
        }
    }
}
