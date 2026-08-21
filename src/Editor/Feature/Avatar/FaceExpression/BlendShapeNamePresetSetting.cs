using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    [Serializable]
    internal sealed class BlendShapeNamePresetState
    {
        public List<BlendShapeFbxPreset> presets = new List<BlendShapeFbxPreset>();
    }

    [Serializable]
    internal sealed class BlendShapeFbxPreset
    {
        public string assetGuid;
        public string assetPath;
        public string name;
        public List<BlendShapeNameMapping> mappings = new List<BlendShapeNameMapping>();
    }

    [Serializable]
    internal sealed class BlendShapeNameMapping
    {
        public long meshLocalId;
        public string meshName;
        public string shapeName;
        public string headerText;
        public string role;
        public string variation;
        public string side;
    }

    internal static class BlendShapeNamePresetSetting
    {
        internal static string DefaultValue => Serialize(new BlendShapeNamePresetState());

        internal static void RegisterDrawer(SettingDefinition<string> definition)
        {
            SettingDrawerApi.Register(definition, context =>
            {
                var root = new VisualElement();
                var count = Parse(context.Value).presets.Count;
                root.Add(UiTextFactory.Create(string.Format(
                    I18N.Get("settings.blendShapePresets.count"),
                    count)));
                var open = UiTextFactory.CreateButton(
                    I18N.Get("settings.blendShapePresets.open"),
                    BlendShapePresetWindow.ShowWindow);
                open.tooltip = context.Tooltip ?? string.Empty;
                root.Add(open);
                return root;
            });
        }

        internal static BlendShapeNamePresetState Parse(string value)
        {
            try
            {
                var parsed = JsonUtility.FromJson<BlendShapeNamePresetState>(
                    value ?? string.Empty) ?? new BlendShapeNamePresetState();
                Normalize(parsed);
                return parsed;
            }
            catch (ArgumentException)
            {
                return new BlendShapeNamePresetState();
            }
        }

        internal static string Serialize(BlendShapeNamePresetState state)
        {
            state = state ?? new BlendShapeNamePresetState();
            Normalize(state);
            return JsonUtility.ToJson(state);
        }

        internal static BlendShapeFbxPreset Find(
            BlendShapeNamePresetState state,
            string assetGuid)
        {
            return state?.presets?.FirstOrDefault(preset =>
                preset != null && string.Equals(
                    preset.assetGuid,
                    assetGuid,
                    StringComparison.Ordinal));
        }

        internal static BlendShapeFbxPreset CreatePreset(
            GameObject fbxAsset,
            IReadOnlyList<string> separators)
        {
            var assetPath = AssetDatabase.GetAssetPath(fbxAsset);
            if (!IsFbxAssetPath(assetPath))
            {
                return null;
            }

            var preset = new BlendShapeFbxPreset
            {
                assetGuid = AssetDatabase.AssetPathToGUID(assetPath),
                assetPath = assetPath,
                name = Path.GetFileNameWithoutExtension(assetPath)
            };
            var meshes = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Mesh>()
                .OrderBy(mesh => mesh.name, StringComparer.Ordinal)
                .ToArray();
            for (var meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                var mesh = meshes[meshIndex];
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    mesh,
                    out _,
                    out long meshLocalId);
                for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    preset.mappings.Add(BlendShapeNameClassifier.Classify(
                        meshLocalId,
                        mesh.name,
                        mesh.GetBlendShapeName(shapeIndex)));
                }
            }

            ApplyHeaders(preset, separators);
            return preset;
        }

        internal static void ApplyHeaders(
            BlendShapeFbxPreset preset,
            IReadOnlyList<string> separators)
        {
            for (var index = 0; index < (preset?.mappings?.Count ?? 0); index++)
            {
                var mapping = preset.mappings[index];
                mapping.headerText = FaceExpressionClipEditor.TryGetHeader(
                    mapping.shapeName,
                    separators,
                    out var headerText)
                    ? headerText
                    : string.Empty;
            }
        }

        internal static void Upsert(
            BlendShapeNamePresetState state,
            BlendShapeFbxPreset preset)
        {
            Normalize(state);
            if (preset == null || string.IsNullOrEmpty(preset.assetGuid))
            {
                return;
            }

            state.presets.RemoveAll(candidate => string.Equals(
                candidate.assetGuid,
                preset.assetGuid,
                StringComparison.Ordinal));
            state.presets.Add(preset);
            Normalize(state);
        }

        internal static GameObject ResolveSourceFbx(GameObject avatar)
        {
            if (avatar == null)
            {
                return null;
            }

            var avatarPath = AssetDatabase.GetAssetPath(avatar);
            if (IsFbxAssetPath(avatarPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(avatarPath);
            }

            var originalSource =
                PrefabUtility.GetCorrespondingObjectFromOriginalSource(avatar);
            var sourcePath = originalSource == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(originalSource);
            return IsFbxAssetPath(sourcePath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath)
                : null;
        }

        private static bool IsFbxAssetPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   string.Equals(
                       Path.GetExtension(assetPath),
                       ".fbx",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void Normalize(BlendShapeNamePresetState state)
        {
            state.presets = (state.presets ?? new List<BlendShapeFbxPreset>())
                .Where(preset => preset != null && !string.IsNullOrEmpty(preset.assetGuid))
                .GroupBy(preset => preset.assetGuid, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderBy(preset => preset.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var index = 0; index < state.presets.Count; index++)
            {
                var preset = state.presets[index];
                preset.assetPath = preset.assetPath ?? string.Empty;
                preset.name = string.IsNullOrWhiteSpace(preset.name)
                    ? Path.GetFileNameWithoutExtension(preset.assetPath)
                    : preset.name.Trim();
                preset.mappings = (preset.mappings ?? new List<BlendShapeNameMapping>())
                    .Where(mapping => mapping != null && !string.IsNullOrEmpty(mapping.shapeName))
                    .ToList();
            }
        }
    }
}
