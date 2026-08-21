using System.Collections.Generic;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.FaceExpression
{
    [InitializeOnLoad]
    internal static class FaceExpressionSettings
    {
        internal const string DefaultBlendShapeNamePattern =
            @"^(?<group>[^_]+)_(?<role>[^_]+)(?:(?:_(?<variation>[^_]+))?_(?<side>L|R)|(?:_(?<variation>[^_]+))?)$";

        internal static readonly SettingDefinition<string> BlendShapeSeparators =
            new SettingDefinition<string>(
                "faceExpression.blendShapeSeparators",
                SettingScope.User,
                "FaceExpression",
                "settings.section.editor",
                "settings.blendShapeSeparators.label",
                "settings.blendShapeSeparators.tooltip",
                "-,*",
                keywords: new[]
                {
                    "avatar",
                    "blendshape",
                    "separator",
                    "header"
                });

        internal static readonly SettingDefinition<string> BlendShapeNamePattern =
            new SettingDefinition<string>(
                "faceExpression.blendShapeNamePattern",
                SettingScope.User,
                "FaceExpression",
                "settings.section.editor",
                "settings.blendShapeNamePattern.label",
                "settings.blendShapeNamePattern.tooltip",
                BlendShapeNamePresetSetting.DefaultValue,
                order: 10,
                validator: BlendShapeNamePresetSetting.Validate,
                keywords: new[]
                {
                    "avatar",
                    "blendshape",
                    "regex",
                    "role",
                    "variation",
                    "side"
                });

        static FaceExpressionSettings()
        {
            CommaSeparatedListSettingDrawer.Register(
                BlendShapeSeparators);
            BlendShapeNamePresetSetting.RegisterDrawer(
                BlendShapeNamePattern);
            CoreSettings.Current.Register(BlendShapeSeparators);
            CoreSettings.Current.Register(BlendShapeNamePattern);
        }

        internal static IReadOnlyList<string> GetSeparators(
            ISettingsService settings = null)
        {
            settings = settings ?? CoreSettings.Current;
            settings.Register(BlendShapeSeparators);
            return CommaSeparatedListSettingDrawer.ParseItems(
                settings.Get(BlendShapeSeparators));
        }

        internal static string GetNamePattern(ISettingsService settings = null)
        {
            settings = settings ?? CoreSettings.Current;
            settings.Register(BlendShapeNamePattern);
            return BlendShapeNamePresetSetting.GetPattern(
                settings.Get(BlendShapeNamePattern));
        }
    }
}
