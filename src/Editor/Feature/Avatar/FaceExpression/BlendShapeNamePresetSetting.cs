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
        public string side;
        public bool mouthMorph;
        public string appearancePart;
        public string appearanceGroup;
    }

    internal static class BlendShapeAppearancePart
    {
        internal const string Expression = "expression";
        internal const string Head = "head";
        internal const string Chest = "chest";
        internal const string Waist = "waist";
        internal const string Shoulders = "shoulders";
        internal const string Arms = "arms";
        internal const string Hands = "hands";
        internal const string Legs = "legs";
        internal const string Feet = "feet";
        internal const string Other = "other";
    }

    internal static class BlendShapeNamePresetSetting
    {
        internal static void RegisterDrawer(
            SettingDefinition<string> definition,
            IBlendShapePresetStore storage)
        {
            SettingDrawerApi.Register(definition, context =>
            {
                var root = new VisualElement();

                void Rebuild()
                {
                    root.Clear();
                    var presets = storage.Load().presets;
                    root.Add(new ListField<BlendShapeFbxPreset>(
                        new ListFieldState<BlendShapeFbxPreset>(
                            presets,
                            (preset, _) =>
                                new InputField(
                                    new InputFieldState(preset.name))
                                {
                                    IsReadOnly = true
                                },
                            tooltip: context.Tooltip)));

                    var openFolder = new UiButton(
                        I18N.Get("settings.blendShapePresets.openFolder"),
                        storage.OpenDirectory);
                    openFolder.tooltip = context.Tooltip ?? string.Empty;
                    root.Add(openFolder);
                }

                void OnStorageChanged()
                {
                    root.schedule.Execute(Rebuild);
                }

                storage.Changed += OnStorageChanged;
                root.RegisterCallback<DetachFromPanelEvent>(
                    _ => storage.Changed -= OnStorageChanged);
                Rebuild();
                return root;
            });
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
            IReadOnlyList<string> separators,
            GameObject avatar = null)
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
            ApplyMouthMorphSuggestions(preset, avatar, separators);
            return preset;
        }

        private static void ApplyMouthMorphSuggestions(
            BlendShapeFbxPreset preset,
            GameObject avatar,
            IReadOnlyList<string> separators)
        {
            if (!VrchatAvatarDescriptorAdapter.TryReadMouthMorphSuggestion(
                    avatar,
                    separators,
                    out var mesh,
                    out var shapeNames) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    mesh,
                    out var assetGuid,
                    out long meshLocalId) ||
                !string.Equals(
                    preset.assetGuid,
                    assetGuid,
                    StringComparison.Ordinal))
            {
                return;
            }

            var names = new HashSet<string>(
                shapeNames,
                StringComparer.Ordinal);
            foreach (var mapping in preset.mappings)
            {
                mapping.mouthMorph = mapping.meshLocalId == meshLocalId &&
                                     names.Contains(mapping.shapeName);
            }
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

        internal static void Normalize(BlendShapeNamePresetState state)
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
                foreach (var mapping in preset.mappings)
                {
                    mapping.appearancePart = mapping.appearancePart ??
                                             string.Empty;
                    mapping.appearanceGroup = mapping.appearanceGroup ??
                                              string.Empty;
                }
            }
        }
    }
}
