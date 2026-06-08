using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class InfoCardCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Display/InfoCard/info-card.uss");
                registry.RegisterStory(new StoryRegistration(
                    "info-card",
                    "Display",
                    "InfoCard",
                    "タイトル、説明、eyebrow、badge、body を組み合わせて情報面を構成する基本コンポーネントです。",
                    "シンプルな情報表示から、結果一覧の見出し付きカードまで幅広く使う土台です。header の各値が欠けても自然に見えるように余白を調整し、内蔵の badge と本文を組み合わせて情報密度を調整できます。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildInfoCardStory(parent)));
            }
        }

        private void BuildInfoCardStory(VisualElement parent)
        {
            var preset = InfoCardStoryPreset.Simple;
            var eyebrow = string.Empty;
            var title = "Feature Test Manager";
            var description = string.Empty;
            var badgeText = string.Empty;
            var bodyText = "カードは単体の情報表示面や、設定グループの土台として使えます。";
            Action refresh = null;

            Action<InfoCardStoryPreset> applyPreset = selectedPreset =>
            {
                preset = selectedPreset;
                switch (selectedPreset)
                {
                    case InfoCardStoryPreset.Result:
                        eyebrow = "I18N";
                        title = "解析結果";
                        description = "件数付きの結果カードとして使う用途を想定した preset です。";
                        badgeText = "12";
                        bodyText = "不足キー 8 件\n未参照エントリ 4 件";
                        break;
                    default:
                        eyebrow = string.Empty;
                        title = "Feature Test Manager";
                        description = string.Empty;
                        badgeText = string.Empty;
                        bodyText = "カードは単体の情報表示面や、設定グループの土台として使えます。";
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "InfoCard の各プロパティを編集し、値の有無ごとの見た目を確認します。");

            var eyebrowField = AddTextField(controls.Content, "Eyebrow", eyebrow, value =>
            {
                eyebrow = value;
                refresh();
            });
            var titleField = AddTextField(controls.Content, "タイトル（必須）", title, value =>
            {
                title = value;
                refresh();
            });
            var descriptionField = AddTextField(controls.Content, "説明", description, value =>
            {
                description = value;
                refresh();
            });
            var badgeField = AddTextField(controls.Content, "バッジ", badgeText, value =>
            {
                badgeText = value;
                refresh();
            });
            var bodyTextField = AddTextField(controls.Content, "本文テキスト", bodyText, value =>
            {
                bodyText = value;
                refresh();
            }, true, 140f);

            var preview = CreatePreviewSection(parent);
            var card = new InfoCard();
            preview.Body.Add(card);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(InfoCardStoryPreset.Simple.ToString(), "Simple"),
                            new TabCardTabState(InfoCardStoryPreset.Result.ToString(), "Result")
                        },
                        preset.ToString()),
                    id => applyPreset((InfoCardStoryPreset)Enum.Parse(typeof(InfoCardStoryPreset), id)));

                eyebrowField.SetValueWithoutNotify(eyebrow);
                titleField.SetValueWithoutNotify(title);
                descriptionField.SetValueWithoutNotify(description);
                badgeField.SetValueWithoutNotify(badgeText);
                bodyTextField.SetValueWithoutNotify(bodyText);

                card.SetState(new InfoCardState(title, description, eyebrow, badgeText));
                card.Body.Clear();

                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    var bodyLabel = UiTextFactory.Create(bodyText);
                    bodyLabel.SetWhiteSpace(WhiteSpace.Normal);
                    card.Body.Add(bodyLabel);
                }
            };

            applyPreset(preset);
            FinalizeControlsSection(parent, controls);
        }
    }
}
