using System;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class WindowToastCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Display/WindowToast/window-toast.uss");
                registry.RegisterStory(new StoryRegistration(
                    "window-toast",
                    "Overlay",
                    "WindowToast",
                    "ee4v 自前 EditorWindow に後付けできる、右上スタック型の toast 通知基盤です。",
                    "window root に absolute overlay host を追加し、info/success/warning/error の通知を縦に積みます。action button を持つ toast も同じ面の中で扱えます。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildWindowToastStory(parent)));
            }
        }

        private void BuildWindowToastStory(VisualElement parent)
        {
            var preset = WindowToastStoryPreset.Info;
            var tone = WindowToastTone.Info;
            var title = string.Empty;
            var message = string.Empty;
            var durationSeconds = 4d;
            var dismissible = true;
            var hasAction = false;
            Action refresh = null;

            Action<WindowToastStoryPreset> applyPreset = selectedPreset =>
            {
                preset = selectedPreset;
                ApplyCatalogToastPreset(selectedPreset, out tone, out title, out message, out durationSeconds, out dismissible, out hasAction);
                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "preset で tone と文面を切り替えながら、Catalog window 右上に積まれる toast を確認します。");
            var toneField = AddEnumField(controls.Content, "Tone", tone, value =>
            {
                applyPreset((WindowToastStoryPreset)(int)value);
            });
            var titleField = AddTextField(controls.Content, "Title", title, value =>
            {
                title = value;
                refresh();
            });
            var messageField = AddTextField(controls.Content, "Message", message, value =>
            {
                message = value;
                refresh();
            }, true);
            var durationField = new FloatField("Duration")
            {
                value = (float)durationSeconds
            };
            durationField.RegisterValueChangedCallback(evt =>
            {
                durationSeconds = Math.Max(0d, evt.newValue);
                refresh();
            });
            controls.Content.Add(durationField);

            var dismissibleToggle = new Toggle("Dismissible")
            {
                value = dismissible
            };
            dismissibleToggle.RegisterValueChangedCallback(evt =>
            {
                dismissible = evt.newValue;
                refresh();
            });
            controls.Content.Add(dismissibleToggle);

            var actionToggle = new Toggle("Action")
            {
                value = hasAction
            };
            actionToggle.RegisterValueChangedCallback(evt =>
            {
                hasAction = evt.newValue;
                refresh();
            });
            controls.Content.Add(actionToggle);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            controls.Content.Add(buttonRow);

            var pushButton = new Button(() =>
            {
                WindowToastApi.Show(this, CreateCatalogToastRequest(tone, title, message, durationSeconds, dismissible, hasAction));
            })
            {
                text = "Push"
            };
            buttonRow.Add(pushButton);

            var clearButton = new Button(() => WindowToastApi.Clear(this))
            {
                text = "Clear"
            };
            clearButton.style.marginLeft = 6f;
            buttonRow.Add(clearButton);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(WindowToastStoryPreset.Info.ToString(), "Info"),
                            new TabCardTabState(WindowToastStoryPreset.Success.ToString(), "Success"),
                            new TabCardTabState(WindowToastStoryPreset.Warning.ToString(), "Warning"),
                            new TabCardTabState(WindowToastStoryPreset.Error.ToString(), "Error")
                        },
                        preset.ToString()),
                    id => applyPreset((WindowToastStoryPreset)Enum.Parse(typeof(WindowToastStoryPreset), id)));
                toneField.SetValueWithoutNotify((Enum)(object)tone);
                titleField.SetValueWithoutNotify(title);
                messageField.SetValueWithoutNotify(message);
                durationField.SetValueWithoutNotify((float)durationSeconds);
                dismissibleToggle.SetValueWithoutNotify(dismissible);
                actionToggle.SetValueWithoutNotify(hasAction);
            };

            applyPreset(preset);
            FinalizeControlsSection(parent, controls);
        }

        private static WindowToastRequest CreateCatalogToastRequest(
            WindowToastTone tone,
            string title,
            string message,
            double durationSeconds,
            bool dismissible,
            bool hasAction)
        {
            return new WindowToastRequest(
                tone,
                FormatCatalogToastTitle(title),
                message,
                durationSeconds: durationSeconds,
                dismissible: dismissible,
                actions: hasAction
                    ? new[]
                    {
                        new WindowToastAction("Open", closesToast: true)
                    }
                    : Array.Empty<WindowToastAction>());
        }

        private static void ApplyCatalogToastPreset(
            WindowToastStoryPreset preset,
            out WindowToastTone tone,
            out string title,
            out string message,
            out double durationSeconds,
            out bool dismissible,
            out bool hasAction)
        {
            dismissible = true;
            hasAction = false;

            switch (preset)
            {
                case WindowToastStoryPreset.Success:
                    tone = WindowToastTone.Success;
                    title = "Catalog Sync Completed";
                    message = "UI Catalog の story metadata 更新が反映されました。";
                    durationSeconds = 3d;
                    return;
                case WindowToastStoryPreset.Warning:
                    tone = WindowToastTone.Warning;
                    title = "Preview Requires Refresh";
                    message = "現在の変更を反映するには Catalog window の再描画が必要です。";
                    durationSeconds = 0d;
                    hasAction = true;
                    return;
                case WindowToastStoryPreset.Error:
                    tone = WindowToastTone.Error;
                    title = "Feature Test Launch Failed";
                    message = "Core の test run を開始できませんでした。詳細ログを確認してください。";
                    durationSeconds = 0d;
                    return;
                default:
                    tone = WindowToastTone.Info;
                    title = "Overlay Preview Active";
                    message = "Catalog window 自体に toast overlay を表示して挙動を確認します。";
                    durationSeconds = 4d;
                    return;
            }
        }
    }
}
