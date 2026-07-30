using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Domain;

namespace Ee4v.AssetManager.Application
{
    internal sealed class AssetImportPlan
    {
        internal AssetImportPlan(
            string itemId,
            string fileId,
            string assetName,
            string assetFileName,
            string sourcePath,
            IReadOnlyList<string> relativePaths)
        {
            ItemId = itemId ?? string.Empty;
            FileId = fileId ?? string.Empty;
            AssetName = assetName ?? string.Empty;
            AssetFileName = assetFileName ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            RelativePaths = relativePaths ?? Array.Empty<string>();
        }

        internal string ItemId { get; }
        internal string FileId { get; }
        internal string AssetName { get; }
        internal string AssetFileName { get; }
        internal string SourcePath { get; }
        internal IReadOnlyList<string> RelativePaths { get; }
    }

    internal sealed class AssetImportResult
    {
        internal AssetImportResult(
            bool succeeded,
            IReadOnlyList<string> assetGuids)
        {
            Succeeded = succeeded;
            AssetGuids = assetGuids ?? Array.Empty<string>();
        }

        internal bool Succeeded { get; }
        internal IReadOnlyList<string> AssetGuids { get; }
    }

    internal sealed class ImportFileUseCase
    {
        private readonly IAssetCatalogReadStore _catalog;
        private readonly IAssetFileReadStore _files;
        private readonly IAssetDependencyReadStore
            _dependencyReader;
        private readonly IAssetImportTargetReadStore _importTargets;
        private readonly IImportedAssetGuidCommandStore _importedAssetGuids;
        private readonly IAssetImportGateway _gateway;
        private readonly Action<AssetManagerChange> _publish;

        internal ImportFileUseCase(
            IAssetCatalogReadStore catalog,
            IAssetFileReadStore files,
            IAssetDependencyReadStore dependencyReader,
            IAssetImportTargetReadStore importTargets,
            IImportedAssetGuidCommandStore importedAssetGuids,
            IAssetImportGateway gateway,
            Action<AssetManagerChange> publish)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _dependencyReader = dependencyReader ??
                throw new ArgumentNullException(
                    nameof(dependencyReader));
            _importTargets = importTargets ?? throw new ArgumentNullException(nameof(importTargets));
            _importedAssetGuids = importedAssetGuids ??
                throw new ArgumentNullException(nameof(importedAssetGuids));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        }

        internal void ImportConfiguredTargets(string itemId, string fileId)
        {
            var relativePaths = _importTargets.GetFileImportTargets(fileId)
                .Where(target => target != null)
                .Select(target => target.RelativePath)
                .ToArray();
            Execute(itemId, fileId, relativePaths);
        }

        internal void ImportEntry(string itemId, string fileId, string relativePath)
        {
            Execute(itemId, fileId, new[] { relativePath });
        }

        private void Execute(
            string itemId,
            string fileId,
            IReadOnlyList<string> relativePaths)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            AssetManagerRequestValidator.Require(fileId, "file id");

            var item = _catalog.GetItem(itemId);
            if (item == null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.NotFound,
                    "The item was not found.");
            }

            IReadOnlyList<string> importOrder;
            try
            {
                importOrder =
                    FileDependencyGraphPolicy.ResolveImportOrder(
                        fileId,
                        dependencyFileId =>
                            _dependencyReader
                                .GetFileDependencies(
                                    dependencyFileId)
                                .Where(dependency =>
                                    dependency != null)
                                .Select(dependency =>
                                    dependency.DependencyFileId)
                                .ToArray());
            }
            catch (CatalogRuleException exception)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.DependencyCycle,
                    "File dependency cycle is not allowed.",
                    exception);
            }

            var plans = importOrder
                .Select(orderedFileId =>
                {
                    var paths = string.Equals(
                        orderedFileId,
                        fileId,
                        StringComparison.Ordinal)
                        ? relativePaths
                        : _importTargets
                            .GetFileImportTargets(
                                orderedFileId)
                            .Where(target =>
                                target != null)
                            .Select(target =>
                                target.RelativePath)
                            .ToArray();
                    return CreatePlan(
                        orderedFileId,
                        paths,
                        string.Equals(
                            orderedFileId,
                            fileId,
                            StringComparison.Ordinal)
                            ? itemId
                            : null);
                })
                .ToArray();
            ImportNext(plans, 0);
        }

        private AssetImportPlan CreatePlan(
            string fileId,
            IReadOnlyList<string> relativePaths,
            string expectedItemId)
        {
            var file = _files.GetFile(fileId);
            var ownerItemId =
                _files.GetFileOwnerItemId(fileId);
            if (file == null ||
                file.Lifecycle != AssetFileLifecycle.Active ||
                string.IsNullOrWhiteSpace(ownerItemId) ||
                (!string.IsNullOrWhiteSpace(expectedItemId) &&
                 !string.Equals(
                     ownerItemId,
                     expectedItemId,
                     StringComparison.Ordinal)))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.NotFound,
                    "The file was not found in the item.");
            }

            var item = _catalog.GetItem(ownerItemId);
            if (item == null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.NotFound,
                    "The item was not found.");
            }

            IReadOnlyList<string> normalizedPaths;
            try
            {
                normalizedPaths =
                    ImportTargetPathPolicy.Normalize(
                        relativePaths);
            }
            catch (ImportTargetPathRuleException exception)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    exception.Error ==
                    ImportTargetPathError.Empty
                        ? "At least one import entry is required."
                        : "Import entry paths must be relative.",
                    exception);
            }

            var resolution =
                _files.ResolveFilePath(fileId);
            if (resolution == null ||
                !resolution.Found ||
                string.IsNullOrWhiteSpace(resolution.Path))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.NotFound,
                    "The file path could not be resolved.");
            }

            return new AssetImportPlan(
                ownerItemId,
                fileId,
                item.Name,
                file.FileName,
                resolution.Path,
                normalizedPaths);
        }

        private void ImportNext(
            IReadOnlyList<AssetImportPlan> plans,
            int index)
        {
            if (index >= plans.Count)
            {
                return;
            }

            var plan = plans[index];
            _gateway.Import(
                plan,
                result =>
                {
                    if (result == null ||
                        !result.Succeeded)
                    {
                        return;
                    }

                    _importedAssetGuids
                        .ReplaceFileImportedAssetGuids(
                            plan.FileId,
                            ImportedAssetGuidPolicy.Normalize(
                                result.AssetGuids));
                    _publish(new AssetManagerChange(
                        AssetManagerChangeKind
                            .ImportedAssetGuids,
                        plan.ItemId,
                        plan.FileId));
                    ImportNext(plans, index + 1);
                });
        }
    }
}
