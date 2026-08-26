using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class CustomPopupCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 40;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Overlays/CustomPopup/custom-popup.uss");
                registry.RegisterStory(new StoryRegistration(
                    "custom-popup",
                    "Overlays",
                    "CustomPopup",
                    "旧BaseWindowを移植したpopup用EditorWindowの共通外枠です。",
                    "1pxの外枠、24pxのヘッダー、本文、任意フッターを持ち、ドラッグ移動と端のリサイズに対応します。",
                    new[] { "UiTextFactory" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildCustomPopupStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetTagField.cs",
                        "Editor/AssetManager/UI/AssetCollectionCreationPopup.cs",
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/AssetManager/UI/DerivedAssetPrefabPickerWindow.cs",
                        "Editor/Feature/Avatar/PlayModeComponentSuppression/ComponentTypePickerWindow.cs"
                    }));
            }
        }

        private void BuildCustomPopupStory(VisualElement parent)
        {
            var title = "Custom Popup";
            var showFooter = true;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "タイトルとフッターの表示を変更します。実際のWindow操作は使用画面で確認します。");
            var titleField = AddTextField(
                controls.Content,
                "タイトル",
                title,
                value =>
                {
                    title = value;
                    refresh();
                });
            var footerToggle = AddToggle(
                controls.Content,
                "フッターを表示",
                showFooter,
                value =>
                {
                    showFooter = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var popup = new CustomPopup(
                title,
                showFooter: true,
                closeTooltip: "Close");
            popup.style.width = 360f;
            popup.style.height = 220f;
            popup.style.flexGrow = 0f;
            var body = new VisualElement();
            body.style.paddingLeft = UiSpacingTokens.Xl;
            body.style.paddingRight = UiSpacingTokens.Xl;
            body.style.paddingTop = UiSpacingTokens.Xl;
            body.style.paddingBottom = UiSpacingTokens.Xl;
            var result = UiTextFactory.Create(
                "Feature固有のフォームや一覧を本文へ配置します。");
            body.Add(result);
            popup.Content.Add(body);

            popup.Footer.Add(UiTextFactory.CreateButton(
                "Cancel",
                () => result.SetText("キャンセルしました。")));
            popup.Footer.Add(UiTextFactory.CreateButton(
                "Apply",
                () => result.SetText("適用しました。")));
            preview.Body.Add(popup);

            refresh = () =>
            {
                titleField.SetValueWithoutNotify(title);
                footerToggle.SetValueWithoutNotify(showFooter);
                popup.SetTitle(title);
                popup.SetFooterVisible(showFooter);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
