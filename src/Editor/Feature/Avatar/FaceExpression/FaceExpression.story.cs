using System.Collections.Generic;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionStoryProvider : IUiStoryProvider
    {
        public int Order => 180;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "face-expression-editor",
                    "Domain/FaceExpression",
                    "Face Expression Editor",
                    "BlendShape 表情の作成とプレビューを行う画面です。",
                    "プレビュー用アバターを保持し、BlendShape 値だけを更新します。",
                    Build,
                    dependencies: new[]
                    {
                        "SearchField",
                        "UiButton",
                        "Icon",
                        "UiTextFactory",
                        "Fluent UI System Icons"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Inputs/ui-button.uss",
                        "Editor/UI/Components/Content/Icon/icon.uss",
                        "Editor/UI/Components/Inputs/SearchField/search-field.uss",
                        "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss"
                    }),
                new UiStory(
                    "gesture-assignment",
                    "Domain/FaceExpression",
                    "Gesture Assignments",
                    "表情クリップを標準ハンドジェスチャーへ割り当てる画面です。",
                    "FaceExpressionWindow から独立したウィンドウで表示します。",
                    BuildAssignments,
                    dependencies: new[] { "UiButton", "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Inputs/ui-button.uss",
                        "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss"
                    }),
                new UiStory(
                    "face-expression-groups",
                    "Domain/FaceExpression",
                    "Expression Groups",
                    "区切り用BlendShapeから作成したグループと件数を表示します。",
                    "選択したグループを表情エディターの一覧へ反映します。",
                    BuildGroups,
                    dependencies: new[] { "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionGroupWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var view = new FaceExpressionView(
                new FaceExpressionViewText
                {
                    Avatar = "Avatar",
                    Clip = "Expression clip",
                    NewClip = "New expression",
                    ResetView = "Reset view",
                    SearchPlaceholder = "Search BlendShapes",
                    SearchTooltip = "Filter BlendShapes by name",
                    ClearSearchTooltip = "Clear search",
                    BlendShapes = "BlendShapes",
                    ClipOnly = "In clip only",
                    ClipOnlyTooltip = "Show only BlendShapes stored in the clip",
                    NoBlendShapes = "No BlendShapes were found.",
                    ClipRequired = "Create or select an expression clip first."
                },
                rect => EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f)));
            view.SetChannels(new[]
            {
                new BlendShapeChannel(
                    "Body",
                    "----------EYE----------",
                    0f,
                    false,
                    "EYE"),
                new BlendShapeChannel("Body", "Smile", 80f, true),
                new BlendShapeChannel("Body", "Blink", 0f, false),
                new BlendShapeChannel("Body", "Angry", 100f, true)
            });
            var surface = new VisualElement();
            surface.style.width = 920f;
            surface.style.height = 640f;
            surface.Add(view);
            parent.Add(surface);
        }

        private static void BuildAssignments(VisualElement parent)
        {
            var view = new GestureAssignmentView(
                new GestureAssignmentViewText
                {
                    Avatar = "Avatar",
                    Hint = "The left hand takes priority when both hands use assigned gestures.",
                    Apply = "Apply to avatar",
                    GestureName = gesture => gesture.ToString()
                });
            view.SetAssignments(new Dictionary<FaceGesture, AnimationClip>());
            var surface = new VisualElement();
            surface.style.width = 520f;
            surface.style.height = 340f;
            surface.Add(view);
            parent.Add(surface);
        }

        private static void BuildGroups(VisualElement parent)
        {
            var eyes = new BlendShapeGroup("Eyes");
            eyes.AddShape();
            eyes.AddShape();
            var mouth = new BlendShapeGroup("Mouth");
            mouth.AddShape();
            var view = new FaceExpressionGroupView(
                new FaceExpressionGroupViewText
                {
                    Groups = "Group",
                    All = "All"
                });
            view.SetGroups(
                new[] { eyes, mouth },
                3,
                "Eyes");
            var surface = new VisualElement();
            surface.style.width = 260f;
            surface.style.height = 480f;
            surface.Add(view);
            parent.Add(surface);
        }
    }
}
