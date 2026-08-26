using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ScrollViewCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 50;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "default-scrollbars",
                    "Collections",
                    "Default Scrollbars",
                    "Core UI の既定の細い縦横スクロールバーです。",
                    "UiComposition.Prepareを適用したrootでは、個別classなしで同じscrollbarを使用します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildScrollViewStory(parent),
                    new[]
                    {
                        "Editor/Core/Presentation/Settings/SettingsUiRenderer.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentView.cs"
                    }));
            }
        }

        private void BuildScrollViewStory(VisualElement parent)
        {
            var itemCount = 16;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "項目数を変更し、縦横のスクロールバーが必要になる境界を確認します。");
            var countField = UiTextFactory.CreateIntegerField("項目数");
            countField.SetValueWithoutNotify(itemCount);
            countField.RegisterValueChangedCallback(evt =>
            {
                itemCount = Math.Max(0, evt.newValue);
                refresh();
            });
            controls.Content.Add(countField);

            var preview = CreatePreviewSection(parent);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var vertical = new ScrollView(ScrollViewMode.Vertical);
            vertical.style.width = 280f;
            vertical.style.height = 220f;

            var horizontal = new ScrollView(ScrollViewMode.Horizontal);
            horizontal.style.width = 420f;
            horizontal.style.height = 90f;
            horizontal.style.marginLeft = UiSpacingTokens.Large;
            horizontal.contentContainer.style.flexDirection =
                FlexDirection.Row;

            row.Add(vertical);
            row.Add(horizontal);
            preview.Body.Add(row);

            refresh = () =>
            {
                countField.SetValueWithoutNotify(itemCount);
                vertical.Clear();
                horizontal.Clear();
                for (var index = 1; index <= itemCount; index++)
                {
                    var verticalItem = UiTextFactory.Create(
                        "Vertical item " + index);
                    verticalItem.style.height = 28f;
                    verticalItem.style.flexShrink = 0f;
                    vertical.Add(verticalItem);

                    var horizontalItem = UiTextFactory.Create(
                        "Horizontal " + index);
                    horizontalItem.style.width = 120f;
                    horizontalItem.style.flexShrink = 0f;
                    horizontal.Add(horizontalItem);
                }
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
