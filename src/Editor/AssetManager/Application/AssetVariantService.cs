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
        private IReadOnlyDictionary<(string VariantId, string RevisionId), AssetVariantSnapshot> _snapshots =
            new Dictionary<(string, string), AssetVariantSnapshot>();
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
            ReplaceIndex();
            NotifyChanged();
        }

        public string GetCurrentRevisionId(string variantId)
        {
            AssetManagerRequestValidator.Require(variantId, "variant id");
            return _workspace.GetBaseRevision(variantId);
        }

        public bool HasChanges(string rootAssetPath)
        {
            AssetManagerRequestValidator.Require(rootAssetPath, "root asset path");
            var variant = _index.GetVariants().FirstOrDefault(candidate =>
                candidate.RootAssetPath == rootAssetPath);
            if (variant == null || string.IsNullOrEmpty(variant.HeadRevisionId))
            {
                return true;
            }
            var latest = ReadSnapshot(variant.Id, variant.HeadRevisionId);
            return _workspace.Inspect(rootAssetPath).ContentHash != latest.ContentHash;
        }

        public AssetVariantRevisionDetails GetRevisionDetails(string variantId, string revisionId)
        {
            AssetManagerRequestValidator.Require(variantId, "variant id");
            AssetManagerRequestValidator.Require(revisionId, "revision id");
            var snapshot = ReadSnapshot(variantId, revisionId);
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

        public async Task UpdateMetadata(string variantId, UpdateAssetVariantRequest request)
        {
            if (_busy)
            {
                throw new InvalidOperationException("A Variant operation is in progress.");
            }
            AssetManagerRequestValidator.Require(variantId, "variant id");
            AssetManagerRequestValidator.RequireRequest(request, "variant update request");
            AssetManagerRequestValidator.Require(request.Name, "variant name");
            var name = request.Name.Trim();
            var description = request.Description ?? string.Empty;
            var variant = GetVariants().FirstOrDefault(candidate => candidate.Id == variantId);
            _busy = true;
            try
            {
                if (variant == null)
                {
                    if (!_workspace.UpdateMetadata(variantId, name, description))
                    {
                        throw new InvalidOperationException("The Variant was not found.");
                    }
                }
                else
                {
                    if (variant.Name == name && (variant.Description ?? string.Empty) == description)
                    {
                        return;
                    }
                    variant.Name = name;
                    variant.Description = description;
                    variant.UpdatedAt = DateTime.UtcNow;
                    await Task.Run(() => _repository.UpdateMetadata(variant));
                    try { _workspace.UpdateMetadata(variantId, name, description); }
                    finally
                    {
                        ReplaceIndex();
                        NotifyChanged();
                    }
                    return;
                }
                NotifyChanged();
            }
            finally { _busy = false; }
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
                var current = GetVariants().FirstOrDefault(variant => variant.RootAssetPath == request.RootAssetPath);
                if (current != null)
                {
                    _workspace.UpdateMetadata(current.Id, current.Name, current.Description);
                }
                capture = _workspace.Capture(request);
                var saved = await Task.Run(() =>
                    _repository.Save(capture.Snapshot, capture.StagingPath));
                _workspace.SetBaseRevision(saved.Variant.Id, saved.Revision.Id);
                ReplaceIndex();
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
                var snapshot = ReadSnapshot(variantId, revisionId);
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
                var plans = AssetFileImportPlanner.Resolve(imports.SelectMany(import =>
                    AssetManagerRequestValidator.NormalizeTargetPaths(import.Dependency.TargetPaths)
                        .Select(path => new AssetFileTarget { FileId = import.File.Id, TargetPath = path })),
                    _manager.GetFileDependencies).Where(plan => plan.TargetPaths.Count > 0).ToArray();
                foreach (var plan in plans)
                {
                    var file = _manager.GetFile(plan.FileId);
                    if (file.IsArchived || string.IsNullOrWhiteSpace(file.ItemId))
                    {
                        throw new InvalidOperationException(
                            "A Variant dependency is archived or unassigned: " + file.FileName);
                    }
                    _manager.GetItem(file.ItemId);
                    plan.TargetPaths = AssetManagerRequestValidator.NormalizeTargetPaths(plan.TargetPaths);
                    AssetManagerRequestValidator.EnsureImportTargetsDoNotContainZip(file, plan.TargetPaths);
                }
                var savedImports = imports.ToLookup(import => import.File.Id, StringComparer.Ordinal);
                staging = await Task.Run(() => _repository.Extract(snapshot));
                foreach (var plan in plans)
                {
                    var saved = savedImports[plan.FileId].ToArray();
                    if (saved.Length > 0 && saved.All(import => _workspace.HasAssets(import.Dependency)) &&
                        plan.TargetPaths.All(path => saved.Any(import =>
                            import.Dependency.TargetPaths.Contains(path, StringComparer.OrdinalIgnoreCase))))
                    {
                        continue;
                    }
                    var result = await _manager.ImportFileEntries(
                        plan.FileId, plan.TargetPaths);
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            result.ErrorMessage ?? "A Variant dependency could not be imported.");
                    }
                }
                foreach (var import in imports)
                {
                    if (!_workspace.HasAssets(import.Dependency))
                    {
                        throw new InvalidOperationException(
                            "The imported dependency does not contain the saved Asset GUIDs: " +
                            import.Dependency.SourceId);
                    }
                }
                _workspace.ValidateRestore(snapshot);
                var path = _workspace.Restore(snapshot, staging);
                var current = GetVariants().FirstOrDefault(variant => variant.Id == variantId);
                if (current != null)
                {
                    _workspace.UpdateMetadata(variantId, current.Name, current.Description);
                }
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

        private void ReplaceIndex()
        {
            var snapshots = _repository.ReadAll();
            var lookup = snapshots.ToDictionary(snapshot => (snapshot.Variant.Id, snapshot.Revision.Id));
            _index.ReplaceVariantIndex(snapshots);
            _snapshots = lookup;
        }

        private AssetVariantSnapshot ReadSnapshot(string variantId, string revisionId) =>
            _snapshots.TryGetValue((variantId, revisionId), out var snapshot)
                ? snapshot
                : _repository.Read(variantId, revisionId);

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
