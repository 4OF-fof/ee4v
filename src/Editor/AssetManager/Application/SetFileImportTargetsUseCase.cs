using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Domain;

namespace Ee4v.AssetManager.Application
{
    internal sealed class SetFileImportTargetsUseCase
    {
        private readonly IAssetImportTargetReadStore _reader;
        private readonly IAssetImportTargetCommandStore _writer;
        private readonly Action<AssetManagerChange> _publish;

        internal SetFileImportTargetsUseCase(
            IAssetImportTargetReadStore reader,
            IAssetImportTargetCommandStore writer,
            Action<AssetManagerChange> publish)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        }

        internal void Execute(
            string fileId,
            IReadOnlyList<AssetFileImportTargetRequest> targets)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            var requestedPaths = targets == null
                ? Array.Empty<string>()
                : targets
                    .Where(target => target != null)
                    .Select(target => target.RelativePath)
                    .ToArray();

            IReadOnlyList<string> normalizedPaths;
            try
            {
                normalizedPaths = ImportTargetPathPolicy.Normalize(requestedPaths);
            }
            catch (ImportTargetPathRuleException exception)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    GetValidationMessage(exception.Error),
                    exception);
            }

            _writer.ReplaceFileImportTargets(fileId, normalizedPaths);
            var updatedTargets = _reader.GetFileImportTargets(fileId);
            _publish(new AssetManagerChange(
                AssetManagerChangeKind.FileImportTargets,
                fileId,
                importTargets: updatedTargets));
            _publish(new AssetManagerChange(AssetManagerChangeKind.FileTree));
        }

        private static string GetValidationMessage(ImportTargetPathError error)
        {
            return error == ImportTargetPathError.Empty
                ? "Import target must identify a child entry of the file."
                : "Import target path must be relative to the file.";
        }
    }
}
