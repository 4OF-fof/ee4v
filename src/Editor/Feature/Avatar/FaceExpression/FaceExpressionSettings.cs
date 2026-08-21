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
                SettingScope.Project,
                "FaceExpression",
                "settings.section.editor",
                "settings.blendShapePresets.label",
                "settings.blendShapePresets.tooltip",
                BlendShapeNamePresetSetting.DefaultValue,
                order: 10,
                keywords: new[]
                {
                    "avatar",
                    "blendshape",
                    "fbx",
                    "preset",
                    "role",
                    "variation",
                    "side"
                });

        static FaceExpressionSettings()
        {
            CommaSeparatedListSettingDrawer.Register(
                BlendShapeSeparators);
            BlendShapeNamePresetSetting.RegisterDrawer(
                BlendShapePresets);
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
            ISettingsService settings = null)
        {
            settings = settings ?? CoreSettings.Current;
            settings.Register(BlendShapePresets);
            return new BlendShapeNamingRule(
                BlendShapeNamePresetSetting.Parse(
                    settings.Get(BlendShapePresets)));
        }

        internal static bool EnsureNamePreset(
            GameObject avatar,
            ISettingsService settings = null)
        {
            settings = settings ?? CoreSettings.Current;
            settings.Register(BlendShapePresets);
            var sourceFbx = BlendShapeNamePresetSetting.ResolveSourceFbx(
                avatar);
            if (sourceFbx == null)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(sourceFbx);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var state = BlendShapeNamePresetSetting.Parse(
                settings.Get(BlendShapePresets));
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

            BlendShapeNamePresetSetting.Upsert(state, preset);
            settings.Set(
                BlendShapePresets,
                BlendShapeNamePresetSetting.Serialize(state));
            return true;
        }
    }
}
