using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectsStoryProvider : IUiStoryProvider
    {
        public int Order => 175;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "hidden-objects",
                    "Domain/HierarchyStyle",
                    "Hidden Objects",
                    "Hierarchy から非表示にした GameObject を確認、選択、再表示する画面です。",
                    "実際の toolbar、scene tree、footer をサンプル状態で表示します。",
                    Build,
                    dependencies: new[]
                    {
                        "SearchField",
                        "Icon",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectsWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Content/Icon/icon.uss",
                        "Editor/UI/Components/Inputs/SearchField/search-field.uss",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/hidden-objects-window.uss"
                    }),
                new UiStory(
                    "hidden-objects-toolbar",
                    "Domain/HierarchyStyle/Components",
                    "HiddenObjectsToolbar",
                    "検索、Scene絞り込み、更新操作をまとめるToolbarです。",
                    "検索文字列とSceneを指定して表示対象を絞り込み、一覧を更新できます。",
                    BuildToolbar,
                    dependencies: new[]
                    {
                        "ActionBar",
                        "SearchField",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectsView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/hidden-objects-window.uss"
                    }),
                new UiStory(
                    "hidden-objects-footer",
                    "Domain/HierarchyStyle/Components",
                    "HiddenObjectsFooter",
                    "選択状態のSummaryと一括操作をまとめるFooterです。",
                    "件数と選択状態を表示し、全選択、選択解除、再表示を実行できます。",
                    BuildFooter,
                    dependencies: new[]
                    {
                        "ActionBar",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectsView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/hidden-objects-window.uss"
                    }),
                new UiStory(
                    "hidden-object-tree-row",
                    "Domain/HierarchyStyle/Components",
                    "HiddenObjectTreeRow",
                    "非表示Object一件の選択、Icon、名前を表示する行です。",
                    "非表示Objectのアイコン、名前、補足を表示し、選択とフォーカス操作を通知します。",
                    BuildTreeRow,
                    dependencies: new[]
                    {
                        "ItemRow",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectTreeView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/hidden-objects-window.uss"
                    })
            };
        }

        private static void BuildToolbar(VisualElement parent)
        {
            var toolbar = new HiddenObjectsToolbar(CreateText());
            toolbar.SetState(
                string.Empty,
                new[]
                {
                    new HiddenObjectSceneOptionViewState(0, "All scenes"),
                    new HiddenObjectSceneOptionViewState(1, "Main"),
                    new HiddenObjectSceneOptionViewState(2, "Avatar Preview")
                },
                0);
            toolbar.style.width = 620f;
            parent.Add(toolbar);
        }

        private static void BuildFooter(VisualElement parent)
        {
            var footer = new HiddenObjectsFooter(CreateText());
            footer.SetState(
                "3 hidden · 3 visible · 1 selected",
                3,
                1);
            footer.style.width = 620f;
            parent.Add(footer);
        }

        private static void BuildTreeRow(VisualElement parent)
        {
            var row = new HiddenObjectTreeRow();
            row.SetState(new HiddenObjectTreeItemViewState(
                1L,
                false,
                101,
                "Preview Avatar",
                "Hidden",
                true,
                true,
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.GameObject,
                    UiSizeTokens.Size16)));
            row.style.width = 420f;
            parent.Add(row);
        }

        private static HiddenObjectsViewText CreateText()
        {
            return new HiddenObjectsViewText(
                "Search hidden objects",
                "Filter hidden objects",
                "Clear search",
                "Filter by scene",
                "Refresh",
                "Refresh hidden objects",
                "Select all visible",
                "Clear selection",
                "Reveal selected");
        }

        private static void Build(VisualElement parent)
        {
            var view = new HiddenObjectsView(CreateText());
            var environment = new HiddenObjectNodeViewState(
                101,
                "Environment",
                true,
                true,
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.GameObject,
                    UiSizeTokens.Size16),
                new[]
                {
                    new HiddenObjectNodeViewState(
                        102,
                        "Reflection Probes",
                        true,
                        false,
                        null,
                        Array.Empty<HiddenObjectNodeViewState>())
                });
            var avatar = new HiddenObjectNodeViewState(
                201,
                "Preview Avatar",
                true,
                false,
                null,
                Array.Empty<HiddenObjectNodeViewState>());
            view.SetState(new HiddenObjectsViewState(
                new[]
                {
                    new HiddenObjectSceneGroupViewState(
                        1,
                        "Main",
                        "2 hidden",
                        new[] { environment }),
                    new HiddenObjectSceneGroupViewState(
                        2,
                        "Avatar Preview",
                        "1 hidden",
                        new[] { avatar })
                },
                new[]
                {
                    new HiddenObjectSceneOptionViewState(0, "All scenes"),
                    new HiddenObjectSceneOptionViewState(1, "Main"),
                    new HiddenObjectSceneOptionViewState(2, "Avatar Preview")
                },
                0,
                string.Empty,
                "3 hidden · 3 visible · 1 selected",
                "No hidden objects",
                "Hidden objects will appear here.",
                3,
                1));

            var surface = new VisualElement();
            surface.AddToClassList("ee4v-hidden-objects-window");
            surface.style.width = 620f;
            surface.style.height = 440f;
            surface.Add(view);
            parent.Add(surface);
        }
    }
}
