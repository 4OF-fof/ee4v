using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.AssetManager.Api;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager
{
    [InitializeOnLoad]
    internal static class AssetManagerStartupSyncConflictPresenter
    {
        static AssetManagerStartupSyncConflictPresenter()
        {
            AssetManagerStartupSync.ConfirmationRequested -= Show;
            AssetManagerStartupSync.ConfirmationRequested += Show;
        }

        private static void Show(IReadOnlyList<AssetSyncConflict> conflicts, Action<bool> onResolved)
        {
            var owner = FindOwner();
            if (owner == null)
            {
                onResolved(false);
                return;
            }

            owner.Show();
            owner.Focus();
            ShowWhenReady(owner, conflicts, onResolved, 0);
        }

        private static void ShowWhenReady(EditorWindow owner, IReadOnlyList<AssetSyncConflict> conflicts, Action<bool> onResolved, int attempt)
        {
            EditorApplication.delayCall += () =>
            {
                if (owner == null)
                {
                    onResolved(false);
                    return;
                }

                if (owner.rootVisualElement.childCount == 0 && attempt < 8)
                {
                    ShowWhenReady(owner, conflicts, onResolved, attempt + 1);
                    return;
                }

                var overlay = DiffConfirmationOverlayApi.Show(
                    owner,
                    CreateState(conflicts),
                    result => onResolved(result == DiffConfirmationResult.Overwrite));
                LoadThumbnails(overlay, conflicts);
            };
        }

        private static void LoadThumbnails(DiffConfirmationOverlay overlay, IReadOnlyList<AssetSyncConflict> conflicts)
        {
            if (overlay == null || conflicts == null)
            {
                return;
            }

            for (var i = 0; i < conflicts.Count; i++)
            {
                var index = i;
                var itemId = conflicts[i].ItemId;
                Task.Run(() => ReadThumbnail(itemId)).ContinueWith(task =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (overlay.panel == null || task.IsCanceled || task.IsFaulted)
                        {
                            return;
                        }

                        overlay.SetThumbnail(index, new ItemImageState("asset-manager:" + itemId, task.Result));
                    };
                });
            }
        }

        private static byte[] ReadThumbnail(string itemId)
        {
            try
            {
                var thumbnail = AssetManagerApi.GetThumbnail(itemId);
                return thumbnail != null && thumbnail.Found ? thumbnail.Data : Array.Empty<byte>();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private static EditorWindow FindOwner()
        {
            var focused = EditorWindow.focusedWindow;
            if (IsAssetManagerWindow(focused))
            {
                return focused;
            }

            var assetManager = Resources.FindObjectsOfTypeAll<AssetManagerWindow>().FirstOrDefault();
            if (assetManager != null)
            {
                return assetManager;
            }

            var mainView = Resources.FindObjectsOfTypeAll<MainViewWindow>().FirstOrDefault();
            if (mainView != null)
            {
                return mainView;
            }

            return EditorWindow.GetWindow<AssetManagerWindow>();
        }

        private static bool IsAssetManagerWindow(EditorWindow window)
        {
            return window is AssetManagerWindow ||
                   window is MainViewWindow;
        }

        private static DiffConfirmationState CreateState(IReadOnlyList<AssetSyncConflict> conflicts)
        {
            var items = (conflicts ?? Array.Empty<AssetSyncConflict>())
                .Select(CreateItem)
                .ToArray();
            return new DiffConfirmationState(
                I18N.Get("assetManager.syncConflict.title"),
                I18N.Get("assetManager.syncConflict.message"),
                I18N.Get("assetManager.syncConflict.current"),
                I18N.Get("assetManager.syncConflict.incoming"),
                I18N.Get("assetManager.syncConflict.overwrite"),
                I18N.Get("assetManager.syncConflict.cancel"),
                items);
        }

        private static DiffConfirmationItemState CreateItem(AssetSyncConflict conflict)
        {
            var source = conflict.SourceType == AssetSourceType.Eagle ? "Eagle" : "BLM";
            var sourceTime = conflict.DatasourceUpdatedAtUtc.HasValue
                ? conflict.DatasourceUpdatedAtUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                : I18N.Get("assetManager.syncConflict.unknownTime");
            var metadata = string.Format(
                CultureInfo.CurrentCulture,
                I18N.Get("assetManager.syncConflict.metadata"),
                source,
                conflict.UnityUpdatedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                sourceTime);
            return new DiffConfirmationItemState(
                conflict.ItemName,
                metadata,
                conflict.Fields.Select(field => new DiffConfirmationFieldState(
                    field.UnityValue,
                    field.DatasourceValue)).ToArray());
        }
    }
}
