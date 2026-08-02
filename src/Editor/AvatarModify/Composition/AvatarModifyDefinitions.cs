using System;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;

namespace Ee4v.AvatarModify.Composition
{
    internal static class AvatarModifyDefinitions
    {
        internal static readonly SettingDefinition<string> VariantRoot =
            new SettingDefinition<string>(
                "avatarModify.variantRoot",
                SettingScope.Project,
                "AvatarModify",
                "settings.section.variant",
                "settings.variantRoot.label",
                "settings.variantRoot.tooltip",
                "Assets/AvatarVariants",
                order: 0,
                validator: value =>
                    !string.IsNullOrWhiteSpace(value) &&
                    (value == "Assets" ||
                     value.Replace('\\', '/').StartsWith(
                         "Assets/",
                         StringComparison.Ordinal))
                        ? SettingValidationResult.Success
                        : SettingValidationResult.Error(
                            I18N.Get(
                                "settings.validation.assetsPath")));

        internal static void RegisterAll(ISettingsService settings)
        {
            settings.Register(VariantRoot);
        }
    }
}
