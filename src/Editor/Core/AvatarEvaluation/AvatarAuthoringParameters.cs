using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Ee4v.Core.AvatarEvaluation
{
    /// <summary>A parameter candidate collected from authoring declarations, not a final built parameter.</summary>
    public sealed class AvatarParameterInfo
    {
        public string Name { get; internal set; }
        public AnimatorControllerParameterType Type { get; internal set; }
        public bool Expression { get; internal set; }
        public AnimatorControllerParameter Declaration { get; internal set; }
    }

    /// <summary>Collects parameter candidates for editing without executing NDMF build passes.</summary>
    public static class AvatarAuthoringParameters
    {
        private static readonly HashSet<string> BuiltIns = new HashSet<string>(StringComparer.Ordinal)
        {
            "IsLocal", "PreviewMode", "Viseme", "Voice", "GestureLeft", "GestureRight", "GestureLeftWeight", "GestureRightWeight",
            "AngularY", "VelocityX", "VelocityY", "VelocityZ", "VelocityMagnitude", "Upright", "Grounded", "Seated", "AFK",
            "TrackingType", "VRMode", "MuteSelf", "InStation", "Earmuffs", "IsOnFriendsList", "AvatarVersion", "IsAnimatorEnabled",
            "ScaleModified", "ScaleFactor", "ScaleFactorInverse", "EyeHeightAsMeters", "EyeHeightAsPercent"
        };

        public static bool IsBuiltIn(string name) => BuiltIns.Contains(name);

        public static IReadOnlyList<AvatarParameterInfo> Read(GameObject avatar, AnimatorController ignore = null)
        {
            if (avatar == null) throw new ArgumentNullException(nameof(avatar));
            var entries = new Dictionary<string, AvatarParameterInfo>(StringComparer.Ordinal);
            void Add(string name, AnimatorControllerParameterType type, bool expression = false, AnimatorControllerParameter declaration = null)
            {
                if (string.IsNullOrWhiteSpace(name) || IsBuiltIn(name)) return;
                if (entries.TryGetValue(name, out var current))
                {
                    current.Expression |= expression;
                    if (declaration != null && (expression || current.Declaration == null)) current.Declaration = declaration;
                }
                else entries.Add(name, new AvatarParameterInfo { Name = name, Type = type, Expression = expression, Declaration = declaration });
            }
            void Controller(RuntimeAnimatorController runtime)
            {
                while (runtime is AnimatorOverrideController overrides) runtime = overrides.runtimeAnimatorController;
                if (!(runtime is AnimatorController controller) || controller == ignore) return;
                foreach (var parameter in controller.parameters) Add(parameter.name, parameter.type, declaration: parameter);
            }
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null)
                foreach (var layer in descriptor.baseAnimationLayers.Concat(descriptor.specialAnimationLayers))
                    if (!layer.isDefault) Controller(layer.animatorController);
            foreach (var merge in avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true)) Controller(merge.animator);
            if (descriptor != null && descriptor.expressionParameters != null)
                foreach (var parameter in descriptor.expressionParameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>())
                {
                    var type = parameter.valueType == VRCExpressionParameters.ValueType.Bool ? AnimatorControllerParameterType.Bool :
                        parameter.valueType == VRCExpressionParameters.ValueType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Float;
                    Add(parameter.name, type, true, new AnimatorControllerParameter
                    {
                        name = parameter.name, type = type, defaultBool = parameter.defaultValue != 0,
                        defaultInt = (int)parameter.defaultValue, defaultFloat = parameter.defaultValue
                    });
                }
            foreach (var component in avatar.GetComponentsInChildren<ModularAvatarParameters>(true))
                foreach (var parameter in component.parameters)
                {
                    // Private/prefix definitions belong to their own object scope, not the shared avatar parameter catalog.
                    if (parameter.internalParameter || parameter.isPrefix || parameter.syncType == ParameterSyncType.NotSynced) continue;
                    var name = string.IsNullOrEmpty(parameter.remapTo) ? parameter.nameOrPrefix : parameter.remapTo;
                    var type = parameter.syncType == ParameterSyncType.Bool ? AnimatorControllerParameterType.Bool :
                        parameter.syncType == ParameterSyncType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Float;
                    Add(name, type, true, parameter.hasExplicitDefaultValue ? new AnimatorControllerParameter
                    {
                        name = name, type = type, defaultBool = parameter.defaultValue != 0,
                        defaultInt = (int)parameter.defaultValue, defaultFloat = parameter.defaultValue
                    } : null);
                }
            return entries.Values.OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray();
        }
    }
}
