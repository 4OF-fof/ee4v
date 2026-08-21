using System;
using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class SearchableTreeViewCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Collections/SearchableTreeView/searchable-tree-view.uss");
                registry.RegisterStory(new StoryRegistration(
                    "searchable-tree-view",
                    "Collections",
                    "SearchableTreeView",
                    "検索窓と tree view をまとめて提供する、絞り込み可能なツリーコンポーネントです。",
                    "検索欄とTreeViewを一つの面にまとめ、階層データの絞り込みと展開状態の維持を行います。各行はbindItemで任意の表示へ構成できます。",
                    new[]
                    {
                        "SearchField"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildSearchableTreeViewStory(parent),
                    new[]
                    {
                        "Editor/UI/Catalog/CatalogWindow.cs",
                        "Editor/AssetManager/UI/SearchableFileTree.cs"
                    }));
            }
        }

        private void BuildSearchableTreeViewStory(VisualElement parent)
        {
            var searchableTreeViewMeta = "Tree";
            Action refresh = null;

            var controls = CreatePlainControlsSection(
                parent,
                "行右側の文字列は、bindItemがSampleTreeNode.Metaを表示する例です。");
            var searchableTreeViewMetaField = AddTextField(controls.Content, "SampleTreeNode.Meta (SearchableTreeView)", searchableTreeViewMeta, value =>
            {
                searchableTreeViewMeta = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var treeView = new SearchableTreeView<SampleTreeNode>(
                CreateSampleTreeItem,
                BindSampleTreeItem,
                null,
                "一致する項目がありません。",
                I18N.Get("ui.search.placeholder"),
                searchTooltip: I18N.Get("ui.search.tooltip"),
                clearTooltip: I18N.Get("ui.clear.tooltip"));
            treeView.SetViewDataKey("ee4v-ui-catalog-searchable-tree-view-story");
            preview.Body.Add(treeView);

            refresh = () =>
            {
                searchableTreeViewMetaField.SetValueWithoutNotify(searchableTreeViewMeta);
                treeView.SetItems(BuildSampleTreeItems("Input", searchableTreeViewMeta));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
