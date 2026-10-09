using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: ExportsPlugin(typeof(Ee4v.AvatarInfo.AvatarPerformanceCapturePlugin))]

namespace Ee4v.AvatarInfo
{
    public sealed class AvatarPerformanceCapturePlugin : Plugin<AvatarPerformanceCapturePlugin>
    {
        public override string QualifiedName => "dev.4of.ee4v.avatar-performance-capture";
        public override string DisplayName => "ee4v Avatar Performance Capture";
        private static GameObject _measurementAvatar;

        private sealed class CaptureState
        {
            internal bool Measuring;
            internal readonly Dictionary<string, AvatarInfoParameterSource> Parameters =
                new Dictionary<string, AvatarInfoParameterSource>();
            internal readonly Dictionary<GameObject, (string Name, string Path)> Items =
                new Dictionary<GameObject, (string, string)>();
        }

        [InitializeOnLoadMethod]
        private static void RegisterParameterSources()
        {
            AvatarInfoSdk.ReadParameterSources = avatar => ReadSources(avatar, ParameterInfo.ForUI);
            AvatarPlayModePerformanceCache.BakeAvatar = Bake;
        }

        private static void Bake(GameObject source, AvatarPlayModePerformanceCache.Record record)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var assetRoot = "Assets/ee4v_Performance_" + Guid.NewGuid().ToString("N");
            GameObject avatar = null;
            try
            {
                avatar = UnityEngine.Object.Instantiate(source);
                SceneManager.MoveGameObjectToScene(avatar, scene);
                avatar.name = source.name;
                if (PrefabUtility.IsPartOfPrefabInstance(avatar))
                    PrefabUtility.UnpackPrefabInstance(avatar, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                _measurementAvatar = avatar;
                using (new OverrideTemporaryDirectoryScope(assetRoot))
                {
                    var context = AvatarProcessor.ProcessAvatar(avatar,
                        PlatformRegistry.GetPrimaryPlatformForAvatar(avatar));
                    if (!context.Successful)
                        throw new InvalidOperationException("NDMF bake failed. Check the NDMF error report.");
                    AvatarPlayModePerformanceCache.Capture(context.AvatarRootObject, record,
                        context.GetState<CaptureState>().Parameters.Values.ToArray());
                }
            }
            finally
            {
                _measurementAvatar = null;
                try
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
                finally
                {
                    if (AssetDatabase.IsValidFolder(assetRoot) && !AssetDatabase.DeleteAsset(assetRoot))
                        throw new InvalidOperationException("Could not remove measurement assets: " + assetRoot);
                }
            }
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
            InPhase(BuildPhase.FirstChance).Run("Remember measurement avatar", context =>
                context.GetState<CaptureState>().Measuring = _measurementAvatar != null &&
                    context.AvatarRootObject == _measurementAvatar);
            InPhase(BuildPhase.Transforming).BeforePlugin("nadena.dev.modular-avatar")
                .Run("Remember parameter items", context =>
                {
                    var state = context.GetState<CaptureState>();
                    if (!state.Measuring) return;
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
                    if (!state.Measuring) return;
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
        }
    }
}
