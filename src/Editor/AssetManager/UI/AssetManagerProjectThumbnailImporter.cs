using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.ProjectStyle;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerProjectThumbnailImporter : IDisposable
    {
        private const string GeneratedAssetRoot =
            "Assets/ee4v/Generated/AssetManager/Thumbnails";

        private readonly IAssetManager _manager;
        private readonly HashSet<string> _pendingImportedItemIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _pendingRefreshItemIds =
            new HashSet<string>(StringComparer.Ordinal);
        private CancellationTokenSource _applyCancellation;
        private bool _applyQueued;
        private bool _applyRunning;
        private bool _disposed;

        internal AssetManagerProjectThumbnailImporter(
            IAssetManager manager)
        {
            _manager = manager ??
                throw new ArgumentNullException(nameof(manager));
            _manager.Changed += OnAssetManagerChanged;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _manager.Changed -= OnAssetManagerChanged;
            _applyCancellation?.Cancel();
            _applyCancellation = null;
            _pendingImportedItemIds.Clear();
            _pendingRefreshItemIds.Clear();
        }

        private void OnAssetManagerChanged(AssetManagerChange change)
        {
            if (_disposed || change == null)
            {
                return;
            }

            IReadOnlyList<string> itemIds;
            HashSet<string> pending;
            if (change.Kind ==
                AssetManagerChangeKind.FileImportedAssetGuidsChanged)
            {
                itemIds = change.RelatedIds;
                pending = _pendingImportedItemIds;
            }
            else if (change.Kind ==
                     AssetManagerChangeKind.SourceSynchronized)
            {
                itemIds = change.SubjectIds;
                pending = _pendingRefreshItemIds;
            }
            else
            {
                return;
            }

            for (var i = 0; i < itemIds.Count; i++)
            {
                pending.Add(itemIds[i]);
            }

            QueueApply();
        }

        private void QueueApply()
        {
            if (_disposed ||
                _applyQueued ||
                _applyRunning ||
                _pendingImportedItemIds.Count == 0 &&
                _pendingRefreshItemIds.Count == 0)
            {
                return;
            }

            _applyQueued = true;
            EditorApplication.delayCall += ApplyPending;
        }

        private void ApplyPending()
        {
            _ = ApplyPendingAsync();
        }

        private async Task ApplyPendingAsync()
        {
            _applyQueued = false;
            if (_disposed || _applyRunning)
            {
                return;
            }

            var importedItemIds = new HashSet<string>(
                _pendingImportedItemIds,
                StringComparer.Ordinal);
            var refreshItemIds = _pendingRefreshItemIds
                .Where(HasGeneratedThumbnailAsset)
                .ToArray();
            _pendingImportedItemIds.Clear();
            _pendingRefreshItemIds.Clear();
            _applyRunning = true;
            var cancellation = new CancellationTokenSource();
            _applyCancellation = cancellation;
            try
            {
                var foldersByItemId =
                    new Dictionary<string, List<string>>(
                        StringComparer.Ordinal);
                var itemIdsWithEligibleFolders = new List<string>();
                var eligibleItemIds = new HashSet<string>(
                    StringComparer.Ordinal);
                if (importedItemIds.Count > 0)
                {
                    var associations =
                        _manager.GetImportedAssetAssociations();
                    foreach (var pair in
                             SelectTopmostImportedFolders(associations))
                    {
                        if (!importedItemIds.Contains(pair.Value))
                        {
                            continue;
                        }

                        if (!foldersByItemId.TryGetValue(
                                pair.Value,
                                out var itemFolders))
                        {
                            itemFolders = new List<string>();
                            foldersByItemId.Add(pair.Value, itemFolders);
                        }

                        itemFolders.Add(pair.Key);
                        if (CanApplyGeneratedThumbnail(pair.Key) &&
                            eligibleItemIds.Add(pair.Value))
                        {
                            itemIdsWithEligibleFolders.Add(pair.Value);
                        }
                    }
                }

                var requestedItemIds = itemIdsWithEligibleFolders
                    .Concat(refreshItemIds)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (requestedItemIds.Length == 0)
                {
                    return;
                }

                var thumbnails = await _manager.GetThumbnails(
                    requestedItemIds,
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                for (var i = 0; i < requestedItemIds.Length; i++)
                {
                    var itemId = requestedItemIds[i];
                    if (!thumbnails.TryGetValue(
                            itemId,
                            out var thumbnail) ||
                        thumbnail == null ||
                        !thumbnail.Found ||
                        thumbnail.Data == null ||
                        thumbnail.Data.Length == 0)
                    {
                        continue;
                    }

                    var folderGuids = foldersByItemId.TryGetValue(
                            itemId,
                            out var itemFolderGuids)
                        ? itemFolderGuids
                            .Where(CanApplyGeneratedThumbnail)
                            .ToArray()
                        : Array.Empty<string>();
                    var iconGuid = GetOrCreateThumbnailAsset(
                        itemId,
                        thumbnail.Data);
                    if (folderGuids.Length > 0 &&
                        !string.IsNullOrEmpty(iconGuid))
                    {
                        ProjectStyleApi.SetIcon(folderGuids, iconGuid);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (!_disposed &&
                    ReferenceEquals(_applyCancellation, cancellation))
                {
                    Debug.LogException(exception);
                }
            }
            finally
            {
                if (ReferenceEquals(_applyCancellation, cancellation))
                {
                    _applyCancellation = null;
                }

                cancellation.Dispose();
                _applyRunning = false;
                QueueApply();
            }
        }

        private static string GetOrCreateThumbnailAsset(
            string itemId,
            byte[] data)
        {
            EnsureGeneratedAssetRoot();
            var assetPath = GetThumbnailAssetPath(itemId);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                assetPath);

            if (texture == null)
            {
                texture = new Texture2D(2, 2)
                {
                    name = "AssetManager Thumbnail"
                };
            }

            if (!texture.LoadImage(data))
            {
                if (!AssetDatabase.Contains(texture))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                return string.Empty;
            }

            if (AssetDatabase.Contains(texture))
            {
                EditorUtility.SetDirty(texture);
            }
            else
            {
                AssetDatabase.CreateAsset(texture, assetPath);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.AssetPathToGUID(assetPath);
        }

        private static bool CanApplyGeneratedThumbnail(string folderGuid)
        {
            var style = ProjectStyleApi.Get(folderGuid);
            if (!style.HasIcon)
            {
                return true;
            }

            var path = AssetDatabase.GUIDToAssetPath(style.IconGuid);
            return IsUnder(path, GeneratedAssetRoot);
        }

        private static bool HasGeneratedThumbnailAsset(string itemId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(
                GetThumbnailAssetPath(itemId)) != null;
        }

        private static string GetThumbnailAssetPath(string itemId)
        {
            return GeneratedAssetRoot + "/" + Hash128.Compute(itemId) + ".asset";
        }

        private static bool IsUnder(string path, string root)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(root + "/", StringComparison.Ordinal);
        }

        private static void EnsureGeneratedAssetRoot()
        {
            var current = "Assets";
            var segments = GeneratedAssetRoot.Split('/');
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        internal static IReadOnlyDictionary<string, string>
            SelectTopmostImportedFolders(
                IReadOnlyList<AssetImportedAssetAssociation> associations)
        {
            return SelectTopmostImportedFolders(
                associations,
                association => association.ItemId);
        }

        private static IReadOnlyDictionary<string, string>
            SelectTopmostImportedFolders(
                IReadOnlyList<AssetImportedAssetAssociation> associations,
                Func<AssetImportedAssetAssociation, string> groupKey)
        {
            var latestByGuid = new Dictionary<
                string,
                AssetImportedAssetAssociation>(StringComparer.Ordinal);
            foreach (var association in associations ??
                     Array.Empty<AssetImportedAssetAssociation>())
            {
                if (association == null ||
                    string.IsNullOrWhiteSpace(association.AssetGuid) ||
                    string.IsNullOrWhiteSpace(association.ItemId) ||
                    string.IsNullOrWhiteSpace(association.FileId))
                {
                    continue;
                }

                if (!latestByGuid.TryGetValue(
                        association.AssetGuid,
                        out var current) ||
                    current.ImportedAt <= association.ImportedAt)
                {
                    latestByGuid[association.AssetGuid] = association;
                }
            }

            var candidates = latestByGuid.Values
                .GroupBy(
                    groupKey,
                    StringComparer.Ordinal)
                .SelectMany(group => SelectImportedRootCandidates(
                    group,
                    latestByGuid))
                .Where(candidate =>
                    !string.IsNullOrWhiteSpace(candidate.Path) &&
                    AssetDatabase.IsValidFolder(candidate.Path))
                .OrderBy(candidate => GetDepth(candidate.Path))
                .ThenBy(
                    candidate => candidate.Path,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    candidate => candidate.Guid,
                    StringComparer.Ordinal)
                .ToArray();
            var selectedPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var selected = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                var ancestor = candidate.Path;
                var isCovered = false;
                while (!string.IsNullOrEmpty(ancestor))
                {
                    if (selectedPaths.Contains(ancestor))
                    {
                        isCovered = true;
                        break;
                    }

                    ancestor = ParentPath(ancestor);
                }

                if (isCovered)
                {
                    continue;
                }

                selectedPaths.Add(candidate.Path);
                selected[candidate.Guid] = candidate.ItemId;
            }

            return selected;
        }

        private static IEnumerable<FolderCandidate>
            SelectImportedRootCandidates(
                IGrouping<string, AssetImportedAssetAssociation> associations,
                IReadOnlyDictionary<
                    string,
                    AssetImportedAssetAssociation> latestByGuid)
        {
            var importedAssets = associations
                .Select(association => new
                {
                    Association = association,
                    Path = AssetDatabase.GUIDToAssetPath(
                        association.AssetGuid)
                })
                .Where(asset =>
                    !string.IsNullOrWhiteSpace(asset.Path))
                .ToArray();
            var contentPaths = importedAssets
                .Where(asset =>
                    !AssetDatabase.IsValidFolder(asset.Path))
                .Select(asset => asset.Path)
                .ToArray();
            var contentRoot = FindCommonParentFolder(contentPaths);
            if (!string.IsNullOrWhiteSpace(contentRoot) &&
                !string.Equals(
                    contentRoot,
                    "Assets",
                    StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.IsValidFolder(contentRoot))
            {
                var rootGuid = AssetDatabase.AssetPathToGUID(contentRoot);
                var itemId = latestByGuid.TryGetValue(
                    rootGuid,
                    out var rootAssociation)
                        ? rootAssociation.ItemId
                        : associations
                            .OrderByDescending(
                                association => association.ImportedAt)
                            .First()
                            .ItemId;
                yield return new FolderCandidate(
                    rootGuid,
                    contentRoot,
                    itemId);
                yield break;
            }

            foreach (var asset in importedAssets.Where(asset =>
                         AssetDatabase.IsValidFolder(asset.Path)))
            {
                yield return new FolderCandidate(
                    asset.Association.AssetGuid,
                    asset.Path,
                    asset.Association.ItemId);
            }
        }

        private static string FindCommonParentFolder(
            IReadOnlyList<string> assetPaths)
        {
            if (assetPaths == null || assetPaths.Count == 0)
            {
                return string.Empty;
            }

            var parent = ParentPath(assetPaths[0]);
            while (!string.IsNullOrEmpty(parent) &&
                   assetPaths.Any(path =>
                       !IsSameOrDescendant(parent, path)))
            {
                parent = ParentPath(parent);
            }

            return parent;
        }

        private static string ParentPath(string path)
        {
            var separator = path.LastIndexOf('/');
            return separator <= 0
                ? string.Empty
                : path.Substring(0, separator);
        }

        private static bool IsSameOrDescendant(
            string parentPath,
            string candidatePath)
        {
            return string.Equals(
                       parentPath,
                       candidatePath,
                       StringComparison.OrdinalIgnoreCase) ||
                   candidatePath.Length > parentPath.Length &&
                   candidatePath.StartsWith(
                       parentPath,
                       StringComparison.OrdinalIgnoreCase) &&
                   candidatePath[parentPath.Length] == '/';
        }

        private static int GetDepth(string path)
        {
            return path.Count(character => character == '/');
        }

        private sealed class FolderCandidate
        {
            internal FolderCandidate(
                string guid,
                string path,
                string itemId)
            {
                Guid = guid;
                Path = (path ?? string.Empty)
                    .Replace('\\', '/')
                    .TrimEnd('/');
                ItemId = itemId;
            }

            internal string Guid { get; }
            internal string Path { get; }
            internal string ItemId { get; }
        }
    }
}
