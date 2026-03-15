using System;
using System.Linq;
using Ee4v.I18n;
using Ee4v.Settings;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Phase1
{
    internal static class Phase1Definitions
    {
        private static bool _registered;

        public static readonly SettingDefinition<string> Language = new SettingDefinition<string>(
            "phase1.i18n.language",
            SettingScope.User,
            "settings.section.localization",
            "settings.language.label",
            "settings.language.tooltip",
            "ja-JP",
            order: 0,
            validator: ValidateLocale,
            customDrawer: DrawLocaleField());

        public static readonly SettingDefinition<string> FallbackLanguage = new SettingDefinition<string>(
            "phase1.i18n.fallbackLanguage",
            SettingScope.User,
            "settings.section.localization",
            "settings.fallbackLanguage.label",
            "settings.fallbackLanguage.tooltip",
            "en-US",
            order: 1,
            validator: ValidateLocale,
            customDrawer: DrawLocaleField());

        public static readonly SettingDefinition<bool> EnableHierarchyItemStub = new SettingDefinition<bool>(
            "phase1.injector.hierarchyItem.enabled",
            SettingScope.User,
            "settings.section.injectorUser",
            "settings.hierarchyItemStub.label",
            "settings.hierarchyItemStub.tooltip",
            true,
            order: 0);

        public static readonly SettingDefinition<bool> EnableHierarchyHeaderStub = new SettingDefinition<bool>(
            "phase1.injector.hierarchyHeader.enabled",
            SettingScope.User,
            "settings.section.injectorUser",
            "settings.hierarchyHeaderStub.label",
            "settings.hierarchyHeaderStub.tooltip",
            true,
            order: 1);

        public static readonly SettingDefinition<bool> EnableProjectItemStub = new SettingDefinition<bool>(
            "phase1.injector.projectItem.enabled",
            SettingScope.User,
            "settings.section.injectorUser",
            "settings.projectItemStub.label",
            "settings.projectItemStub.tooltip",
            true,
            order: 2);

        public static readonly SettingDefinition<bool> EnableProjectToolbarStub = new SettingDefinition<bool>(
            "phase1.injector.projectToolbar.enabled",
            SettingScope.User,
            "settings.section.injectorUser",
            "settings.projectToolbarStub.label",
            "settings.projectToolbarStub.tooltip",
            true,
            order: 3);

        public static readonly SettingDefinition<string> HierarchyBadgeText = new SettingDefinition<string>(
            "phase1.injector.hierarchyBadgeText",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.hierarchyBadgeText.label",
            "settings.hierarchyBadgeText.tooltip",
            "stub",
            order: 0,
            validator: ValidateNonEmpty);

        public static readonly SettingDefinition<string> HierarchyHeaderText = new SettingDefinition<string>(
            "phase1.injector.hierarchyHeaderText",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.hierarchyHeaderText.label",
            "settings.hierarchyHeaderText.tooltip",
            "ee4v phase1 hierarchy",
            order: 1,
            validator: ValidateNonEmpty);

        public static readonly SettingDefinition<string> ProjectToolbarText = new SettingDefinition<string>(
            "phase1.injector.projectToolbarText",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.projectToolbarText.label",
            "settings.projectToolbarText.tooltip",
            "ee4v phase1 toolbar",
            order: 2,
            validator: ValidateNonEmpty);

        public static readonly SettingDefinition<int> ToolbarButtonWidth = new SettingDefinition<int>(
            "phase1.injector.toolbarButtonWidth",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.toolbarButtonWidth.label",
            "settings.toolbarButtonWidth.tooltip",
            96,
            order: 3,
            validator: value => value >= 60 ? SettingValidationResult.Success : SettingValidationResult.Error(I18N.Get("settings.validation.toolbarButtonWidth")));

        public static readonly SettingDefinition<Color> HierarchyAccentColor = new SettingDefinition<Color>(
            "phase1.injector.hierarchyAccentColor",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.hierarchyAccentColor.label",
            "settings.hierarchyAccentColor.tooltip",
            new Color(0.25f, 0.72f, 0.92f, 1f),
            order: 4);

        public static readonly SettingDefinition<Color> ProjectAccentColor = new SettingDefinition<Color>(
            "phase1.injector.projectAccentColor",
            SettingScope.Project,
            "settings.section.injectorProject",
            "settings.projectAccentColor.label",
            "settings.projectAccentColor.tooltip",
            new Color(0.95f, 0.62f, 0.18f, 1f),
            order: 5);

        public static void RegisterAll()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;

            SettingApi.Register(Language);
            SettingApi.Register(FallbackLanguage);
            SettingApi.Register(EnableHierarchyItemStub);
            SettingApi.Register(EnableHierarchyHeaderStub);
            SettingApi.Register(EnableProjectItemStub);
            SettingApi.Register(EnableProjectToolbarStub);
            SettingApi.Register(HierarchyBadgeText);
            SettingApi.Register(HierarchyHeaderText);
            SettingApi.Register(ProjectToolbarText);
            SettingApi.Register(ToolbarButtonWidth);
            SettingApi.Register(HierarchyAccentColor);
            SettingApi.Register(ProjectAccentColor);

            SettingApi.Changed -= OnSettingChanged;
            SettingApi.Changed += OnSettingChanged;
        }

        private static void OnSettingChanged(SettingDefinitionBase definition, object value)
        {
            if (definition == Language || definition == FallbackLanguage)
            {
                I18N.Reload();
            }
        }

        private static SettingValidationResult ValidateLocale(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                return SettingValidationResult.Error(I18N.Get("settings.validation.locale"));
            }

            return SettingValidationResult.Success;
        }

        private static SettingValidationResult ValidateNonEmpty(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SettingValidationResult.Error(I18N.Get("settings.validation.required"));
            }

            return SettingValidationResult.Success;
        }

        private static Func<SettingDrawerContext<string>, string> DrawLocaleField()
        {
            return context =>
            {
                var languages = I18N.GetAvailableLanguages();
                if (languages.Count == 0)
                {
                    return EditorGUILayout.TextField(context.Label, context.Value ?? string.Empty);
                }

                var options = languages.ToArray();
                var currentIndex = Math.Max(0, Array.IndexOf(options, context.Value));
                var nextIndex = EditorGUILayout.Popup(context.Label, currentIndex, options);
                return options[nextIndex];
            };
        }
    }
}
