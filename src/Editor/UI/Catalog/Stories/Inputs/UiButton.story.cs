using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class UiButtonStoryProvider : IUiStoryProvider
    {
        public int Order => 5;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "ui-button",
                    "Inputs",
                    "UiButton",
                    "共通の余白と状態表現を持つボタンです。",
                    "文字は UiTextFactory、アイコンは Icon を使用します。",
                    Build,
                    dependencies: new[] { "Icon", "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Hierarchy/SceneSwitcher/UI/SceneSwitcherView.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleWindow.cs",
                        "Editor/AssetManager/UI/AssetManagerControls.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Content/Icon/icon.uss",
                        "Editor/UI/Components/Inputs/ui-button.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var column = new VisualElement();
            column.style.width = 260f;
            var primary = new UiButton(
                "Primary action",
                icon: IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Package,
                    UiSizeTokens.Size12));
            primary.style.marginBottom = UiSpacingTokens.Medium;
            column.Add(primary);
            var ghost = new UiButton(
                "Ghost action",
                icon: IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Refresh,
                    UiSizeTokens.Size12),
                variant: UiButtonVariant.Ghost);
            ghost.style.marginBottom = UiSpacingTokens.Medium;
            column.Add(ghost);
            column.Add(new UiButton(
                string.Empty,
                tooltip: "Close",
                icon: IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Close,
                    UiSizeTokens.Size12),
                variant: UiButtonVariant.Ghost,
                compact: true));
            parent.Add(column);
        }
    }
}
