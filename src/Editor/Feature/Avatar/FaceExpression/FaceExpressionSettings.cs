using System;
using System.Collections.Generic;
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

        static FaceExpressionSettings()
        {
            CommaSeparatedListSettingDrawer.Register(
                BlendShapeSeparators);
            BlendShapeNamePresetSetting.RegisterDrawer(
                BlendShapePresets,
                BlendShapePresetStorage.Shared);
            CoreSettings.Current.Register(BlendShapeSeparators);
            CoreSettings.Current.Register(BlendShapePresets);
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
                GetSeparators(settings));
            if (preset == null)
            {
                return false;
            }

            presetStore.Save(preset);
            return true;
        }
    }
}
