using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ColorPaletteStoryProvider : IUiStoryProvider
    {
        public int Order => 0;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "color-palette",
                    "Reference",
                    "Color Palette",
                    "UIとIMGUIが共有する役割別の色を一覧表示します。",
                    "色は用途名で参照し、部品側へ直接カラー値を書きません。",
                    Build,
                    dependencies: new[] { "UiColorTokens" })
            };
        }

        private static void Build(VisualElement parent)
        {
            var palette = UiColorPalettes.UnityDark;
            var entries = new[]
            {
                Entry("ChromeDeep", palette.ChromeDeep),
                Entry("TabIdle", palette.TabIdle),
                Entry("Field", palette.Field),
                Entry("Panel", palette.Panel),
                Entry("SurfaceRaised", palette.SurfaceRaised),
                Entry("Control", palette.Control),
                Entry("ToolActive", palette.ToolActive),
                Entry("Selection", palette.Selection),
                Entry("Focus", palette.Focus),
                Entry("Error", palette.Error),
                Entry("TextPrimary", palette.TextPrimary),
                Entry("TextSecondary", palette.TextSecondary),
                Entry("TextMuted", palette.TextMuted),
                Entry("TextSoft", palette.TextSoft),
                Entry("TextDisabled", palette.TextDisabled),
                Entry("StatusIdleText", palette.StatusIdleText),
                Entry("StatusRunningText", palette.StatusRunningText),
                Entry("StatusPassedText", palette.StatusPassedText),
                Entry("StatusFailedText", palette.StatusFailedText),
                Entry("StatusSkippedText", palette.StatusSkippedText),
                Entry("StatusInconclusiveText", palette.StatusInconclusiveText)
            };

            var grid = new VisualElement();
            grid.AddToClassList("ee4v-ui-catalog-color-palette");
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var item = new VisualElement();
                item.AddToClassList("ee4v-ui-catalog-color-palette__item");

                var swatch = new VisualElement();
                swatch.AddToClassList("ee4v-ui-catalog-color-palette__swatch");
                swatch.style.backgroundColor = (Color)entry.Value;
                item.Add(swatch);

                item.Add(UiTextFactory.Create(
                    entry.Key,
                    UiClassNames.FormLabel));
                item.Add(UiTextFactory.Create(
                    "#" + ColorUtility.ToHtmlStringRGBA(entry.Value),
                    UiClassNames.SecondaryText));
                grid.Add(item);
            }

            parent.Add(grid);
        }

        private static KeyValuePair<string, Color32> Entry(
            string name,
            Color32 color)
        {
            return new KeyValuePair<string, Color32>(name, color);
        }
    }
}
