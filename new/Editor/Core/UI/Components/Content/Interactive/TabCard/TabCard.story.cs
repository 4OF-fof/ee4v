using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class TabCardCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Content/Interactive/TabCard/tab-card.uss");
                registry.RegisterStory(new StoryRegistration(
                    "tab-card",
                    "Content/Interactive",
                    "TabCard",
                    "左上のタブ列で内容を切り替える box コンポーネントです。",
                    "ブラウザのタブのように、上部タブを切り替えながら下部 panel の内容を差し替える用途を想定しています。content slot には任意の UI 要素を配置できます。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildTabCardStory(parent)));
            }
        }

        private void BuildTabCardStory(VisualElement parent)
        {
            var firstLabel = "基本";
            var secondLabel = "詳細";
            var thirdLabel = "空状態";
            var selectedTabId = "basic";
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "タブ名と、選択中タブで表示する内容を編集します。");
            AddTextField(controls.Content, "タブ1", firstLabel, value =>
            {
                firstLabel = value;
                refresh();
            });
            AddTextField(controls.Content, "タブ2", secondLabel, value =>
            {
                secondLabel = value;
                refresh();
            });
            AddTextField(controls.Content, "タブ3", thirdLabel, value =>
            {
                thirdLabel = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var tabCard = new TabCard();
            preview.Body.Add(tabCard);

            refresh = () =>
            {
                tabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState("basic", firstLabel),
                            new TabCardTabState("detail", secondLabel),
                            new TabCardTabState("empty", thirdLabel)
                        },
                        selectedTabId),
                    id =>
                    {
                        selectedTabId = id;
                        refresh();
                    });

                tabCard.Content.Clear();
                var previewCard = new InfoCard(new InfoCardState(
                    selectedTabId == "basic" ? "基本表示" : selectedTabId == "detail" ? "詳細表示" : "空状態表示",
                    selectedTabId == "basic"
                        ? "タブ切り替え後の content slot に任意の UI を配置できます。"
                        : selectedTabId == "detail"
                            ? "複数のフォーム、説明文、ステータスなどを任意に構成できます。"
                            : "コンポーネント未選択時やデータ空状態の panel としても使えます。",
                    null,
                    selectedTabId.ToUpperInvariant()));
                previewCard.AddToClassList("ee4v-ui-catalog-preview-card--flush");
                tabCard.Content.Add(previewCard);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
