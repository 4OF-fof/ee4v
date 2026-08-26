using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class BadgeCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 22;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "badge", "Content", "Badge",
                    "件数や短い補足を表示する中立的なバッジです。",
                    "処理状態に限定せず、一覧項目の件数などに使用します。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildBadgeStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionGroupView.cs"
                    }));
            }
        }

        private void BuildBadgeStory(VisualElement parent)
        {
            var text = "12";
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "件数や短い分類値を変更し、中立表示を確認します。状態色はStatusBadgeで扱います。");
            var textField = AddTextField(
                controls.Content,
                "テキスト",
                text,
                value =>
                {
                    text = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var badge = new Badge();
            preview.Body.Add(CreatePreviewSurface(badge, true));

            refresh = () =>
            {
                textField.SetValueWithoutNotify(text);
                badge.SetText(text);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
