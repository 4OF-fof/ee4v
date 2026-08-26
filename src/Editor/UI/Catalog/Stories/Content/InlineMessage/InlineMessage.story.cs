using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class InlineMessageCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 17;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "inline-message",
                    "Content",
                    "InlineMessage",
                    "処理結果や入力エラーを行内に表示するメッセージです。",
                    "メッセージ、状態色、任意のアイコンを一貫した行内表現で表示します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildInlineMessageStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerControls.cs",
                        "Editor/AssetManager/UI/SearchableFileTree.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildInlineMessageStory(VisualElement parent)
        {
            var text = "設定を保存しました。";
            var tone = UiStatusTone.Passed;
            var showIcon = true;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "メッセージ、状態、アイコンの有無を変更します。");
            var textField = AddTextField(
                controls.Content,
                "メッセージ",
                text,
                value =>
                {
                    text = value;
                    refresh();
                });
            var toneField = AddEnumField(
                controls.Content,
                "状態",
                tone,
                value =>
                {
                    tone = value;
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

            var preview = CreatePreviewSection(parent);
            var message = new InlineMessage();
            preview.Body.Add(message);

            refresh = () =>
            {
                textField.SetValueWithoutNotify(text);
                toneField.SetValueWithoutNotify((Enum)(object)tone);
                iconToggle.SetValueWithoutNotify(showIcon);
                message.SetState(new InlineMessageState(
                    text,
                    tone,
                    showIcon
                        ? IconState.FromBuiltinIcon(
                            UiBuiltinIcon.Info,
                            UiSizeTokens.Size16)
                        : null));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
