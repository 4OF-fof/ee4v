using System;
using UnityEditor;

namespace Ee4v.UI
{
    internal sealed class AssetManagerViewItemState
    {
        public AssetManagerViewItemState(
            string id,
            string label,
            string meta,
            string eyebrow,
            string title,
            string description,
            string[] rows,
            IconState iconState = null)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Meta = meta ?? string.Empty;
            Eyebrow = eyebrow ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Rows = rows ?? Array.Empty<string>();
            IconState = iconState;
        }

        public string Id { get; }

        public string Label { get; }

        public string Meta { get; }

        public string Eyebrow { get; }

        public string Title { get; }

        public string Description { get; }

        public string[] Rows { get; }

        public IconState IconState { get; }
    }

    internal static class AssetManagerViewState
    {
        private const string SelectedItemEditorPrefsKey = "ee4v.asset-manager.selected-item";
        private static readonly AssetManagerViewItemState[] ItemsInternal =
        {
            new AssetManagerViewItemState(
                "all-assets",
                "All Assets",
                "240 items",
                "AssetManager",
                "All Assets",
                "プロジェクト全体を横断して検索、一覧、操作を行う標準ビューです。",
                new[]
                {
                    "検索バーとフィルタ",
                    "グリッド / リスト切り替え",
                    "一括操作と import/export 導線"
                },
                IconState.FromBuiltinIcon(UiBuiltinIcon.Search, size: 12f)),
            new AssetManagerViewItemState(
                "favorites",
                "Favorites",
                "Pinned",
                "Collections",
                "Favorites",
                "作業中に頻繁に触るアセットを優先表示するビューです。",
                new[]
                {
                    "お気に入りの一覧",
                    "最近の更新順ソート",
                    "即時プレビュー導線"
                },
                IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureOpen, size: 12f)),
            new AssetManagerViewItemState(
                "booth-library",
                "Booth Library",
                "Store Sync",
                "Source",
                "Booth Library",
                "Booth 連携済みライブラリと同期状態を扱うビューです。",
                new[]
                {
                    "購入済みアセット一覧",
                    "同期待ち / 更新待ちの確認",
                    "ダウンロード導線"
                },
                IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureClosed, size: 12f)),
            new AssetManagerViewItemState(
                "packages",
                "Packages",
                "UPM",
                "Source",
                "Packages",
                "Package Manager 由来のアセットと依存関係を確認するビューです。",
                new[]
                {
                    "導入済み package 一覧",
                    "依存関係の可視化",
                    "更新確認"
                })
        };

        private static string _selectedItemId;
        private static bool _loaded;

        public static event Action<string> SelectedItemChanged;

        public static AssetManagerViewItemState[] Items
        {
            get { return ItemsInternal; }
        }

        public static string SelectedItemId
        {
            get
            {
                EnsureLoaded();
                return _selectedItemId;
            }
        }

        public static AssetManagerViewItemState SelectedItem
        {
            get { return GetItem(SelectedItemId); }
        }

        public static AssetManagerViewItemState GetItem(string itemId)
        {
            EnsureLoaded();
            var resolvedId = NormalizeSelectedItemId(itemId);
            for (var i = 0; i < ItemsInternal.Length; i++)
            {
                if (string.Equals(ItemsInternal[i].Id, resolvedId, StringComparison.Ordinal))
                {
                    return ItemsInternal[i];
                }
            }

            return ItemsInternal[0];
        }

        public static void SetSelectedItem(string itemId, bool notify = true)
        {
            EnsureLoaded();
            var resolvedId = NormalizeSelectedItemId(itemId);
            if (string.Equals(_selectedItemId, resolvedId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedItemId = resolvedId;
            EditorPrefs.SetString(SelectedItemEditorPrefsKey, _selectedItemId);

            if (notify)
            {
                SelectedItemChanged?.Invoke(_selectedItemId);
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _selectedItemId = NormalizeSelectedItemId(EditorPrefs.GetString(SelectedItemEditorPrefsKey, ItemsInternal[0].Id));
            _loaded = true;
        }

        private static string NormalizeSelectedItemId(string itemId)
        {
            for (var i = 0; i < ItemsInternal.Length; i++)
            {
                if (string.Equals(ItemsInternal[i].Id, itemId, StringComparison.Ordinal))
                {
                    return ItemsInternal[i].Id;
                }
            }

            return ItemsInternal[0].Id;
        }
    }
}
