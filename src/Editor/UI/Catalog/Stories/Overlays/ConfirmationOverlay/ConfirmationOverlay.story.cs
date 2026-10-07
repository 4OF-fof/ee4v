using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ConfirmationOverlayCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 11;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "confirmation-overlay", "Displays", "Confirmation Overlay",
                    "背景の操作を止め、設定を破棄するか確認する通知を表示します。",
                    "中立色の本文と区切り線のある操作欄を共通化します。左端に赤い破棄ボタンを置き、取消とEscで元の画面へ戻ります。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildConfirmationOverlayStory(parent),
                    new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs",
                        "Editor/Feature/Avatar/ExpressionMenu/ExpressionMenuTemplateEditor.cs",
                        "Editor/Feature/Avatar/ExpressionMenu/ExpressionMenuView.cs"
                    }));
            }
        }

        private void BuildConfirmationOverlayStory(VisualElement parent)
        {
            var controls = CreatePlainControlsSection(parent, "確認を表示し、破棄・取消・Escを確認します。");
            controls.Content.Add(new UiButton("確認を表示", () =>
            {
                var overlay = new ConfirmationOverlay(rootVisualElement,
                    new MessagePanelState("操作タイプを変更しますか？",
                        "初期値とすべての動作設定を破棄します。名前とアイコンは保持されます。", MessageSeverity.Warning));
                overlay.AddDiscardAction("破棄して変更", overlay.Close);
                overlay.Notification.Actions.Add(new UiButton("編集を続ける", overlay.Close));
            }));
            FinalizeControlsSection(parent, controls);
        }
    }
}
