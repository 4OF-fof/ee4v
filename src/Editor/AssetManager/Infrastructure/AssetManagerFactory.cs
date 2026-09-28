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
        private static string _sessionDatabasePath;
        private static IAssetManager _sessionManager;
        private static string _variantSessionDatabasePath;
        private static IAssetManager _variantSessionAssetManager;
        private static IAssetVariantManager _variantSessionManager;

        public static IAssetManager OpenSession(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path is required.", nameof(databasePath));
            }

            var path = System.IO.Path.GetFullPath(databasePath);
            if (_sessionManager == null || !string.Equals(_sessionDatabasePath, path, StringComparison.Ordinal))
            {
                _sessionManager = Open(path);
                _sessionDatabasePath = path;
            }

            return _sessionManager;
        }

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

        public static IAssetVariantManager OpenVariantSession(string databasePath, IAssetManager manager)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path is required.", nameof(databasePath));
            }
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }
            var path = System.IO.Path.GetFullPath(databasePath);
            if (_variantSessionManager == null ||
                !string.Equals(_variantSessionDatabasePath, path, StringComparison.Ordinal) ||
                !ReferenceEquals(_variantSessionAssetManager, manager))
            {
                _variantSessionManager = OpenVariants(path, manager);
                _variantSessionDatabasePath = path;
                _variantSessionAssetManager = manager;
            }
            return _variantSessionManager;
        }
    }
}
