using System;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;

namespace Ee4v.AssetManager.UI
{
    internal enum AssetManagerUiErrorKind
    {
        Unknown,
        NotFound,
        Duplicate,
        InvalidRequest,
        CollectionCycle,
        InvalidCollectionHierarchy,
        InvalidSmartCollectionCondition,
        DatabaseSchemaIncompatible,
        Database,
        Datasource
    }

    internal static class AssetManagerUiErrorMessage
    {
        public static string Format(Exception exception)
        {
            if (exception == null)
            {
                return string.Empty;
            }

            switch (ResolveKind(exception))
            {
                case AssetManagerUiErrorKind.NotFound:
                    return I18N.Get(
                        "assetManager.error.notFound");
                case AssetManagerUiErrorKind.Duplicate:
                    return I18N.Get(
                        "assetManager.error.duplicate");
                case AssetManagerUiErrorKind.InvalidRequest:
                    return I18N.Get(
                        "assetManager.error.invalidRequest");
                case AssetManagerUiErrorKind.CollectionCycle:
                    return I18N.Get(
                        "assetManager.error.collectionCycle");
                case AssetManagerUiErrorKind
                    .InvalidCollectionHierarchy:
                    return I18N.Get(
                        "assetManager.navigation.collections.error.smartCollectionCannotContainCollections");
                case AssetManagerUiErrorKind
                    .InvalidSmartCollectionCondition:
                    return I18N.Get(
                        "assetManager.error.invalidSmartCollectionCondition");
                case AssetManagerUiErrorKind
                    .DatabaseSchemaIncompatible:
                    return I18N.Get(
                        "assetManager.error.databaseSchemaIncompatible");
                case AssetManagerUiErrorKind.Database:
                    return I18N.Get(
                        "assetManager.error.database");
                case AssetManagerUiErrorKind.Datasource:
                    return I18N.Get(
                        "assetManager.error.datasource");
                default:
                    return I18N.Get(
                        "assetManager.error.unknown");
            }
        }

        internal static AssetManagerUiErrorKind ResolveKind(
            Exception exception)
        {
            var assetManagerException =
                exception as AssetManagerException;
            if (assetManagerException == null)
            {
                return AssetManagerUiErrorKind.Unknown;
            }

            switch (assetManagerException.Code)
            {
                case AssetManagerErrorCode.NotFound:
                    return AssetManagerUiErrorKind.NotFound;
                case AssetManagerErrorCode.Duplicate:
                    return AssetManagerUiErrorKind.Duplicate;
                case AssetManagerErrorCode.InvalidRequest:
                    return AssetManagerUiErrorKind.InvalidRequest;
                case AssetManagerErrorCode.CollectionCycle:
                    return AssetManagerUiErrorKind.CollectionCycle;
                case AssetManagerErrorCode
                    .InvalidCollectionHierarchy:
                    return AssetManagerUiErrorKind
                        .InvalidCollectionHierarchy;
                case AssetManagerErrorCode
                    .InvalidSmartCollectionCondition:
                    return AssetManagerUiErrorKind
                        .InvalidSmartCollectionCondition;
                case AssetManagerErrorCode
                    .DatabaseSchemaIncompatible:
                    return AssetManagerUiErrorKind
                        .DatabaseSchemaIncompatible;
                case AssetManagerErrorCode.DatabaseError:
                    return AssetManagerUiErrorKind.Database;
                case AssetManagerErrorCode.DatasourceError:
                    return AssetManagerUiErrorKind.Datasource;
                default:
                    return AssetManagerUiErrorKind.Unknown;
            }
        }
    }
}
