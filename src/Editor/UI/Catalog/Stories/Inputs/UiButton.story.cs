using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class UiButtonCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 5;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Content/Icon/icon.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/ui-button.uss");
                registry.RegisterStory(new StoryRegistration(
                    "ui-button",
                    "Inputs",
                    "UiButton",
                    "InputFieldと共通の境界線とフォーカス色を持ち、操作面を薄く塗ったボタンです。",
                    "文字はUiTextFactory、アイコンはIconを使用します。Ghostは通常時の境界線を表示しません。",
                    new[] { "Icon", "UiTextFactory" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildUiButtonStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerControls.cs",
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/AssetManager/UI/AssetTagField.cs",
                        "Editor/AssetManager/UI/DerivedAssetPrefabPickerWindow.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs",
                        "Editor/Feature/Hierarchy/SceneSwitcher/UI/SceneSwitcherView.cs",
                        "Editor/Feature/Project/ProjectTabs/UI/ProjectTabsView.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleEditor.cs",
                        "Editor/UI/Components/Content/DisclosureSection/DisclosureSection.cs",
                        "Editor/UI/Components/Content/TagPill/TagPill.cs",
                        "Editor/UI/Components/Overlays/CustomPopup/CustomPopup.cs"
                    }));
            }
        }

        private void BuildUiButtonStory(VisualElement parent)
        {
            var label = "Primary action";
            var variant = UiButtonVariant.Solid;
            var showIcon = true;
            var enabled = true;
            var clickCount = 0;
            Action refresh = null;

            var controls = CreatePlainControlsSection(
                parent,
                "文字、種類、アイコン、有効状態を変更します。");
            var labelField = AddTextField(
                controls.Content,
                "文字",
                label,
                value =>
                {
                    label = value;
                    refresh();
                });
            var variantField = AddEnumField(
                controls.Content,
                "種類",
                variant,
                value =>
                {
                    variant = value;
                    refresh();
                });
            var iconToggle = AddToggle(
                controls.Content,
                "アイコンを表示",
                showIcon,
                value =>
                {
                    showIcon = value;
                    refresh();
                });
            var enabledToggle = AddToggle(
                controls.Content,
                "有効",
                enabled,
                value =>
                {
                    enabled = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var buttonHost = CreatePreviewArea(true);
            var result = UiTextFactory.Create(
                "ボタンはまだ押されていません。",
                UiClassNames.SecondaryText);
            preview.Body.Add(buttonHost);
            preview.Body.Add(result);

            refresh = () =>
            {
                labelField.SetValueWithoutNotify(label);
                variantField.SetValueWithoutNotify((Enum)(object)variant);
                iconToggle.SetValueWithoutNotify(showIcon);
                enabledToggle.SetValueWithoutNotify(enabled);

                buttonHost.Clear();
                var button = new UiButton(
                    label,
                    () =>
                    {
                        clickCount++;
                        result.SetText(
                            "ボタンを" + clickCount + "回押しました。");
                    },
                    icon: showIcon
                        ? FluentUiIcons.CreateState(
                            "archive.png",
                            UiSizeTokens.Size12)
                        : null,
                    variant: variant);
                button.SetEnabled(enabled);
                buttonHost.Add(button);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
