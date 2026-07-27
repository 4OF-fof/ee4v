using System;

namespace Ee4v.AssetManager.Infrastructure
{
    internal interface IAssetManagerInfrastructureSettings
    {
        string GlobalPath { get; }
        string BlmDatabasePath { get; }
        string EagleLibraryPath { get; }
        string SourcePriority { get; }
        string AvatarNames { get; }
        string VersionGroupRegex { get; }
        bool ShowUnityPackageImportDialog { get; }
    }

    internal static class AssetManagerInfrastructureSettings
    {
        private static IAssetManagerInfrastructureSettings _current;

        internal static IAssetManagerInfrastructureSettings Current =>
            _current ?? throw new InvalidOperationException(
                "AssetManager infrastructure settings have not been configured.");

        internal static void Configure(IAssetManagerInfrastructureSettings settings)
        {
            _current = settings ?? throw new ArgumentNullException(nameof(settings));
        }
    }
}
