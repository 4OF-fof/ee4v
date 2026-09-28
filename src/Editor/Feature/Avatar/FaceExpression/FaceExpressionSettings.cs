using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    [InitializeOnLoad]
    internal static class FaceExpressionSettings
    {
        internal static readonly SettingDefinition<string> BlendShapeSeparators =
            new SettingDefinition<string>(
                "faceExpression.blendShapeSeparators",
                SettingScope.User,
                "FaceExpression",
                "settings.section.editor",
                "settings.blendShapeSeparators.label",
                "settings.blendShapeSeparators.tooltip",
                "-,─,=,*",
                keywords: new[]
                {
                    "avatar",
                    "blendshape",
                    "separator",
                    "header"
                });

        internal static readonly SettingDefinition<string> BlendShapePresets =
            new SettingDefinition<string>(
                "faceExpression.blendShapePresets",
                SettingScope.User,
                "FaceExpression",
                "settings.section.editor",
                "settings.blendShapePresets.label",
                "settings.blendShapePresets.tooltip",
                string.Empty,
                order: 10,
                keywords: new[]
                {
                    "avatar",
                    "blendshape",
                    "fbx",
                    "preset",
                    "role",
                    "side"
                });

        internal static readonly SettingDefinition<bool> MenuIconsDisabled =
            new SettingDefinition<bool>(
                "faceExpression.menuIconsDisabled",
                SettingScope.User,
                "FaceExpression",
                "settings.section.editor",
                "settings.menuIconsDisabled.label",
                "settings.menuIconsDisabled.tooltip",
                false,
                order: 20,
                keywords: new[]
                {
                    "avatar",
                    "expression",
                    "menu",
                    "icon",
                    "preview"
                });

        static FaceExpressionSettings()
        {
            CommaSeparatedListSettingDrawer.Register(
                BlendShapeSeparators);
            BlendShapeNamePresetSetting.RegisterDrawer(
                BlendShapePresets,
                BlendShapePresetStorage.Shared);
            CoreSettings.Current.Register(BlendShapeSeparators);
            CoreSettings.Current.Register(BlendShapePresets);
            CoreSettings.Current.Register(MenuIconsDisabled);
        }

        internal static IReadOnlyList<string> GetSeparators(
            ISettingsService settings = null)
        {
            settings = settings ?? CoreSettings.Current;
            settings.Register(BlendShapeSeparators);
            return CommaSeparatedListSettingDrawer.ParseItems(
                settings.Get(BlendShapeSeparators));
        }

        internal static BlendShapeNamingRule GetNameRule(
            IBlendShapePresetStore presetStore)
        {
            if (presetStore == null)
            {
                throw new ArgumentNullException(
                    nameof(presetStore));
            }

            return new BlendShapeNamingRule(
                presetStore.Load());
        }

        internal static bool GetMenuIconsDisabled(
            ISettingsService settings = null)
        {
            settings = settings ?? CoreSettings.Current;
            settings.Register(MenuIconsDisabled);
            return settings.Get(MenuIconsDisabled);
        }

        internal static bool EnsureNamePreset(
            GameObject avatar,
            ISettingsService settings,
            IBlendShapePresetStore presetStore)
        {
            settings = settings ?? CoreSettings.Current;
            if (presetStore == null)
            {
                throw new ArgumentNullException(
                    nameof(presetStore));
            }

            var sourceFbx = BlendShapeNamePresetSetting.ResolveSourceFbx(
                avatar);
            if (sourceFbx == null)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(sourceFbx);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var state = presetStore.Load();
            if (BlendShapeNamePresetSetting.Find(state, assetGuid) != null)
            {
                return false;
            }

            var preset = BlendShapeNamePresetSetting.CreatePreset(
                sourceFbx,
                GetSeparators(settings),
                avatar);
            if (preset == null)
            {
                return false;
            }

            presetStore.Save(preset);
            return true;
        }

        internal static void EnsureNamePresets(
            GameObject avatar,
            IEnumerable<Mesh> meshes,
            ISettingsService settings,
            IBlendShapePresetStore presetStore)
        {
            settings = settings ?? CoreSettings.Current;
            if (presetStore == null)
            {
                throw new ArgumentNullException(nameof(presetStore));
            }
            if (meshes == null)
            {
                return;
            }

            var state = presetStore.Load();
            var separators = GetSeparators(settings);
            foreach (var assetPath in meshes
                         .Where(mesh => mesh != null)
                         .Select(AssetDatabase.GetAssetPath)
                         .Where(path => string.Equals(
                             Path.GetExtension(path),
                             ".fbx",
                             StringComparison.OrdinalIgnoreCase))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (BlendShapeNamePresetSetting.Find(state, assetGuid) != null)
                {
                    continue;
                }

                var sourceFbx = AssetDatabase.LoadAssetAtPath<GameObject>(
                    assetPath);
                var preset = BlendShapeNamePresetSetting.CreatePreset(
                    sourceFbx,
                    separators,
                    avatar);
                if (preset == null)
                {
                    continue;
                }

                presetStore.Save(preset);
                BlendShapeNamePresetSetting.Upsert(state, preset);
            }
        }
    }

    public readonly struct FaceExpressionShapeMapping
    {
        internal FaceExpressionShapeMapping(
            string appearancePart,
            string appearanceGroup,
            string role)
        {
            AppearancePart = appearancePart;
            AppearanceGroup = appearanceGroup;
            Role = role;
        }

        public string AppearancePart { get; }
        public string AppearanceGroup { get; }
        public string Role { get; }
    }

    public sealed class FaceExpressionShapeNamingSnapshot
    {
        private readonly IReadOnlyList<string> _separators;
        private readonly BlendShapeNamingRule _namingRule;

        private FaceExpressionShapeNamingSnapshot(
            IReadOnlyList<string> separators,
            BlendShapeNamingRule namingRule)
        {
            _separators = separators;
            _namingRule = namingRule;
        }

        public static event Action PresetsChanged
        {
            add { BlendShapePresetStorage.Shared.Changed += value; }
            remove { BlendShapePresetStorage.Shared.Changed -= value; }
        }

        public static bool IsSeparatorSetting(
            SettingDefinitionBase definition)
        {
            return ReferenceEquals(
                definition,
                FaceExpressionSettings.BlendShapeSeparators);
        }

        public static FaceExpressionShapeNamingSnapshot Create(
            GameObject avatar,
            IEnumerable<Mesh> meshes)
        {
            var presetStore = BlendShapePresetStorage.Shared;
            FaceExpressionSettings.EnsureNamePresets(
                avatar,
                meshes,
                null,
                presetStore);
            return new FaceExpressionShapeNamingSnapshot(
                FaceExpressionSettings.GetSeparators(),
                FaceExpressionSettings.GetNameRule(presetStore));
        }

        public bool IsHeader(string shapeName)
        {
            return FaceExpressionClipEditor.TryGetHeader(
                shapeName,
                _separators,
                out _);
        }

        public bool TryGetMapping(
            string assetGuid,
            long meshLocalId,
            string shapeName,
            out FaceExpressionShapeMapping result)
        {
            if (!_namingRule.TryGetMapping(
                    assetGuid,
                    meshLocalId,
                    shapeName,
                    out var mapping))
            {
                result = default;
                return false;
            }

            result = new FaceExpressionShapeMapping(
                mapping.appearancePart,
                mapping.appearanceGroup,
                mapping.role);
            return true;
        }
    }
}
