using System;
using System.IO;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    [InitializeOnLoad]
    internal static class ProjectAssetSettings
    {
        internal const string DefaultRootFolderName = "!ee4vAsset";

        internal static readonly SettingDefinition<string> RootFolderName =
            new SettingDefinition<string>(
                "core.assets.rootFolderName",
                SettingScope.User,
                "Core",
                "settings.section.assets",
                "settings.assetRootFolderName.label",
                "settings.assetRootFolderName.tooltip",
                DefaultRootFolderName,
                validator: ValidateRootFolderName,
                keywords: new[]
                {
                    "assets",
                    "folder",
                    "root",
                    "scene",
                    "face clip"
                });

        internal static readonly SettingDefinition<bool> UseProjectRootFolderName =
            new SettingDefinition<bool>(
                "core.assets.useProjectRootFolderName",
                SettingScope.Project,
                "Core",
                "settings.section.assets",
                "settings.useProjectAssetRootFolderName.label",
                "settings.useProjectAssetRootFolderName.tooltip",
                false,
                order: 0,
                keywords: new[]
                {
                    "assets",
                    "folder",
                    "root",
                    "override"
                });

        internal static readonly SettingDefinition<string> ProjectRootFolderName =
            new SettingDefinition<string>(
                "core.assets.projectRootFolderName",
                SettingScope.Project,
                "Core",
                "settings.section.assets",
                "settings.projectAssetRootFolderName.label",
                "settings.projectAssetRootFolderName.tooltip",
                DefaultRootFolderName,
                order: 1,
                validator: ValidateRootFolderName,
                keywords: new[]
                {
                    "assets",
                    "folder",
                    "root",
                    "project"
                });

        static ProjectAssetSettings()
        {
            Register(CoreSettings.Current);
            SettingDrawerApi.Register(
                ProjectRootFolderName,
                CreateProjectRootFolderNameField);
        }

        internal static void Register(ISettingsService settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Register(RootFolderName);
            settings.Register(UseProjectRootFolderName);
            settings.Register(ProjectRootFolderName);
        }

        internal static string GetAssetFolder(
            string childFolderName,
            ISettingsService settings = null)
        {
            if (!IsValidFolderName(childFolderName))
            {
                throw new ArgumentException(
                    "Asset child folder name is invalid.",
                    nameof(childFolderName));
            }

            return GetRootAssetFolder(settings) +
                   "/" + childFolderName;
        }

        internal static string GetRootAssetFolder(
            ISettingsService settings = null)
        {
            settings = settings ?? CoreSettings.Current;
            Register(settings);
            var definition = settings.Get(UseProjectRootFolderName)
                ? ProjectRootFolderName
                : RootFolderName;
            return "Assets/" + settings.Get(definition);
        }

        internal static string EnsureAssetFolder(
            string childFolderName,
            ISettingsService settings = null)
        {
            var path = GetAssetFolder(childFolderName, settings);
            var parent = "Assets";
            var segments = path.Substring("Assets/".Length)
                .Split('/');
            for (var index = 0; index < segments.Length; index++)
            {
                var current = parent + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(current))
                {
                    var guid = AssetDatabase.CreateFolder(
                        parent,
                        segments[index]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new InvalidOperationException(
                            "Failed to create asset folder: " + current);
                    }
                }

                parent = current;
            }

            return path;
        }

        private static VisualElement CreateProjectRootFolderNameField(
            SettingDrawerContext<string> context)
        {
            var settings = CoreSettings.Current;
            var field = UiTextFactory.CreateTextField();
            field.tooltip = context.Tooltip;
            field.value = context.Value ?? string.Empty;
            field.SetEnabled(settings.Get(UseProjectRootFolderName));
            field.RegisterValueChangedCallback(
                evt => context.NotifyValueChanged(evt.newValue));

            void OnSettingChanged(
                object sender,
                SettingChangedEventArgs args)
            {
                if (ReferenceEquals(
                        args.Definition,
                        UseProjectRootFolderName))
                {
                    field.SetEnabled((bool)args.Value);
                }
            }

            settings.Changed += OnSettingChanged;
            field.RegisterCallback<DetachFromPanelEvent>(
                _ => settings.Changed -= OnSettingChanged);
            return field;
        }

        private static SettingValidationResult ValidateRootFolderName(
            string value)
        {
            return IsValidFolderName(value)
                ? SettingValidationResult.Success
                : SettingValidationResult.Error(I18N.GetForScope(
                    "Core",
                    "settings.validation.assetRootFolderName"));
        }

        private static bool IsValidFolderName(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
                   !string.Equals(value, ".", StringComparison.Ordinal) &&
                   !string.Equals(value, "..", StringComparison.Ordinal) &&
                   value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                   value.IndexOf('/') < 0 &&
                   value.IndexOf('\\') < 0;
        }
    }
}
