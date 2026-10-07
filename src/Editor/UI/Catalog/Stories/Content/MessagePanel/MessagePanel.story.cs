using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class MessagePanelCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 15;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "message-panel", "Displays", "MessagePanel",
                    "エラー・警告・案内を見出し、説明、対象一覧へ分けて表示します。",
                    "共通の色・文字・余白とFluentアイコンを使用し、多数の対象は一覧内でスクロールします。通知内へ操作ボタンを配置できます。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildMessagePanelStory(parent),
                    new[] { "Editor/UI/Components/Overlays/ConfirmationOverlay/ConfirmationOverlay.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/ExpressionConversionView.cs",
                        "Editor/Feature/Avatar/AvatarInfo/UI/AvatarInfoView.cs" }));
            }
        }

        private void BuildMessagePanelStory(VisualElement parent)
        {
            var title = "表情クリップの確認事項（6件）";
            var message = "対象のRendererまたはBlendShapeがアバターにありません。";
            var severity = MessageSeverity.Error;
            var showDetails = true;
            var manyDetails = false;
            var visible = true;
            var showAction = true;
            Action refresh = null;
            var controls = CreatePlainControlsSection(parent,
                "種類、文章、対象件数と空状態を切り替えて表示を確認します。");
            AddTextField(controls.Content, "見出し", title, value => { title = value; refresh(); });
            AddTextField(controls.Content, "説明", message, value => { message = value; refresh(); });
            AddEnumField(controls.Content, "種類", severity, value => { severity = value; refresh(); });
            AddToggle(controls.Content, "対象一覧を表示", showDetails,
                value => { showDetails = value; refresh(); });
            AddToggle(controls.Content, "多数の対象", manyDetails,
                value => { manyDetails = value; refresh(); });
            AddToggle(controls.Content, "表示", visible, value => { visible = value; refresh(); });
            AddToggle(controls.Content, "操作ボタン", showAction, value => { showAction = value; refresh(); });
            var preview = CreatePreviewSection(parent);
            var panel = new MessagePanel();
            var action = new UiButton("対応付けを設定");
            action.style.marginLeft = UiSpacingTokens.Large;
            panel.DetailsActions.Add(action);
            preview.Body.Add(panel);
            var targets = new[]
            {
                "Body / eye_joy", "Body / eyebrow_joy", "Body / mouth_a1",
                "Body / other_cheek_2", "Body / eye_close_L", "Body / eye_happy_R"
            };
            refresh = () =>
            {
                action.style.display = showAction ? DisplayStyle.Flex : DisplayStyle.None;
                panel.DetailsActions.style.display = showAction ? DisplayStyle.Flex : DisplayStyle.None;
                panel.SetState(visible ? new MessagePanelState(title, message, severity,
                showDetails ? manyDetails
                    ? Enumerable.Range(0, 30).Select(index => "Avatar/Face/AdditionalMesh_" + index + " / BlendShape_" + index).ToArray()
                    : targets
                    : Array.Empty<string>()) : null);
            };
            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
