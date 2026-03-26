using System;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using UnityEngine;

namespace Ee4v.Phase1
{
    internal static class Phase1Definitions
    {
        private static bool _registered;

        public static readonly SettingDefinition<bool> EnableHierarchyItemStub = new SettingDefinition<bool>(
            "phase1.injector.hierarchyItem.enabled",
            SettingScope.User,
            "settings.section.injectorUser",
            "settings.hierarchyItemStub.label",
            "settings.hierarchyItemStub.tooltip",
            true,
            order: 0);

        public static readonly SettingDefinition<bool> EnableProjectItemStub = new SettingDefinition<bool>(
            "phase1.injector.projectItem.enabled",
            SettingScope.User,
            "settings.section.injectorUser",
            "settings.projectItemStub.label",
            "settings.projectItemStub.tooltip",
            true,
            order: 1);

        public static readonly SettingDefinition<bool> EnableProjectToolbarStub = new SettingDefinition<bool>(
            "phase1.injector.projectToolbar.enabled",
            SettingScope.User,
            "settings.section.injectorUser",
            "settings.projectToolbarStub.label",
            "settings.projectToolbarStub.tooltip",
            true,
            order: 2);

        public static readonly SettingDefinition<string> HierarchyBadgeText = new SettingDefinition<string>(
            "phase1.injector.hierarchyBadgeText",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.hierarchyBadgeText.label",
            "settings.hierarchyBadgeText.tooltip",
            "stub",
            order: 0,
            validator: ValidateNonEmpty);

        public static readonly SettingDefinition<string> ProjectToolbarText = new SettingDefinition<string>(
            "phase1.injector.projectToolbarText",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.projectToolbarText.label",
            "settings.projectToolbarText.tooltip",
            "ee4v phase1 toolbar",
            order: 1,
            validator: ValidateNonEmpty);

        public static readonly SettingDefinition<int> ToolbarButtonWidth = new SettingDefinition<int>(
            "phase1.injector.toolbarButtonWidth",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.toolbarButtonWidth.label",
            "settings.toolbarButtonWidth.tooltip",
            96,
            order: 2,
            validator: value => value >= 60 ? SettingValidationResult.Success : SettingValidationResult.Error(I18N.Get("settings.validation.toolbarButtonWidth")));

        public static readonly SettingDefinition<Color> HierarchyAccentColor = new SettingDefinition<Color>(
            "phase1.injector.hierarchyAccentColor",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.hierarchyAccentColor.label",
            "settings.hierarchyAccentColor.tooltip",
            new Color(0.25f, 0.72f, 0.92f, 1f),
            order: 3);

        public static readonly SettingDefinition<Color> ProjectAccentColor = new SettingDefinition<Color>(
            "phase1.injector.projectAccentColor",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.projectAccentColor.label",
            "settings.projectAccentColor.tooltip",
            new Color(0.95f, 0.62f, 0.18f, 1f),
            order: 4);

        public static void RegisterAll()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;

            SettingApi.Register(EnableHierarchyItemStub);
            SettingApi.Register(EnableProjectItemStub);
            SettingApi.Register(EnableProjectToolbarStub);
            SettingApi.Register(HierarchyBadgeText);
            SettingApi.Register(ProjectToolbarText);
            SettingApi.Register(ToolbarButtonWidth);
            SettingApi.Register(HierarchyAccentColor);
            SettingApi.Register(ProjectAccentColor);
        }

        private static SettingValidationResult ValidateNonEmpty(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SettingValidationResult.Error(I18N.Get("settings.validation.required"));
            }

            return SettingValidationResult.Success;
        }
    }
}
