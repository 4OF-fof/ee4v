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
                    "共通の余白と状態表現を持つボタンです。",
                    "文字はUiTextFactory、アイコンはIconを使用します。",
                    new[] { "Icon", "UiTextFactory" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildUiButtonStory(parent),
                    new[]
                    {
                        "Editor/Feature/Hierarchy/SceneSwitcher/UI/SceneSwitcherView.cs",
                        "Editor/AssetManager/UI/AssetManagerControls.cs",
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    }));
            }
        }

        private void BuildUiButtonStory(VisualElement parent)
        {
            var label = "Primary action";
            var variant = UiButtonVariant.Solid;
            var compact = false;
            var showIcon = true;
            var enabled = true;
            var clickCount = 0;
            Action refresh = null;

            var controls = CreatePlainControlsSection(
                parent,
                "文字、種類、密度、アイコン、有効状態を変更します。");
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
            var compactToggle = AddToggle(
                controls.Content,
                "小型",
                compact,
                value =>
                {
                    compact = value;
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
            var buttonHost = CreatePreviewSurface(true);
            var result = UiTextFactory.Create(
                "ボタンはまだ押されていません。",
                UiClassNames.SecondaryText);
            preview.Body.Add(buttonHost);
            preview.Body.Add(result);

            refresh = () =>
            {
                labelField.SetValueWithoutNotify(label);
                variantField.SetValueWithoutNotify((Enum)(object)variant);
                compactToggle.SetValueWithoutNotify(compact);
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
                    variant: variant,
                    compact: compact);
                button.SetEnabled(enabled);
                buttonHost.Add(button);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
