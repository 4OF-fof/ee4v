using System;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerMainViewCatalogRegistrarStory
    {
        private sealed class AssetManagerMainViewCatalogRegistrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order
            {
                get { return 101; }
            }

            public void Register(CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/Panels/MainView/main-view.uss");
                registry.RegisterStory(new CatalogWindow.StoryRegistration(
                    "asset-manager-main-view",
                    "Domain/AssetManager",
                    "MainView",
                    "AssetManager 中央領域の toolbar 以下だけを表す main view コンポーネントです。",
                    "layout 内では上部 toolbar の下に配置し、単体 window では toolbar と呼び出し側で合成する前提です。一覧、空状態、進行中タスク表示などを置くベース領域として扱います。",
                    new[] { "AssetItemGrid" },
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildAssetManagerMainViewStory(window, parent)));
            }
        }

        private static void BuildAssetManagerMainViewStory(CatalogWindow window, VisualElement parent)
        {
            const string DatabaseErrorPresetId = "database-error";
            const string CollectionErrorPresetId = "collection-error";
            const string EmptyPresetId = "empty";
            var selectedPresetId = DatabaseErrorPresetId;
            Action<string> applyPreset = null;
            var controls = window.CreateTabbedControlsSection(
                parent,
                "MainView 全体を置き換えるエラー状態を切り替えて、中央配置と複数行表示を確認します。");

            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.paddingLeft = UiSpacingTokens.None;
            surface.style.paddingRight = UiSpacingTokens.None;
            surface.style.paddingTop = UiSpacingTokens.None;
            surface.style.paddingBottom = UiSpacingTokens.None;
            surface.style.height = 360f;

            var host = new MainViewHost();
            var panel = host.MainView;
            panel.RegisterCallback<DetachFromPanelEvent>(_ => host.Dispose());
            panel.style.flexGrow = 1f;
            surface.Add(panel);
            preview.Body.Add(surface);

            applyPreset = presetId =>
            {
                selectedPresetId = presetId;
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(
                                DatabaseErrorPresetId,
                                "Database"),
                            new TabCardTabState(
                                CollectionErrorPresetId,
                                "Collection"),
                            new TabCardTabState(
                                EmptyPresetId,
                                "Empty")
                        },
                        selectedPresetId),
                    applyPreset);
                var empty = string.Equals(
                    selectedPresetId,
                    EmptyPresetId,
                    StringComparison.Ordinal);
                panel.SetEmptyState(
                    empty
                        ? I18N.Get("assetManager.mainView.noItems")
                        : string.Empty);
                panel.SetExternalError(
                    empty
                        ? string.Empty
                        : I18N.Get(
                            string.Equals(
                                selectedPresetId,
                                CollectionErrorPresetId,
                                StringComparison.Ordinal)
                                ? "assetManager.mainView.preview.collectionError"
                                : "assetManager.error.databaseSchemaIncompatible"));
            };

            applyPreset(selectedPresetId);
            CatalogWindow.FinalizeControlsSection(parent, controls);
        }
    }
}
