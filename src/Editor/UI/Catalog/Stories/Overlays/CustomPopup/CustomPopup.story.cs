using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class CustomPopupStoryProvider : IUiStoryProvider
    {
        public int Order => 40;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "custom-popup",
                    "Overlays",
                    "CustomPopup",
                    "ドロップダウン型EditorWindowの共通外枠です。",
                    "同じ線幅、背景、ヘッダー、本文、任意フッターを持ち、表示位置と固定サイズの設定も共通化します。",
                    Build,
                    dependencies: new[] { "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetTagField.cs",
                        "Editor/AssetManager/UI/AssetCollectionCreationPopup.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Overlays/CustomPopup/custom-popup.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var popup = new CustomPopup(
                "Custom Popup",
                showFooter: true);
            popup.style.width = 360f;
            popup.style.height = 220f;
            popup.style.flexGrow = 0f;

            var body = new VisualElement();
            body.style.paddingLeft = UiSpacingTokens.Xl;
            body.style.paddingRight = UiSpacingTokens.Xl;
            body.style.paddingTop = UiSpacingTokens.Xl;
            body.style.paddingBottom = UiSpacingTokens.Xl;
            body.Add(UiTextFactory.Create(
                "Feature固有のフォームや一覧を本文へ配置します。"));
            popup.Content.Add(body);

            popup.HeaderActions.Add(UiTextFactory.CreateButton("Close"));
            popup.Footer.Add(UiTextFactory.CreateButton("Cancel"));
            popup.Footer.Add(UiTextFactory.CreateButton("Apply"));
            parent.Add(popup);
        }
    }
}
