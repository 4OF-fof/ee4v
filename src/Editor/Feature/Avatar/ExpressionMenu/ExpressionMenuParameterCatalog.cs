using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using nadena.dev.modular_avatar.core;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Ee4v.ExpressionMenu
{
    internal static class ExpressionMenuParameterCatalog
    {
        internal sealed class Entry
        {
            internal string Name;
            internal AnimatorControllerParameterType Type;
            internal bool Expression;
        }

        private static readonly HashSet<string> BuiltIns = new HashSet<string>(StringComparer.Ordinal)
        {
            "IsLocal", "PreviewMode", "Viseme", "Voice", "GestureLeft", "GestureRight", "GestureLeftWeight", "GestureRightWeight",
            "AngularY", "VelocityX", "VelocityY", "VelocityZ", "VelocityMagnitude", "Upright", "Grounded", "Seated", "AFK",
            "TrackingType", "VRMode", "MuteSelf", "InStation", "Earmuffs", "IsOnFriendsList", "AvatarVersion", "IsAnimatorEnabled",
            "ScaleModified", "ScaleFactor", "ScaleFactorInverse", "EyeHeightAsMeters", "EyeHeightAsPercent"
        };

        internal static bool IsBuiltIn(string name) => BuiltIns.Contains(name);

        internal static Entry Find(AvatarEditingContext context, AnimatorController ignore, string name) =>
            Entries(context, ignore).FirstOrDefault(entry => entry.Name == name);

        internal static Entry[] Entries(AvatarEditingContext context, AnimatorController ignore)
        {
            var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            void Add(string name, AnimatorControllerParameterType type, bool expression = false)
            {
                if (string.IsNullOrWhiteSpace(name) || IsBuiltIn(name)) return;
                if (entries.TryGetValue(name, out var current)) current.Expression |= expression;
                else entries.Add(name, new Entry { Name = name, Type = type, Expression = expression });
            }
            void Controller(RuntimeAnimatorController runtime)
            {
                while (runtime is AnimatorOverrideController overrides) runtime = overrides.runtimeAnimatorController;
                if (!(runtime is AnimatorController controller) || controller == ignore) return;
                foreach (var parameter in controller.parameters) Add(parameter.name, parameter.type);
            }
            var root = context.Root;
            if (root == null) return Array.Empty<Entry>();
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null)
            {
                foreach (var layer in descriptor.baseAnimationLayers.Concat(descriptor.specialAnimationLayers))
                    if (!layer.isDefault) Controller(layer.animatorController);
            }
            foreach (var merge in root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true)) Controller(merge.animator);
            if (descriptor != null && descriptor.expressionParameters != null)
                foreach (var parameter in descriptor.expressionParameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>())
                    Add(parameter.name, parameter.valueType == VRCExpressionParameters.ValueType.Bool ? AnimatorControllerParameterType.Bool :
                        parameter.valueType == VRCExpressionParameters.ValueType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Float, true);
            foreach (var component in root.GetComponentsInChildren<ModularAvatarParameters>(true))
                foreach (var parameter in component.parameters)
                {
                    // Private/prefix definitions belong to their own object scope, not the shared avatar parameter catalog.
                    if (parameter.internalParameter || parameter.isPrefix || parameter.syncType == ParameterSyncType.NotSynced) continue;
                    Add(string.IsNullOrEmpty(parameter.remapTo) ? parameter.nameOrPrefix : parameter.remapTo,
                        parameter.syncType == ParameterSyncType.Bool ? AnimatorControllerParameterType.Bool :
                        parameter.syncType == ParameterSyncType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Float, true);
                }
            return entries.Values.OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray();
        }
    }
}
