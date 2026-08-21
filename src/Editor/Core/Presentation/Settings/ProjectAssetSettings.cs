using System;
using System.IO;
using Ee4v.Core.I18n;
using UnityEditor;

namespace Ee4v.Core.Settings
{
    [InitializeOnLoad]
    internal static class ProjectAssetSettings
    {
        internal const string DefaultRootFolderName = "!ee4vAsset";

        internal static readonly SettingDefinition<string> RootFolderName =
            new SettingDefinition<string>(
                "core.assets.rootFolderName",
                SettingScope.Project,
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

        static ProjectAssetSettings()
        {
            Register(CoreSettings.Current);
        }

        internal static void Register(ISettingsService settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Register(RootFolderName);
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
            return "Assets/" + settings.Get(RootFolderName);
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
