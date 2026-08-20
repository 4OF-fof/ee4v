using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.Injector;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerProjectThumbnailPresenter : IDisposable
    {
        private static readonly Color DarkListBackground =
            new Color32(56, 56, 56, 255);
        private static readonly Color DarkGridBackground =
            new Color32(51, 51, 51, 255);
        private static readonly Color LightListBackground =
            new Color32(200, 200, 200, 255);
        private static readonly Color LightGridBackground =
            new Color32(189, 189, 189, 255);

        private readonly IAssetManager _manager;
        private readonly Dictionary<string, Texture2D> _texturesByGuid =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private CancellationTokenSource _reloadCancellation;
        private bool _reloadQueued;
        private bool _disposed;

        internal AssetManagerProjectThumbnailPresenter(
            IAssetManager manager)
        {
            _manager = manager ??
                throw new ArgumentNullException(nameof(manager));
            _manager.Changed += OnAssetManagerChanged;
            QueueReload();
        }

        internal void Draw(ItemInjectionContext context)
        {
            if (_disposed ||
                context == null ||
                string.IsNullOrWhiteSpace(context.Guid) ||
                Event.current == null ||
                Event.current.type != EventType.Repaint ||
                !_texturesByGuid.TryGetValue(
                    context.Guid,
                    out var texture) ||
                texture == null)
            {
                return;
            }

            var iconRect = ProjectItemLayout.GetIconRect(
                context.SelectionRect,
                context.ProjectViewMode,
                context.ProjectOrientation);
            EditorGUI.DrawRect(iconRect, ResolveBackgroundColor(context));
            GUI.DrawTexture(
                iconRect,
                texture,
                ScaleMode.ScaleToFit,
                true);
            context.SuppressProjectItemIconOverlay = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _manager.Changed -= OnAssetManagerChanged;
            _reloadCancellation?.Cancel();
            _reloadCancellation?.Dispose();
            _reloadCancellation = null;
            DestroyTextures(_texturesByGuid.Values);
            _texturesByGuid.Clear();
        }

        private void OnAssetManagerChanged(AssetManagerChange change)
        {
            if (change != null && AffectsProjectThumbnails(change.Kind))
            {
                QueueReload();
            }
        }

        private void QueueReload()
        {
            if (_disposed || _reloadQueued)
            {
                return;
            }

            _reloadCancellation?.Cancel();
            _reloadQueued = true;
            EditorApplication.delayCall += ReloadAsync;
        }

        private async void ReloadAsync()
        {
            _reloadQueued = false;
            if (_disposed)
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            _reloadCancellation = cancellation;
            try
            {
                var folders = SelectTopmostImportedFolders(
                    _manager.GetImportedAssetAssociations());
                var itemIds = folders.Values
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var thumbnails = itemIds.Length == 0
                    ? new Dictionary<string, AssetThumbnail>()
                    : await _manager.GetThumbnails(
                        itemIds,
                        cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();

                var texturesByItem = new Dictionary<string, Texture2D>(
                    StringComparer.Ordinal);
                foreach (var itemId in itemIds)
                {
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

                    var texture = new Texture2D(2, 2)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    if (texture.LoadImage(thumbnail.Data))
                    {
                        texturesByItem[itemId] = texture;
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }

                var nextTextures = new Dictionary<string, Texture2D>(
                    StringComparer.Ordinal);
                foreach (var folder in folders)
                {
                    if (texturesByItem.TryGetValue(
                            folder.Value,
                            out var texture))
                    {
                        nextTextures[folder.Key] = texture;
                    }
                }

                if (_disposed ||
                    !ReferenceEquals(_reloadCancellation, cancellation))
                {
                    DestroyTextures(texturesByItem.Values);
                    return;
                }

                DestroyTextures(_texturesByGuid.Values);
                _texturesByGuid.Clear();
                foreach (var pair in nextTextures)
                {
                    _texturesByGuid[pair.Key] = pair.Value;
                }

                InjectorApi.Repaint(InjectionChannel.ProjectItem);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (!_disposed &&
                    ReferenceEquals(_reloadCancellation, cancellation))
                {
                    Debug.LogException(exception);
                }
            }
            finally
            {
                if (ReferenceEquals(_reloadCancellation, cancellation))
                {
                    _reloadCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private static IReadOnlyDictionary<string, string>
            SelectTopmostImportedFolders(
                IReadOnlyList<AssetImportedAssetAssociation> associations)
        {
            var latestByGuid = new Dictionary<
                string,
                AssetImportedAssetAssociation>(StringComparer.Ordinal);
            foreach (var association in associations ??
                     Array.Empty<AssetImportedAssetAssociation>())
            {
                if (association == null ||
                    string.IsNullOrWhiteSpace(association.AssetGuid) ||
                    string.IsNullOrWhiteSpace(association.ItemId))
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
                .Select(association => new FolderCandidate(
                    association.AssetGuid,
                    AssetDatabase.GUIDToAssetPath(association.AssetGuid),
                    association.ItemId))
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
            var selectedPaths = new List<string>();
            var selected = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (selectedPaths.Any(path =>
                        IsSameOrDescendant(path, candidate.Path)))
                {
                    continue;
                }

                selectedPaths.Add(candidate.Path);
                selected[candidate.Guid] = candidate.ItemId;
            }

            return selected;
        }

        private static bool AffectsProjectThumbnails(
            AssetManagerChangeKind kind)
        {
            return kind ==
                       AssetManagerChangeKind.FileImportedAssetGuidsChanged ||
                   kind == AssetManagerChangeKind.SourceSynchronized ||
                   kind == AssetManagerChangeKind.ItemUpdated ||
                   kind == AssetManagerChangeKind.ItemDeleted ||
                   kind == AssetManagerChangeKind.FilePlacementChanged ||
                   kind == AssetManagerChangeKind.FileDeleted;
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

        private static void DestroyTextures(
            IEnumerable<Texture2D> textures)
        {
            foreach (var texture in textures
                         .Where(texture => texture != null)
                         .Distinct())
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Color ResolveBackgroundColor(
            ItemInjectionContext context)
        {
            var isList =
                context.ProjectViewMode == ProjectItemViewMode.OneColumn ||
                context.ProjectOrientation ==
                ProjectItemOrientation.Horizontal;
            if (EditorGUIUtility.isProSkin)
            {
                return isList
                    ? DarkListBackground
                    : DarkGridBackground;
            }

            return isList
                ? LightListBackground
                : LightGridBackground;
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
