using System;
using Ee4v.AssetManager.Application;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure.Eagle;
using Ee4v.AssetManager.Infrastructure.Ee4v;
using Ee4v.AssetManager.Infrastructure.Persistence;

namespace Ee4v.AssetManager.Infrastructure
{
    public static class AssetManagerFactory
    {
        public static IAssetManager Open(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException(
                    "Database path is required.",
                    nameof(databasePath));
            }

            return new AssetManagerService(
                new SqliteAssetManagerStore(databasePath),
                new EagleAssetSource(),
                new Ee4vAssetSource(),
                new AssetTargetImporter(),
                new AssetFileAnalyzer(databasePath),
                new AssetThumbnailProvider(databasePath));
        }

        public static IAssetVariantManager OpenVariants(string databasePath, IAssetManager manager)
        {
            if (manager == null) { throw new ArgumentNullException(nameof(manager)); }
            var libraryPath = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(databasePath));
            var repository = new GitAssetVariantRepository(libraryPath);
            var service = new AssetVariantService(manager, repository,
                new SqliteAssetManagerStore(databasePath),
                new UnityAssetVariantWorkspace(manager, repository));
            service.RebuildIndex();
            return service;
        }
    }
}
