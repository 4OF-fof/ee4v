using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ErrorScreenCatalogRegistrar :
            ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Content/" +
                    "ErrorScreen/error-screen.uss");
                registry.RegisterStory(new StoryRegistration(
                    "error-screen",
                    "Content",
                    "ErrorScreen",
                    "画面全体の進行中、空状態、エラーを中央のアイコンとメッセージで表示するコンポーネントです。",
                    "一覧を隠して状態だけを伝える場面で使い、文字だけの空画面を残しません。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildErrorScreenStory(parent)));
            }
        }

        private void BuildErrorScreenStory(
            VisualElement parent)
        {
            var kind = ErrorScreenKind.Error;
            var message = "アセットを読み込めませんでした。";
            Action refresh = null;
            Action<ErrorScreenKind> applyPreset = selectedKind =>
            {
                kind = selectedKind;
                switch (selectedKind)
                {
                    case ErrorScreenKind.Info:
                        message = "表示できるアセットがありません。";
                        break;
                    case ErrorScreenKind.Loading:
                        message = "アセットを読み込み中...";
                        break;
                    default:
                        message = "アセットを読み込めませんでした。";
                        break;
                }

                refresh?.Invoke();
            };

            var controls = CreateTabbedControlsSection(
                parent,
                "状態ごとのアイコンとメッセージを確認します。");
            var messageField = AddTextField(
                controls.Content,
                "メッセージ",
                message,
                value =>
                {
                    message = value;
                    refresh();
                },
                true);

            var preview = CreatePreviewSection(parent);
            var screen = new ErrorScreen();
            var surface = CreatePreviewSurface();
            surface.style.height = 300f;
            surface.Add(screen);
            preview.Body.Add(surface);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(
                                ErrorScreenKind.Info.ToString(),
                                "Empty"),
                            new TabCardTabState(
                                ErrorScreenKind.Loading.ToString(),
                                "Loading"),
                            new TabCardTabState(
                                ErrorScreenKind.Error.ToString(),
                                "Error")
                        },
                        kind.ToString()),
                    id => applyPreset(
                        (ErrorScreenKind)Enum.Parse(
                            typeof(ErrorScreenKind),
                            id)));
                messageField.SetValueWithoutNotify(message);
                screen.SetState(
                    new ErrorScreenState(message, kind));
            };

            applyPreset(kind);
            FinalizeControlsSection(parent, controls);
        }
    }
}
