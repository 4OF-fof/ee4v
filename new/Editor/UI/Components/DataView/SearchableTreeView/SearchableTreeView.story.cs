using System;
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
                registry.RegisterStyleSheet("Editor/UI/Components/DataView/SearchableTreeView/searchable-tree-view.uss");
                registry.RegisterStory(new StoryRegistration(
                    "searchable-tree-view",
                    "DataView",
                    "SearchableTreeView",
                    "検索窓と tree view をまとめて提供する、絞り込み可能なツリーコンポーネントです。",
                    "呼び出し側は階層データと row 描画だけを渡し、検索文字列の状態管理や tree の絞り込みは component 側に任せます。検索欄は SearchField を内部利用し、tree 本体と同じ面の中で扱います。各 row 右側の短い文字列は component が自動生成するものではなく、bindItem で描画する row data 側の meta 表示です。",
                    new[]
                    {
                        "SearchField"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildSearchableTreeViewStory(parent)));
            }
        }

        private void BuildSearchableTreeViewStory(VisualElement parent)
        {
            var searchableTreeViewMeta = "Tree";
            Action refresh = null;

            var controls = CreatePlainControlsSection(
                parent,
                "行右側の短い文字列は SearchableTreeView 固有の列ではなく、Catalog story では bindItem が SampleTreeNode.Meta を描画しています。");
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
                "一致する項目がありません。");
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
