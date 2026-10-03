using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ScenePreviewViewportCatalogRegistrar :
            ICatalogRegistrar
        {
            public int Order => 19;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "scene-preview-viewport",
                    "Containers",
                    "ScenePreviewViewport",
                    "3Dプレビューのグリッド背景、描画領域、共通操作をまとめるコンテナです。",
                    "背景の明暗切り替え、表示リセット、機能固有オーバーレイを確認します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildScenePreviewViewportStory(parent),
                    new[]
                    {
                        "Editor/UI/Components/Content/PrefabScenePreview/PrefabScenePreview.cs"
                    }));
            }
        }

        private void BuildScenePreviewViewportStory(VisualElement parent)
        {
            var available = true;
            var resetCount = 0;
            var controls = CreatePlainControlsSection(
                parent,
                "表示の有無を切り替え、共通の背景と操作を確認します。");
            var resetStatus = UiTextFactory.Create(
                "表示リセット: 0回",
                UiClassNames.SecondaryText,
                "ee4v-ui-catalog-scene-preview-viewport__status");
            controls.Content.Add(resetStatus);

            var preview = CreatePreviewSection(parent);
            var viewport = new ScenePreviewViewport(
                _ => { },
                () =>
                {
                    resetCount++;
                    resetStatus.SetText(
                        "表示リセット: " + resetCount + "回");
                },
                "背景の明暗を切り替え",
                "表示をリセット");
            viewport.AddToClassList(
                "ee4v-ui-catalog-scene-preview-viewport");
            viewport.FeatureOverlay.Add(UiTextFactory.Create(
                "機能固有オーバーレイ",
                UiClassNames.SecondaryText,
                "ee4v-ui-catalog-scene-preview-viewport__feature"));
            viewport.SetPreviewAvailable(available);
            viewport.RegisterCallback<DetachFromPanelEvent>(_ =>
                viewport.Dispose());
            preview.Body.Add(CreatePreviewArea(viewport, true));

            AddToggle(
                controls.Content,
                "プレビュー内容を表示",
                available,
                value =>
                {
                    available = value;
                    viewport.SetPreviewAvailable(available);
                });
            FinalizeControlsSection(parent, controls);
        }
    }
}
