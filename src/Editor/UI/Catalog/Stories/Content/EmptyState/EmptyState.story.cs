using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class EmptyStateCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 14;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "empty-state",
                    "Content",
                    "EmptyState",
                    "結果や対象がない状態を、アイコン・見出し・説明・操作で表すコンポーネントです。",
                    "空状態のアイコン、見出し、説明、次の操作を中央へまとめて表示します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildEmptyStateStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetTagField.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectTreeView.cs",
                        "Editor/Feature/Hierarchy/SceneSwitcher/UI/SceneSwitcherView.cs",
                        "Editor/AssetManager/AssetProtection/AssetProtectionModificationProcessor.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildEmptyStateStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var state = new EmptyState(new EmptyStateState(
                "項目がありません",
                "条件を変更するか、新しい項目を追加してください。",
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Info,
                    UiSizeTokens.Size24)));
            state.Actions.Add(UiTextFactory.CreateButton(
                "項目を追加",
                () => { }));
            preview.Body.Add(state);
        }
    }
}
