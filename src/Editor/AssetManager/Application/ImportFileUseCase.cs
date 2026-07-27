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
            string assetName,
            string assetFileName,
            string sourcePath,
            IReadOnlyList<string> relativePaths)
        {
            AssetName = assetName ?? string.Empty;
            AssetFileName = assetFileName ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            RelativePaths = relativePaths ?? Array.Empty<string>();
        }

        internal string AssetName { get; }
        internal string AssetFileName { get; }
        internal string SourcePath { get; }
        internal IReadOnlyList<string> RelativePaths { get; }
    }

    internal sealed class ImportFileUseCase
    {
        private readonly IAssetCatalogReadStore _catalog;
        private readonly IAssetFileReadStore _files;
        private readonly IAssetImportTargetReadStore _importTargets;
        private readonly IAssetImportGateway _gateway;

        internal ImportFileUseCase(
            IAssetCatalogReadStore catalog,
            IAssetFileReadStore files,
            IAssetImportTargetReadStore importTargets,
            IAssetImportGateway gateway)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _importTargets = importTargets ?? throw new ArgumentNullException(nameof(importTargets));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
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

            IReadOnlyList<string> normalizedPaths;
            try
            {
                normalizedPaths = ImportTargetPathPolicy.Normalize(relativePaths);
            }
            catch (ImportTargetPathRuleException exception)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    exception.Error == ImportTargetPathError.Empty
                        ? "At least one import entry is required."
                        : "Import entry paths must be relative.",
                    exception);
            }

            var item = _catalog.GetItem(itemId);
            if (item == null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.NotFound,
                    "The item was not found.");
            }

            var file = _files.GetFiles(
                    itemId,
                    new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active })
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, fileId, StringComparison.Ordinal));
            if (file == null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.NotFound,
                    "The file was not found in the item.");
            }

            var resolution = _files.ResolveFilePath(fileId);
            if (resolution == null ||
                !resolution.Found ||
                string.IsNullOrWhiteSpace(resolution.Path))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.NotFound,
                    "The file path could not be resolved.");
            }

            _gateway.Import(new AssetImportPlan(
                item.Name,
                file.FileName,
                resolution.Path,
                normalizedPaths));
        }
    }
}
