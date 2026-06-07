using System;
using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class StatusBadgeCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Display/StatusBadge/status-badge.uss");
                registry.RegisterStory(new StoryRegistration(
                    "status-badge",
                    "Display",
                    "StatusBadge",
                    "短い状態テキストを pill 形で表示するステータス表示コンポーネントです。",
                    "カード header や一覧の補助情報に載せる小さな状態表示です。長めのテキストでも楕円に潰れず、pill 形を維持する前提で調整しています。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildStatusBadgeStory(parent)));
            }
        }

        private void BuildStatusBadgeStory(VisualElement parent)
        {
            var text = I18N.Get("catalog.status.running");
            var tone = UiStatusTone.Running;
            Action refresh = null;
            Action<UiStatusTone> applyPreset = selectedTone =>
            {
                tone = selectedTone;
                switch (selectedTone)
                {
                    case UiStatusTone.Passed:
                        text = I18N.Get("catalog.status.passed");
                        break;
                    case UiStatusTone.Failed:
                        text = I18N.Get("catalog.status.failed");
                        break;
                    case UiStatusTone.Skipped:
                        text = I18N.Get("catalog.status.skipped");
                        break;
                    case UiStatusTone.Inconclusive:
                        text = I18N.Get("catalog.status.inconclusive");
                        break;
                    case UiStatusTone.Idle:
                        text = I18N.Get("catalog.status.idle");
                        break;
                    default:
                        text = I18N.Get("catalog.status.running");
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "状態テキストと tone を切り替えて badge の見た目を確認します。");

            var textField = AddTextField(controls.Content, "テキスト", text, value =>
            {
                text = value;
                refresh();
            });
            var toneField = AddEnumField(controls.Content, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var badge = new StatusBadge();
            var surface = CreatePreviewSurface(true);
            surface.Add(badge);
            preview.Body.Add(surface);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(UiStatusTone.Idle.ToString(), "Idle"),
                            new TabCardTabState(UiStatusTone.Running.ToString(), "Running"),
                            new TabCardTabState(UiStatusTone.Passed.ToString(), "Passed"),
                            new TabCardTabState(UiStatusTone.Failed.ToString(), "Failed"),
                            new TabCardTabState(UiStatusTone.Skipped.ToString(), "Skipped"),
                            new TabCardTabState(UiStatusTone.Inconclusive.ToString(), "Inconclusive")
                        },
                        tone.ToString()),
                    id => applyPreset((UiStatusTone)Enum.Parse(typeof(UiStatusTone), id)));

                textField.SetValueWithoutNotify(text);
                toneField.SetValueWithoutNotify((Enum)(object)tone);
                badge.SetState(new StatusBadgeState(text, tone));
            };

            applyPreset(tone);
            FinalizeControlsSection(parent, controls);
        }
    }
}
