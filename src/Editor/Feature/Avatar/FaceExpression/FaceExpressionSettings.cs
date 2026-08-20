using System.Collections.Generic;
using Ee4v.Core.Settings;
using UnityEditor;

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
                "-",
                keywords: new[]
                {
                    "avatar",
                    "blendshape",
                    "separator",
                    "header"
                });

        static FaceExpressionSettings()
        {
            CommaSeparatedListSettingDrawer.Register(
                BlendShapeSeparators);
            CoreSettings.Current.Register(BlendShapeSeparators);
        }

        internal static IReadOnlyList<string> GetSeparators(
            ISettingsService settings = null)
        {
            settings = settings ?? CoreSettings.Current;
            settings.Register(BlendShapeSeparators);
            return CommaSeparatedListSettingDrawer.ParseItems(
                settings.Get(BlendShapeSeparators));
        }
    }
}
