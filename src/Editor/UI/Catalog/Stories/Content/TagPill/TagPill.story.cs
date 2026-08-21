using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class TagPillCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 24;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Content/TagPill/tag-pill.uss");
                registry.RegisterStory(new StoryRegistration(
                    "tag-pill",
                    "Content",
                    "TagPill",
                    "短いタグ名をpill形で表示するコンポーネントです。",
                    "先頭アイコン、pill全体の選択操作、右端の削除操作を用途に応じて組み合わせます。",
                    new[] { "UiButton", "Icon", "UiTextFactory" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildTagPillStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetTagField.cs"
                    }));
            }
        }

        private void BuildTagPillStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            TagPill removable = null;
            removable = new TagPill(
                new TagPillState("avatar", "avatarを削除"),
                () => removable.RemoveFromHierarchy());
            removable.style.marginRight = UiSpacingTokens.Medium;
            row.Add(removable);
            var clickable = new TagPill(
                new TagPillState(
                    "costume",
                    icon: FluentUiIcons.CreateState(
                        "add.png",
                        UiSizeTokens.Size12)),
                onClick: () => { });
            row.Add(clickable);
            preview.Body.Add(row);
        }
    }
}
