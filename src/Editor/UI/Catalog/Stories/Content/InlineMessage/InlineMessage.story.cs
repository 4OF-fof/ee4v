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
            var preview = CreatePreviewSection(parent);
            preview.Body.Add(new InlineMessage(new InlineMessageState(
                "設定を保存しました。",
                UiStatusTone.Passed,
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Info,
                    UiSizeTokens.Size16))));
            preview.Body.Add(new InlineMessage(new InlineMessageState(
                "名前を入力してください。",
                UiStatusTone.Failed)));
        }
    }
}
