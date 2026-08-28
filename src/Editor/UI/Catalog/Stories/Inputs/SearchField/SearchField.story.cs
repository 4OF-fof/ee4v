using System;
using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class SearchFieldCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Inputs/SearchField/search-field.uss");
                registry.RegisterStory(new StoryRegistration(
                    "search-field",
                    "Inputs",
                    "SearchField",
                    "薄い入力面へ検索入力と消去操作をまとめた検索コンポーネントです。",
                    "全周枠と検索アイコンで用途を示し、境界線とフォーカス色はInputFieldと共有します。先頭アイコンは任意の操作ボタンとしても使用できます。",
                    new[]
                    {
                        "Icon"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildSearchFieldStory(parent),
                    new[]
                    {
                        "Editor/UI/Components/Collections/SearchableTreeView/SearchableTreeView.cs",
                        "Editor/AssetManager/UI/AssetManagerControls.cs",
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/AssetManager/UI/AssetTagField.cs",
                        "Editor/AssetManager/UI/DerivedAssetPrefabPickerWindow.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectsToolbar.cs",
                        "Editor/Feature/Hierarchy/SceneSwitcher/UI/SceneSwitcherView.cs"
                    }));
            }
        }

        private void BuildSearchFieldStory(VisualElement parent)
        {
            var value = string.Empty;
            var placeholder = "suite 名、説明、テスト名で検索";
            var searchActionEnabled = false;
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "placeholder と入力値を変えながら、一覧絞り込み用の単体 search field を確認します。");
            var valueField = AddTextField(controls.Content, "値", value, nextValue =>
            {
                value = nextValue;
                refresh();
            });
            var placeholderField = AddTextField(controls.Content, "Placeholder", placeholder, nextValue =>
            {
                placeholder = nextValue;
                refresh();
            });
            var actionToggle = AddToggle(
                controls.Content,
                "先頭操作",
                searchActionEnabled,
                nextValue =>
                {
                    searchActionEnabled = nextValue;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewArea(true);
            var searchField = new SearchField();
            var actionStatus = UiTextFactory.Create(
                "先頭アイコンを押すとここへ表示します。",
                UiClassNames.SecondaryText);
            searchField.SearchActionRequested += () =>
                actionStatus.SetText("先頭操作を実行しました。");
            surface.Add(searchField);
            surface.Add(actionStatus);
            preview.Body.Add(surface);

            refresh = () =>
            {
                valueField.SetValueWithoutNotify(value);
                placeholderField.SetValueWithoutNotify(placeholder);
                actionToggle.SetValueWithoutNotify(searchActionEnabled);
                searchField.SetState(new SearchFieldState(
                    value,
                    placeholder,
                    I18N.Get("ui.search.tooltip"),
                    I18N.Get("ui.clear.tooltip"),
                    searchActionEnabled: searchActionEnabled));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
