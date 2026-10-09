using System;
using System.Collections.Generic;
using System.Linq;
using BlackStartX.GestureManager.Editor.Modules.Vrc3;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.Simulation
{
    public static partial class GestureManagerIntegration
    {
        public sealed class Parameter
        {
            public string Name;
            public string Type;
            public float Value;
        }

        public static bool IsConnected(GameObject avatar) =>
            FindManager(avatar)?.Module is ModuleVrc3 module && module.Avatar == avatar && module.Active;

        public static GameObject VisibleAvatar(GameObject avatar) =>
            FindManager(avatar)?.Module is ModuleVrc3 module && module.Avatar == avatar ? GetVisibleAvatar(module) ?? avatar : avatar;

        public static IReadOnlyList<Parameter> ReadParameters(GameObject avatar)
        {
            if (!IsConnected(avatar)) return Array.Empty<Parameter>();
            var module = (ModuleVrc3)FindManager(avatar).Module;
            return module.Params.Values.OrderBy(value => value.Name, StringComparer.Ordinal)
                .Select(value => new Parameter { Name = value.Name, Type = value.TypeText, Value = value.FloatValue() }).ToArray();
        }

        public static void Connect(GameObject avatar)
        {
            RequireLiveAvatar(avatar);
            var manager = FindManager(avatar);
            if (manager == null) throw new InvalidOperationException("Place one GestureManager in the avatar's Scene, or select its existing association.");
            Connect(avatar, manager);
        }

        public static void SetParameters(GameObject avatar, IReadOnlyDictionary<string, float> values)
        {
            RequireLiveAvatar(avatar);
            if (!IsConnected(avatar)) throw new InvalidOperationException("Connect GestureManager to this avatar first.");
            if (values == null || values.Count == 0) throw new ArgumentException("At least one parameter is required.");
            var module = (ModuleVrc3)FindManager(avatar).Module;
            foreach (var pair in values)
            {
                var parameter = module.GetParam(pair.Key);
                if (parameter == null) throw new ArgumentException("Unknown runtime parameter: " + pair.Key);
                if (float.IsNaN(pair.Value) || float.IsInfinity(pair.Value)) throw new ArgumentException("A finite parameter value is required.");
                if ((pair.Key == "GestureLeft" || pair.Key == "GestureRight") &&
                    (pair.Value < 0 || pair.Value > 7 || pair.Value != Mathf.Round(pair.Value)))
                    throw new ArgumentException("Gesture values must be integers from 0 to 7.");
                if ((pair.Key == "GestureLeftWeight" || pair.Key == "GestureRightWeight") && (pair.Value < 0 || pair.Value > 1))
                    throw new ArgumentException("Gesture weights must be from 0 to 1.");
                if ((parameter.Type == AnimatorControllerParameterType.Bool || parameter.Type == AnimatorControllerParameterType.Trigger) &&
                    pair.Value != 0 && pair.Value != 1) throw new ArgumentException("Bool and Trigger inputs must be 0 or 1.");
                if (parameter.Type == AnimatorControllerParameterType.Int && pair.Value != Mathf.Round(pair.Value))
                    throw new ArgumentException("Int parameters require integer values.");
            }
            foreach (var pair in values) module.GetParam(pair.Key).Set(module, pair.Value);
        }

        private static void RequireLiveAvatar(GameObject avatar)
        {
            if (!EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode || avatar == null || EditorUtility.IsPersistent(avatar) || !avatar.scene.IsValid())
                throw new InvalidOperationException("A live Play Mode avatar is required.");
        }
    }
}
