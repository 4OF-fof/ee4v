using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private VisualElement CreateSampleTreeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-ui-catalog-tree-item");
            row.Add(UiTextFactory.Create(string.Empty, UiClassNames.CatalogTreeTitle));
            row.Add(UiTextFactory.Create(string.Empty, UiClassNames.CatalogTreeImplementation));
            return row;
        }

        private void BindSampleTreeItem(VisualElement element, SampleTreeNode node)
        {
            var title = element.ElementAt(0) as UiTextElement;
            var meta = element.ElementAt(1) as UiTextElement;

            if (title != null)
            {
                title.SetText(node.Title);
            }

            if (meta != null)
            {
                meta.SetText(node.Meta);
                meta.EnableInClassList("ee4v-ui-catalog-tree-item__implementation--hidden", string.IsNullOrWhiteSpace(node.Meta));
            }
        }

        private static IReadOnlyList<SearchableTreeItemData<SampleTreeNode>> BuildSampleTreeItems(
            string searchFieldMeta = "Input",
            string searchableTreeViewMeta = "Tree")
        {
            return new[]
            {
                new SearchableTreeItemData<SampleTreeNode>(
                    1,
                    new SampleTreeNode("Display", string.Empty),
                    "Display",
                    new[]
                    {
                        new SearchableTreeItemData<SampleTreeNode>(
                            2,
                            new SampleTreeNode("InfoCard", "Card"),
                            "InfoCard Card information"),
                        new SearchableTreeItemData<SampleTreeNode>(
                            3,
                            new SampleTreeNode("Alerts", "Banner"),
                            "Alerts Banner feedback"),
                        new SearchableTreeItemData<SampleTreeNode>(
                            4,
                            new SampleTreeNode("StatusBadge", "Pill"),
                            "StatusBadge pill status"),
                        new SearchableTreeItemData<SampleTreeNode>(
                            5,
                            new SampleTreeNode("Icon", "Image"),
                            "Icon image texture builtin")
                    }),
                new SearchableTreeItemData<SampleTreeNode>(
                    6,
                    new SampleTreeNode("DataView", string.Empty),
                    "DataView",
                    new[]
                    {
                        new SearchableTreeItemData<SampleTreeNode>(
                            7,
                            new SampleTreeNode("SearchableTreeView", searchableTreeViewMeta),
                            "SearchableTreeView searchable tree")
                    }),
                new SearchableTreeItemData<SampleTreeNode>(
                    8,
                    new SampleTreeNode("Interactive", string.Empty),
                    "Interactive",
                    new[]
                    {
                        new SearchableTreeItemData<SampleTreeNode>(
                            9,
                            new SampleTreeNode("SearchField", searchFieldMeta),
                            "SearchField input search"),
                        new SearchableTreeItemData<SampleTreeNode>(
                            10,
                            new SampleTreeNode("TabCard", "Tabs"),
                            "TabCard Tabs switcher")
                    }),
                new SearchableTreeItemData<SampleTreeNode>(
                    11,
                    new SampleTreeNode("Overlay", string.Empty),
                    "Overlay",
                    new[]
                    {
                        new SearchableTreeItemData<SampleTreeNode>(
                            12,
                            new SampleTreeNode("WindowToast", "Toast"),
                            "WindowToast editor window overlay toast")
                    }),
                new SearchableTreeItemData<SampleTreeNode>(
                    13,
                    new SampleTreeNode("Domain", string.Empty),
                    "Domain",
                    new[]
                    {
                        new SearchableTreeItemData<SampleTreeNode>(
                            14,
                            new SampleTreeNode("Testing", string.Empty),
                            "Testing",
                            new[]
                            {
                                new SearchableTreeItemData<SampleTreeNode>(
                                    15,
                                    new SampleTreeNode("TestResultGroup", "Testing"),
                                    "TestResultGroup testing domain result")
                            })
                    })
            };
        }

        private sealed class SampleTreeNode
        {
            public SampleTreeNode(string title, string meta)
            {
                Title = title ?? string.Empty;
                Meta = meta ?? string.Empty;
            }

            public string Title { get; }

            public string Meta { get; }
        }
    }
}
