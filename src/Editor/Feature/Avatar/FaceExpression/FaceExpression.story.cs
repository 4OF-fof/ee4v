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
                    "左右ジェスチャー64通りをサムネイル付きセルで設定する画面です。",
                    "グリッドと設定画面が一つのセッションを共有し、Modular Avatar用データを生成します。",
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
                    "編集対象メッシュと区切り用BlendShapeのグループを表示します。",
                    "メッシュ追加とグループ選択を表情エディターへ反映します。",
                    BuildGroups,
                    dependencies: new[] { "UiButton", "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionGroupWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Inputs/ui-button.uss",
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
            view.SetChannels(
                new[]
                {
                    CreateStoryChannel("eye_blink_1", 80f, true),
                    CreateStoryChannel("eye_blink_1_L", 0f, false),
                    CreateStoryChannel("eye_blink_1_R", 0f, false),
                    CreateStoryChannel("eye_blink_2", 100f, true),
                    CreateStoryChannel("eye_blink_2_L", 0f, false),
                    CreateStoryChannel("eye_blink_2_R", 0f, false),
                    CreateStoryChannel("eye_smile_1", 30f, true)
                },
                "Eye",
                CreateStoryRule(),
                true);
            var surface = new VisualElement();
            surface.style.width = 920f;
            surface.style.height = 640f;
            surface.Add(view);
            parent.Add(surface);
        }

        private static BlendShapeChannel CreateStoryChannel(
            string name,
            float value,
            bool animated)
        {
            return new BlendShapeChannel(
                "Body",
                name,
                value,
                animated,
                sourceAssetGuid: "story-fbx",
                sourceMeshLocalId: 1L);
        }

        private static BlendShapeNamingRule CreateStoryRule()
        {
            var names = new[]
            {
                "eye_blink_1",
                "eye_blink_1_L",
                "eye_blink_1_R",
                "eye_blink_2",
                "eye_blink_2_L",
                "eye_blink_2_R",
                "eye_smile_1"
            };
            var preset = new BlendShapeFbxPreset
            {
                assetGuid = "story-fbx",
                name = "Story"
            };
            for (var index = 0; index < names.Length; index++)
            {
                preset.mappings.Add(BlendShapeNameClassifier.Classify(
                    1L,
                    "Body",
                    names[index]));
            }

            var state = new BlendShapeNamePresetState();
            state.presets.Add(preset);
            return new BlendShapeNamingRule(state);
        }

        private static void BuildAssignments(VisualElement parent)
        {
            var view = new GestureAssignmentView(
                new GestureAssignmentViewText
                {
                    Avatar = "Avatar",
                    Apply = "Apply to avatar",
                    LeftHand = "Left hand",
                    RightHand = "Right hand",
                    Selection = "Selected",
                    Clip = "Expression clip",
                    EnableBlink = "Enable blinking",
                    FixMouth = "Fix mouth movement",
                    Unassigned = "Unassigned",
                    MenuOnly = "Extra (menu only)",
                    MenuOnlyHint = "Select an Extra cell below the divider to edit it in the settings window.",
                    MenuName = "Menu name",
                    AddMenuExpression = "Add menu-only expression",
                    Remove = "Remove",
                    GestureName = gesture => gesture.ToString()
                });
            view.SetConfiguration(new FaceExpressionConfiguration(
                new Dictionary<GestureCombination, FaceExpressionAssignment>(),
                new[]
                {
                    new FaceExpressionMenuEntry(
                        "Menu expression",
                        FaceExpressionAssignment.Default)
                }));
            var surface = new VisualElement();
            surface.style.width = 1200f;
            surface.style.height = 640f;
            surface.Add(view);
            parent.Add(surface);

            var settingsSurface = new VisualElement();
            settingsSurface.style.width = 420f;
            settingsSurface.style.height = 150f;
            settingsSurface.Add(new GestureAssignmentSettingsView(
                new GestureAssignmentViewText
                {
                    Selection = "Selected",
                    EnableBlink = "Enable blinking",
                    FixMouth = "Fix mouth movement",
                    MenuOnly = "Extra (menu only)",
                    MenuName = "Menu name",
                    Remove = "Remove",
                    GestureName = gesture => gesture.ToString()
                }));
            parent.Add(settingsSurface);
        }

        private static void BuildGroups(VisualElement parent)
        {
            var eyes = new BlendShapeGroup("Eyes");
            eyes.AddShape();
            eyes.AddShape();
            var mouth = new BlendShapeGroup("Mouth");
            mouth.AddShape();
            var accessory = new BlendShapeGroup("Face/Eyes", "Face/Eyes");
            accessory.AddShape();
            var view = new FaceExpressionGroupView(
                new FaceExpressionGroupViewText
                {
                    Groups = "Group",
                    All = "All",
                    AddMesh = "Add mesh",
                    RemoveMesh = "Remove",
                    MeshGroupSection = "Added meshes",
                    BodySection = "Body"
                });
            view.SetGroups(
                new[] { accessory, eyes, mouth },
                4,
                eyes.Key);
            var surface = new VisualElement();
            surface.style.width = 260f;
            surface.style.height = 480f;
            surface.Add(view);
            parent.Add(surface);
        }
    }
}
