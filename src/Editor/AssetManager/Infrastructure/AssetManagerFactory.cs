using System;
using System.IO;
using Ee4v.AssetLibrary;
using Ee4v.Core.Settings;
using UnityEditor;
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
        private static AssetDatasourceKind _sessionDatasource;
        private static string _variantSessionDatabasePath;
        private static IAssetManager _variantSessionAssetManager;
        private static IAssetVariantManager _variantSessionManager;
        private static string _expressionDatabasePath;
        private static IExpressionLibrary _expressionLibrary;

        [InitializeOnLoadMethod]
        private static void RegisterExpressionLibrary()
        {
            ExpressionLibrary.Register(() => OpenExpressionSession(
                Path.Combine(GlobalDataSettings.RootDirectory, "asset-manager-v1.db")));
            GlobalDataSettings.PathChanged += () =>
            {
                _expressionDatabasePath = null;
                _expressionLibrary = null;
            };
        }

        public static IExpressionLibrary OpenExpressions(string databasePath)
        {
            return new Ee4vExpressionLibrary(databasePath, new SqliteAssetManagerStore(databasePath), Open(databasePath));
        }

        public static IExpressionLibrary OpenExpressionSession(string databasePath)
        {
            var path = Path.GetFullPath(databasePath);
            if (_expressionLibrary == null || !string.Equals(path, _expressionDatabasePath, StringComparison.OrdinalIgnoreCase))
            {
                _expressionLibrary = OpenExpressions(path);
                _expressionDatabasePath = path;
            }
            return _expressionLibrary;
        }

        public static IAssetManager OpenSession(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path is required.", nameof(databasePath));
            }
            var path = System.IO.Path.GetFullPath(databasePath);
            return OpenSession(path, _sessionManager != null &&
                string.Equals(_sessionDatabasePath, path, StringComparison.Ordinal)
                    ? _sessionDatasource : AssetDatasourceKind.Eagle);
        }

        public static IAssetManager OpenSession(string databasePath,
            AssetDatasourceKind datasource)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path is required.", nameof(databasePath));
            }

            var path = System.IO.Path.GetFullPath(databasePath);
            if (_sessionManager == null || _sessionDatasource != datasource ||
                !string.Equals(_sessionDatabasePath, path, StringComparison.Ordinal))
            {
                _sessionManager = Open(path, datasource);
                _sessionDatasource = datasource;
                _sessionDatabasePath = path;
            }

            return _sessionManager;
        }

        public static IAssetManager Open(string databasePath)
        {
            return Open(databasePath, AssetDatasourceKind.Eagle, false);
        }

        public static IAssetManager Open(string databasePath,
            AssetDatasourceKind datasource)
        {
            return Open(databasePath, datasource, true);
        }

        private static IAssetManager Open(string databasePath,
            AssetDatasourceKind datasource, bool scopeCatalog)
        {
            if (!Enum.IsDefined(typeof(AssetDatasourceKind), datasource))
            {
                throw new ArgumentOutOfRangeException(nameof(datasource));
            }
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException(
                    "Database path is required.",
                    nameof(databasePath));
            }

            return new AssetManagerService(
                new SqliteAssetManagerStore(databasePath, scopeCatalog
                    ? (AssetSourceType?)(datasource == AssetDatasourceKind.Eagle
                        ? AssetSourceType.Eagle
                        : datasource == AssetDatasourceKind.Ee4v
                            ? AssetSourceType.Ee4v : AssetSourceType.BoothLibraryManager)
                    : null),
                new EagleAssetSource(),
                new Ee4vAssetSource(),
                new AssetTargetImporter(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(databasePath))),
                new AssetFileAnalyzer(databasePath),
                new AssetThumbnailProvider(databasePath),
                new ExternalAssetSource(), scopeCatalog ? datasource : (AssetDatasourceKind?)null);
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
