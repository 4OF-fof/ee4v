using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ScrollViewStoryProvider : IUiStoryProvider
    {
        public int Order => 50;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "default-scrollbars",
                    "Collections",
                    "Default Scrollbars",
                    "Core UI の既定の細い縦横スクロールバーです。",
                    "UiComposition.Prepareを適用したrootでは、個別classなしで同じscrollbarを使用します。",
                    Build,
                    usageLocations: new[]
                    {
                        "Editor/Core/Presentation/Settings/SettingsUiRenderer.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentView.cs"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var vertical = new ScrollView(ScrollViewMode.Vertical);
            vertical.style.width = 280f;
            vertical.style.height = 220f;
            for (var index = 1; index <= 16; index++)
            {
                var item = UiTextFactory.Create("Vertical item " + index);
                item.style.height = 28f;
                item.style.flexShrink = 0f;
                vertical.Add(item);
            }

            var horizontal = new ScrollView(ScrollViewMode.Horizontal);
            horizontal.style.width = 420f;
            horizontal.style.height = 90f;
            horizontal.style.marginLeft = UiSpacingTokens.Large;
            horizontal.contentContainer.style.flexDirection =
                FlexDirection.Row;
            for (var index = 1; index <= 8; index++)
            {
                var item = UiTextFactory.Create("Horizontal " + index);
                item.style.width = 120f;
                item.style.flexShrink = 0f;
                horizontal.Add(item);
            }

            row.Add(vertical);
            row.Add(horizontal);
            parent.Add(row);
        }
    }
}
