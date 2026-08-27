using System;
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
                    "Feedback",
                    "EmptyState",
                    "結果や対象がない状態を、アイコン・見出し・説明・操作で表すコンポーネントです。",
                    "空状態のアイコン、見出し、説明、次の操作を中央へまとめて表示します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildEmptyStateStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetTagField.cs",
                        "Editor/AssetManager/UI/DerivedAssetPrefabPickerWindow.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/Components/GestureAssignmentCell.cs",
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
            var title = "項目がありません";
            var description =
                "条件を変更するか、新しい項目を追加してください。";
            var showIcon = true;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "見出し、説明、アイコンの有無を変更して空状態を確認します。");
            var titleField = AddTextField(
                controls.Content,
                "見出し",
                title,
                value =>
                {
                    title = value;
                    refresh();
                });
            var descriptionField = AddTextField(
                controls.Content,
                "説明",
                description,
                value =>
                {
                    description = value;
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
            var state = new EmptyState();
            var result = UiTextFactory.Create(
                "次の操作を選択してください。",
                UiClassNames.SecondaryText);
            state.Actions.Add(UiTextFactory.CreateButton(
                "項目を追加",
                () => result.SetText("項目の追加を要求しました。")));
            preview.Body.Add(state);
            preview.Body.Add(result);

            refresh = () =>
            {
                titleField.SetValueWithoutNotify(title);
                descriptionField.SetValueWithoutNotify(description);
                iconToggle.SetValueWithoutNotify(showIcon);
                state.SetState(new EmptyStateState(
                    title,
                    description,
                    showIcon
                        ? FluentUiIcons.CreateState(
                            "info.png",
                            UiSizeTokens.Size24)
                        : null));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
