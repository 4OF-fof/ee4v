using System;
using System.Collections.Generic;
using System.IO;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.Injector;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerProjectDecorationPresenter :
        IAssetManagerProjectActions,
        IDisposable
    {
        private static readonly Color HighlightColor =
            new Color(0.18f, 0.55f, 0.32f, 0.38f);
        private static readonly Color DarkListBackground =
            new Color32(56, 56, 56, 255);
        private static readonly Color DarkGridBackground =
            new Color32(51, 51, 51, 255);
        private static readonly Color LightListBackground =
            new Color32(200, 200, 200, 255);
        private static readonly Color LightGridBackground =
            new Color32(189, 189, 189, 255);

        private readonly IAssetManagerProjectCacheSource
            _cacheSource;
        private readonly IAssetManagerUiScheduler _scheduler;
        private readonly Dictionary<string, Texture2D>
            _iconsByGuid =
                new Dictionary<string, Texture2D>(
                    StringComparer.Ordinal);
        private readonly HashSet<string> _highlightedGuids =
            new HashSet<string>(StringComparer.Ordinal);
        private AssetManagerProjectAssociationIndex
            _associationIndex =
                AssetManagerProjectAssociationIndex.Create(null);
        private readonly AssetManagerProjectHighlightSelection
            _highlightSelection =
                new AssetManagerProjectHighlightSelection();
        private bool _initialized;
        private bool _disposed;

        internal AssetManagerProjectDecorationPresenter(
            IAssetManager assetManager,
            IAssetManagerUiScheduler scheduler)
            : this(
                new AssetManagerProjectCacheSource(
                    assetManager),
                scheduler)
        {
        }

        internal AssetManagerProjectDecorationPresenter(
            IAssetManagerProjectCacheSource cacheSource,
            IAssetManagerUiScheduler scheduler)
        {
            _cacheSource = cacheSource ??
                throw new ArgumentNullException(
                    nameof(cacheSource));
            _scheduler = scheduler ??
                throw new ArgumentNullException(nameof(scheduler));
        }

        internal void Initialize()
        {
            if (_initialized || _disposed)
            {
                return;
            }

            _initialized = true;
            _cacheSource.Changed += OnAssetManagerChanged;
            TryReloadAssociations();
        }

        internal bool ShowIcons { get; set; }

        internal event Action DecorationChanged;

        public bool CanHighlightItem(string itemId)
        {
            return HasGuids(
                _associationIndex.GuidsByItem,
                itemId);
        }

        public bool CanHighlightFile(string fileId)
        {
            return HasGuids(
                _associationIndex.GuidsByFile,
                fileId);
        }

        public bool IsItemHighlighted(string itemId)
        {
            return _highlightSelection.IsSelected(
                AssetManagerProjectHighlightTargetKind.Item,
                itemId);
        }

        public bool IsFileHighlighted(string fileId)
        {
            return _highlightSelection.IsSelected(
                AssetManagerProjectHighlightTargetKind.File,
                fileId);
        }

        public bool IsGuidHighlighted(string guid)
        {
            return !string.IsNullOrWhiteSpace(guid) &&
                   _highlightedGuids.Contains(guid);
        }

        public void HighlightItem(string itemId)
        {
            Highlight(
                _associationIndex.GuidsByItem,
                itemId,
                AssetManagerProjectHighlightTargetKind.Item);
        }

        public void HighlightFile(string fileId)
        {
            Highlight(
                _associationIndex.GuidsByFile,
                fileId,
                AssetManagerProjectHighlightTargetKind.File);
        }

        public void ClearHighlights()
        {
            var hadGuids = _highlightedGuids.Count > 0;
            var hadTarget = _highlightSelection.Clear();
            if (!hadGuids && !hadTarget)
            {
                return;
            }

            _highlightedGuids.Clear();
            DecorationChanged?.Invoke();
        }

        internal void Draw(ItemInjectionContext context)
        {
            if (context == null ||
                string.IsNullOrWhiteSpace(context.Guid) ||
                Event.current == null ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (_highlightedGuids.Contains(context.Guid))
            {
                EditorGUI.DrawRect(
                    context.SelectionRect,
                    HighlightColor);
            }

            Texture2D icon;
            if (!ShowIcons ||
                !_iconsByGuid.TryGetValue(
                    context.Guid,
                    out icon) ||
                icon == null)
            {
                return;
            }

            var iconRect = ProjectItemLayout.GetIconRect(
                context.SelectionRect,
                context.ProjectViewMode,
                context.ProjectOrientation);
            EditorGUI.DrawRect(
                iconRect,
                ResolveBackgroundColor(context));
            GUI.DrawTexture(
                iconRect,
                icon,
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
            if (_initialized)
            {
                _cacheSource.Changed -= OnAssetManagerChanged;
            }
        }

        private void Highlight(
            IReadOnlyDictionary<
                string,
                IReadOnlyList<string>> source,
            string id,
            AssetManagerProjectHighlightTargetKind targetKind)
        {
            IReadOnlyList<string> guids;
            if (string.IsNullOrWhiteSpace(id) ||
                !source.TryGetValue(id, out guids))
            {
                return;
            }

            _highlightedGuids.Clear();
            _highlightSelection.Select(targetKind, id);
            for (var i = 0; i < guids.Count; i++)
            {
                AddGuidAndParents(guids[i]);
            }

            DecorationChanged?.Invoke();
        }

        private void AddGuidAndParents(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return;
            }

            _highlightedGuids.Add(guid);
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            while (!string.IsNullOrWhiteSpace(assetPath))
            {
                assetPath = Path.GetDirectoryName(assetPath)
                    ?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(assetPath) ||
                    string.Equals(
                        assetPath,
                        "Assets",
                        StringComparison.Ordinal))
                {
                    break;
                }

                var parentGuid =
                    AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrWhiteSpace(parentGuid))
                {
                    _highlightedGuids.Add(parentGuid);
                }
            }
        }

        private void OnAssetManagerChanged(
            AssetManagerChange change)
        {
            if (change == null ||
                change.Kind !=
                AssetManagerChangeKind.ImportedAssetGuids &&
                change.Kind != AssetManagerChangeKind.Catalog)
            {
                return;
            }

            _scheduler.RunOnMainThread(() =>
            {
                if (_disposed)
                {
                    return;
                }

                if (TryReloadAssociations())
                {
                    DecorationChanged?.Invoke();
                }
            });
        }

        private bool TryReloadAssociations()
        {
            try
            {
                ReloadAssociations();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private void ReloadAssociations()
        {
            var associations =
                _cacheSource.GetImportedAssetAssociations() ??
                Array.Empty<AssetImportedAssetAssociation>();
            var associationIndex =
                AssetManagerProjectAssociationIndex.Create(
                    associations);
            var folderCandidates =
                new List<
                    AssetManagerProjectFolderIconCandidate>();
            foreach (var pair in
                     associationIndex.ItemIdByAssetGuid)
            {
                var assetPath =
                    AssetDatabase.GUIDToAssetPath(pair.Key);
                if (string.IsNullOrWhiteSpace(assetPath) ||
                    !AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                folderCandidates.Add(
                    new AssetManagerProjectFolderIconCandidate(
                        pair.Key,
                        assetPath,
                        pair.Value));
            }

            var selectedFolders =
                AssetManagerProjectFolderIconSelection
                    .SelectTopmost(folderCandidates);
            var itemIds =
                new List<string>();
            foreach (var itemId in
                     selectedFolders.Values)
            {
                if (!itemIds.Contains(itemId))
                {
                    itemIds.Add(itemId);
                }
            }

            var thumbnails =
                _cacheSource.GetThumbnails(itemIds);
            var iconsByGuid =
                new Dictionary<string, Texture2D>(
                    StringComparer.Ordinal);
            foreach (var pair in
                     selectedFolders)
            {
                AssetThumbnail thumbnail;
                if (!thumbnails.TryGetValue(
                        pair.Value,
                        out thumbnail) ||
                    thumbnail == null ||
                    !thumbnail.Found ||
                    thumbnail.Data == null ||
                    thumbnail.Data.Length == 0)
                {
                    continue;
                }

                var texture =
                    ItemImageTextureCache.GetTexture(
                        new ItemImageState(
                            pair.Value,
                            thumbnail.Data));
                if (texture != null)
                {
                    iconsByGuid[pair.Key] = texture;
                }
            }

            _associationIndex = associationIndex;
            _iconsByGuid.Clear();
            foreach (var pair in iconsByGuid)
            {
                _iconsByGuid[pair.Key] = pair.Value;
            }
        }

        private static bool HasGuids(
            IReadOnlyDictionary<
                string,
                IReadOnlyList<string>> source,
            string id)
        {
            IReadOnlyList<string> guids;
            return !string.IsNullOrWhiteSpace(id) &&
                   source.TryGetValue(id, out guids) &&
                   guids.Count > 0;
        }

        private static Color ResolveBackgroundColor(
            ItemInjectionContext context)
        {
            var isList =
                context.ProjectViewMode ==
                ProjectItemViewMode.OneColumn ||
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
    }

    internal enum AssetManagerProjectHighlightTargetKind
    {
        None,
        Item,
        File
    }

    internal sealed class AssetManagerProjectHighlightSelection
    {
        private AssetManagerProjectHighlightTargetKind _kind;
        private string _id = string.Empty;

        internal bool IsSelected(
            AssetManagerProjectHighlightTargetKind kind,
            string id)
        {
            return _kind == kind &&
                   kind !=
                   AssetManagerProjectHighlightTargetKind.None &&
                   !string.IsNullOrWhiteSpace(id) &&
                   string.Equals(
                       _id,
                       id,
                       StringComparison.Ordinal);
        }

        internal void Select(
            AssetManagerProjectHighlightTargetKind kind,
            string id)
        {
            if (kind ==
                    AssetManagerProjectHighlightTargetKind.None ||
                string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            _kind = kind;
            _id = id;
        }

        internal bool Clear()
        {
            if (_kind ==
                AssetManagerProjectHighlightTargetKind.None)
            {
                return false;
            }

            _kind = AssetManagerProjectHighlightTargetKind.None;
            _id = string.Empty;
            return true;
        }
    }

    internal static class AssetManagerProjectHighlightMenu
    {
        private const string ClearHighlightMenuPath =
            "Assets/AssetManager/Clear Highlight";

        [MenuItem(ClearHighlightMenuPath, false, 2100)]
        private static void ClearHighlight()
        {
            if (AssetManagerUiDependencies
                .TryGetProjectActions(out var projectActions))
            {
                projectActions.ClearHighlights();
            }
        }

        [MenuItem(ClearHighlightMenuPath, true)]
        private static bool CanClearHighlight()
        {
            if (!AssetManagerUiDependencies
                    .TryGetProjectActions(
                        out var projectActions) ||
                Selection.activeObject == null)
            {
                return false;
            }

            var assetPath =
                AssetDatabase.GetAssetPath(
                    Selection.activeObject);
            var guid =
                string.IsNullOrWhiteSpace(assetPath)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(
                        assetPath);
            return projectActions.IsGuidHighlighted(guid);
        }
    }
}
