using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

[assembly: ExportsPlugin(typeof(Ee4v.AvatarInfo.AvatarPerformanceCapturePlugin))]

namespace Ee4v.AvatarInfo
{
    public sealed class AvatarPerformanceCapturePlugin : Plugin<AvatarPerformanceCapturePlugin>
    {
        public override string QualifiedName => "dev.4of.ee4v.avatar-performance-capture";
        public override string DisplayName => "ee4v Avatar Performance Capture";

        private sealed class CaptureState
        {
            internal AvatarPlayModePerformanceCache.Record Source;
            internal readonly Dictionary<string, AvatarInfoParameterSource> Parameters =
                new Dictionary<string, AvatarInfoParameterSource>();
            internal readonly Dictionary<GameObject, (string Name, string Path)> Items =
                new Dictionary<GameObject, (string, string)>();
        }

        [InitializeOnLoadMethod]
        private static void RegisterParameterSources()
        {
            AvatarInfoSdk.ReadParameterSources = avatar => ReadSources(avatar, ParameterInfo.ForUI);
        }

        private static AvatarInfoParameterSource[] ReadSources(GameObject avatar, ParameterInfo info)
        {
            if (avatar == null) return null;
            return info.GetParametersForObject(avatar)
                .Where(parameter => parameter.BitUsage > 0 && !string.IsNullOrEmpty(parameter.EffectiveName))
                .Select(parameter =>
                {
                    var item = GetItemRoot(avatar, parameter.Source);
                    return new AvatarInfoParameterSource
                    {
                        Name = parameter.EffectiveName, Used = parameter.BitUsage,
                        ItemName = item.name, ItemPath = item == avatar ? string.Empty : item.name +
                            " [" + item.transform.GetSiblingIndex() + "]"
                    };
                }).ToArray();
        }

        private static GameObject GetItemRoot(GameObject avatar, Component source)
        {
            if (source == null || !source.transform.IsChildOf(avatar.transform)) return avatar;
            var current = source.transform;
            while (current != avatar.transform && current.parent != avatar.transform) current = current.parent;
            return current.gameObject;
        }

        protected override void Configure()
        {
            InPhase(BuildPhase.FirstChance).Run("Remember play mode avatar", context =>
                context.GetState<CaptureState>().Source = AvatarPlayModePerformanceCache.FindBuildSource(context.AvatarRootObject));
            InPhase(BuildPhase.Transforming).BeforePlugin("nadena.dev.modular-avatar")
                .Run("Remember parameter items", context =>
                {
                    var state = context.GetState<CaptureState>();
                    if (state.Source == null) return;
                    foreach (Transform item in context.AvatarRootTransform)
                        state.Items[item.gameObject] = (item.name, item.name + " [" + item.GetSiblingIndex() + "]");
                    foreach (var parameter in ReadSources(context.AvatarRootObject, ParameterInfo.ForContext(context)))
                        state.Parameters[parameter.Name] = parameter;
                });
            InPhase(BuildPhase.Transforming).AfterPlugin("nadena.dev.modular-avatar")
                .BeforePlugin("nadena.dev.modular-avatar.late-transform-stages")
                .Run("Remember generated menu parameters", context =>
                {
                    var state = context.GetState<CaptureState>();
                    if (state.Source == null) return;
                    foreach (var item in state.Items)
                    {
                        if (item.Key == null) continue;
                        foreach (var parameter in ReadSources(item.Key, ParameterInfo.ForContext(context)))
                        {
                            if (state.Parameters.ContainsKey(parameter.Name)) continue;
                            parameter.ItemName = item.Value.Name;
                            parameter.ItemPath = item.Value.Path;
                            state.Parameters[parameter.Name] = parameter;
                        }
                    }
                });
            InPhase(BuildPhase.Optimizing).AfterPlugin("com.anatawa12.avatar-optimizer")
                .Run("Remember play mode build", context =>
                    AvatarPlayModePerformanceCache.RememberBuild(context.GetState<CaptureState>().Source,
                        () => context.AvatarRootObject, () => context.Successful,
                        context.GetState<CaptureState>().Parameters.Values.ToArray()));
        }
    }
}
