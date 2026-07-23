using Ee4v.AssetManager.Infrastructure;
using Ee4v.Core.Settings;

namespace Ee4v.AssetManager.Composition
{
    internal sealed class AssetManagerInfrastructureSettingsAdapter :
        IAssetManagerInfrastructureSettings
    {
        public string GlobalPath =>
            SettingApi.Get(AssetManagerDefinitions.Ee4vGlobalPath);

        public string BlmDatabasePath =>
            SettingApi.Get(AssetManagerDefinitions.BlmDatabasePath);

        public string EagleLibraryPath =>
            SettingApi.Get(AssetManagerDefinitions.EagleLibraryPath);

        public string SourcePriority =>
            SettingApi.Get(AssetManagerDefinitions.SourcePriority);

        public string AvatarNames =>
            SettingApi.Get(AssetManagerDefinitions.AvatarNames);

        public string VersionGroupRegex =>
            SettingApi.Get(AssetManagerDefinitions.VersionGroupRegex);

        public bool ShowUnityPackageImportDialog =>
            SettingApi.Get(AssetManagerDefinitions.ShowUnityPackageImportDialog);
    }
}
