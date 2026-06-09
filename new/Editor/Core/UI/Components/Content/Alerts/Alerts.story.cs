using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class AlertsCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Content/Alerts/alerts.uss");
                registry.RegisterStory(new StoryRegistration(
                    "alerts",
                    "Content",
                    "Alerts",
                    "情報、警告、エラーの tone を切り替えてメッセージを表示する通知コンポーネントです。",
                    "非ブロッキングな案内からエラー通知までを同じ構造で扱います。タイトルとメッセージの両方を持てるので、短い要約と補足説明を分けて表示できます。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildAlertsStory(parent)));
            }
        }

        private void BuildAlertsStory(VisualElement parent)
        {
            var tone = UiBannerTone.Info;
            var title = "情報表示";
            var message = "非ブロッキングな案内やエラー通知に使います。";
            Action refresh = null;
            Action<UiBannerTone> applyPreset = selectedTone =>
            {
                tone = selectedTone;
                switch (selectedTone)
                {
                    case UiBannerTone.Warning:
                        title = "警告表示";
                        message = "確認が必要な状態や注意喚起に使います。";
                        break;
                    case UiBannerTone.Error:
                        title = "エラー表示";
                        message = "処理失敗や設定不備など、強く伝える必要がある状態に使います。";
                        break;
                    default:
                        title = "情報表示";
                        message = "非ブロッキングな案内やエラー通知に使います。";
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "タイトル、メッセージ、tone を切り替えて通知の見た目を確認します。");

            var toneField = AddEnumField(controls.Content, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });
            var titleField = AddTextField(controls.Content, "タイトル", title, value =>
            {
                title = value;
                refresh();
            });
            var messageField = AddTextField(controls.Content, "メッセージ", message, value =>
            {
                message = value;
                refresh();
            }, true);

            var preview = CreatePreviewSection(parent);
            var alerts = new Alerts();
            preview.Body.Add(CreatePreviewSurface(alerts, true));

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(UiBannerTone.Info.ToString(), "Info"),
                            new TabCardTabState(UiBannerTone.Warning.ToString(), "Warning"),
                            new TabCardTabState(UiBannerTone.Error.ToString(), "Error")
                        },
                        tone.ToString()),
                    id => applyPreset((UiBannerTone)Enum.Parse(typeof(UiBannerTone), id)));

                toneField.SetValueWithoutNotify((Enum)(object)tone);
                titleField.SetValueWithoutNotify(title);
                messageField.SetValueWithoutNotify(message);
                alerts.SetState(new AlertsState(tone, title, message));
            };

            applyPreset(tone);
            FinalizeControlsSection(parent, controls);
        }
    }
}
