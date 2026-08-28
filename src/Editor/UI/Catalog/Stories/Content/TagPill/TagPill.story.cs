using System;
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
                    "Inputs",
                    "TagPill",
                    "短いタグ名を薄い面と境界線のpill形で表示するコンポーネントです。",
                    "先頭アイコン、pill全体の選択操作、右端の削除操作を用途に応じて組み合わせ、選択操作ではフォーカス色を表示します。",
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
            var text = "avatar";
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "タグ名を変更し、選択操作と削除操作を確認します。");
            var textField = AddTextField(
                controls.Content,
                "タグ名",
                text,
                value =>
                {
                    text = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var result = UiTextFactory.Create(
                "タグを選択または削除してください。",
                UiClassNames.SecondaryText);

            TagPill removable = null;
            removable = new TagPill(
                onRemove: () =>
                {
                    removable.style.display = DisplayStyle.None;
                    result.SetText("タグを削除しました。");
                });
            removable.style.marginRight = UiSpacingTokens.Medium;
            row.Add(removable);
            var clickable = new TagPill(
                new TagPillState(
                    "costume",
                    icon: FluentUiIcons.CreateState(
                        "add.png",
                        UiSizeTokens.Size12)),
                onClick: () => result.SetText("costumeを選択しました。"));
            row.Add(clickable);
            preview.Body.Add(row);
            preview.Body.Add(result);

            refresh = () =>
            {
                textField.SetValueWithoutNotify(text);
                removable.SetState(new TagPillState(
                    text,
                    text + "を削除"));
                removable.style.display = DisplayStyle.Flex;
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
