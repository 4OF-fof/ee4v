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
                    "検索入力と clear 操作をまとめた単体利用向けの検索コンポーネントです。",
                    "一覧やカード列の絞り込みに使う軽量な検索入力です。placeholder と clear button を持ち、SearchableTreeView の検索 UI と同じ見た目・挙動を単体でも使えます。",
                    new[]
                    {
                        "Icon"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildSearchFieldStory(parent)));
            }
        }

        private void BuildSearchFieldStory(VisualElement parent)
        {
            var value = string.Empty;
            var placeholder = "suite 名、説明、テスト名で検索";
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

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            var searchField = new SearchField();
            surface.Add(searchField);
            preview.Body.Add(surface);

            refresh = () =>
            {
                valueField.SetValueWithoutNotify(value);
                placeholderField.SetValueWithoutNotify(placeholder);
                searchField.SetState(new SearchFieldState(
                    value,
                    placeholder,
                    I18N.Get("ui.search.tooltip"),
                    I18N.Get("ui.clear.tooltip")));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
