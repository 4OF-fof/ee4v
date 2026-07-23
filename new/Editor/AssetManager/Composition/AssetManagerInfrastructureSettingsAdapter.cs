using Ee4v.AssetManager.Infrastructure;
using Ee4v.Core.Settings;

namespace Ee4v.AssetManager.Composition
{
    internal sealed class AssetManagerInfrastructureSettingsAdapter :
        IAssetManagerInfrastructureSettings
    {
        private readonly ISettingsService _settings;

        internal AssetManagerInfrastructureSettingsAdapter(ISettingsService settings)
        {
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
        }

        public string GlobalPath =>
            _settings.Get(AssetManagerDefinitions.Ee4vGlobalPath);

        public string BlmDatabasePath =>
            _settings.Get(AssetManagerDefinitions.BlmDatabasePath);

        public string EagleLibraryPath =>
            _settings.Get(AssetManagerDefinitions.EagleLibraryPath);

        public string SourcePriority =>
            _settings.Get(AssetManagerDefinitions.SourcePriority);

        public string AvatarNames =>
            _settings.Get(AssetManagerDefinitions.AvatarNames);

        public string VersionGroupRegex =>
            _settings.Get(AssetManagerDefinitions.VersionGroupRegex);

        public bool ShowUnityPackageImportDialog =>
            _settings.Get(AssetManagerDefinitions.ShowUnityPackageImportDialog);
    }
}
