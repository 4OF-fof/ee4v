using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application
{
    internal sealed class AssetVariantService : IAssetVariantManager
    {
        private readonly IAssetManager _manager;
        private readonly IAssetVariantRepository _repository;
        private readonly IAssetVariantIndex _index;
        private readonly IAssetVariantWorkspace _workspace;
        private bool _busy;

        internal AssetVariantService(IAssetManager manager,
            IAssetVariantRepository repository, IAssetVariantIndex index,
            IAssetVariantWorkspace workspace)
        {
            _manager = manager;
            _repository = repository;
            _index = index;
            _workspace = workspace;
        }

        public event Action Changed;

        public IReadOnlyList<AssetVariant> GetVariants() => _index.GetVariants();

        public IReadOnlyList<AssetVariantRevision> GetRevisions(string variantId)
        {
            AssetManagerRequestValidator.Require(variantId, "variant id");
            return _index.GetVariantRevisions(variantId);
        }

        public void RebuildIndex()
        {
            if (_busy)
            {
                throw new InvalidOperationException("A Variant operation is in progress.");
            }
            _index.ReplaceVariantIndex(_repository.ReadAll());
            NotifyChanged();
        }

        public AssetVariantRevisionDetails GetRevisionDetails(string variantId, string revisionId)
        {
            AssetManagerRequestValidator.Require(variantId, "variant id");
            AssetManagerRequestValidator.Require(revisionId, "revision id");
            var snapshot = _repository.Read(variantId, revisionId);
            return new AssetVariantRevisionDetails
            {
                UnityVersion = snapshot.UnityVersion,
                Dependencies = snapshot.Dependencies
                    .Select(dependency => new AssetVariantFileDependency
                    {
                        FileId = dependency.FileId,
                        SourceType = dependency.SourceType,
                        SourceId = dependency.SourceId,
                        TargetPaths = dependency.TargetPaths.ToArray()
                    }).ToArray()
            };
        }

        public async Task<AssetVariantRevision> Save(AssetVariantSaveRequest request)
        {
            if (_busy)
            {
                throw new InvalidOperationException("A Variant operation is in progress.");
            }
            AssetManagerRequestValidator.RequireRequest(request, "variant save request");
            AssetManagerRequestValidator.Require(request.RootAssetPath, "root asset path");
            _busy = true;
            AssetVariantCapture capture = null;
            try
            {
                capture = _workspace.Capture(request);
                var saved = await Task.Run(() =>
                    _repository.Save(capture.Snapshot, capture.StagingPath));
                _workspace.SetBaseRevision(saved.Variant.Id, saved.Revision.Id);
                _index.ReplaceVariantIndex(_repository.ReadAll());
                NotifyChanged();
                return saved.Revision;
            }
            finally
            {
                if (capture != null)
                {
                    _repository.DeleteStaging(capture.StagingPath);
                }
                _busy = false;
            }
        }

        public Task<AssetThumbnail> GetRevisionThumbnail(string variantId, string revisionId,
            CancellationToken cancellationToken = default)
        {
            AssetManagerRequestValidator.Require(variantId, "variant id");
            AssetManagerRequestValidator.Require(revisionId, "revision id");
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = _repository.Read(variantId, revisionId);
                var bytes = _repository.ReadPreview(snapshot);
                cancellationToken.ThrowIfCancellationRequested();
                return new AssetThumbnail { Found = true, Data = bytes };
            }, cancellationToken);
        }

        public async Task<string> Restore(string variantId, string revisionId)
        {
            if (_busy)
            {
                throw new InvalidOperationException("A Variant operation is in progress.");
            }
            _busy = true;
            string staging = null;
            try
            {
                var snapshot = _repository.Read(variantId, revisionId);
                _workspace.ValidateRestore(snapshot);
                var imports = snapshot.Dependencies.Select(dependency => new
                {
                    Dependency = dependency,
                    File = ResolveDependency(dependency)
                }).ToArray();
                staging = await Task.Run(() => _repository.Extract(snapshot));
                foreach (var import in imports)
                {
                    if (_workspace.HasAssets(import.Dependency))
                    {
                        continue;
                    }
                    var result = await _manager.ImportFileEntries(
                        import.File.Id, import.Dependency.TargetPaths);
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            result.ErrorMessage ?? "A Variant dependency could not be imported.");
                    }
                    if (!_workspace.HasAssets(import.Dependency))
                    {
                        throw new InvalidOperationException(
                            "The imported dependency does not contain the saved Asset GUIDs: " +
                            import.Dependency.SourceId);
                    }
                }
                _workspace.ValidateRestore(snapshot);
                var path = _workspace.Restore(snapshot, staging);
                _workspace.SetBaseRevision(variantId, revisionId);
                NotifyChanged();
                return path;
            }
            finally
            {
                if (staging != null)
                {
                    _repository.DeleteStaging(staging);
                }
                _busy = false;
            }
        }

        private AssetFile ResolveDependency(AssetVariantDependency dependency)
        {
            var files = _manager.SearchItems(new AssetItemQuery { IncludeArchived = true })
                .Items.SelectMany(item => _manager.GetFiles(item.Id, true))
                .Concat(_manager.GetUnassignedFiles(true));
            var file = files.SingleOrDefault(candidate =>
                candidate.SourceType == dependency.SourceType &&
                candidate.SourceId == dependency.SourceId);
            if (file == null || file.IsArchived || string.IsNullOrEmpty(file.ItemId))
            {
                throw new InvalidOperationException(
                    "A Variant dependency is missing, archived, or unassigned: " +
                    dependency.SourceId);
            }
            return file;
        }

        private void NotifyChanged()
        {
            var handlers = Changed;
            if (handlers == null)
            {
                return;
            }
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch { /* A subscriber cannot undo an already published Git revision. */ }
            }
        }
    }
}
