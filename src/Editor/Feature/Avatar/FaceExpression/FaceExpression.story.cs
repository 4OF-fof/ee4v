using System;
using System.Collections.Generic;
using Ee4v.Core.Settings;
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
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentWindow.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentSettingsWindow.cs"
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
                    }),
                new UiStory(
                    "blend-shape-presets",
                    "Domain/FaceExpression",
                    "BlendShape Presets",
                    "FBX の BlendShape 名を役割、連番、左右へ分類する画面です。",
                    "実際の編集画面を、保存を行わないサンプル mapping で表示します。",
                    BuildPresets,
                    dependencies: new[]
                    {
                        "UiTextFactory",
                        "ListView"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetWindow.cs"
                    }),
                new UiStory(
                    "gesture-assignment-cell",
                    "Domain/FaceExpression/Components",
                    "GestureAssignmentCell",
                    "一つのGestureにAnimationClipとPreviewを割り当てるCardです。",
                    "AnimationClipの選択とDrag & Dropに対応し、Previewと未割り当て状態を切り替えます。",
                    BuildAssignmentCell,
                    dependencies: new[]
                    {
                        "PreviewSurface",
                        "EmptyState",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss"
                    }),
                new UiStory(
                    "blend-shape-preset-mapping-row",
                    "Domain/FaceExpression/Components",
                    "BlendShapePresetMappingRow",
                    "BlendShape名にVariationとSideを割り当てる編集行です。",
                    "Source名を確認しながらVariationとSideを編集し、Drag & Dropによる移動を処理します。",
                    BuildPresetMappingRow,
                    dependencies: new[] { "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss"
                    }),
                new UiStory(
                    "blend-shape-row",
                    "Domain/FaceExpression/Components",
                    "BlendShapeRow",
                    "BlendShape一件のAnimation、値、Variation、Sideを編集する行です。",
                    "Animation対象の切り替え、値の調整、VariationとSideの選択を一行で行います。",
                    BuildBlendShapeRow,
                    dependencies: new[]
                    {
                        "ContentRow",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss"
                    })
            };
        }

        private static void BuildAssignmentCell(VisualElement parent)
        {
            var clip = new AnimationClip
            {
                name = "Smile"
            };
            var cell = new GestureAssignmentCell(
                "Expression clip",
                "Unassigned",
                () => { },
                _ => { },
                rect => EditorGUI.DrawRect(
                    rect,
                    new Color(0.18f, 0.28f, 0.42f, 1f)));
            cell.SetAssignment(new FaceExpressionAssignment(clip));
            cell.style.width = 220f;
            cell.style.height = 180f;
            cell.RegisterCallback<DetachFromPanelEvent>(_ =>
                UnityEngine.Object.DestroyImmediate(clip));
            parent.Add(cell);
        }

        private static void BuildPresetMappingRow(VisualElement parent)
        {
            var row = new BlendShapePresetMappingRow(
                () => { },
                _ => null,
                (_, __) => { });
            row.Bind(new BlendShapeNameMapping
            {
                meshName = "Body",
                shapeName = "eye_blink_1_L",
                variation = "1",
                side = "L"
            });
            row.style.width = 620f;
            row.style.height = 34f;
            parent.Add(row);
        }

        private static void BuildBlendShapeRow(VisualElement parent)
        {
            var items = BlendShapeRowItem.Create(
                new[]
                {
                    CreateStoryChannel("eye_blink_1", 80f, true),
                    CreateStoryChannel("eye_blink_1_L", 0f, false),
                    CreateStoryChannel("eye_blink_1_R", 0f, false)
                },
                CreateStoryRule(),
                true);
            var row = new BlendShapeRow();
            row.SetItem(items[0], false);
            row.style.width = 720f;
            parent.Add(row);
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

        private static void BuildPresets(VisualElement parent)
        {
            var view = new BlendShapePresetView(
                new StorySettingsService());
            view.SetDraft(
                CreateStoryPreset(),
                false,
                "Story preview");
            view.SetAvatarSelectionEnabled(false);
            view.style.width = 960f;
            view.style.height = 560f;
            parent.Add(view);
        }

        private static BlendShapeFbxPreset CreateStoryPreset()
        {
            return new BlendShapeFbxPreset
            {
                assetGuid = "story-fbx",
                assetPath = "Assets/Avatar/Body.fbx",
                name = "Body",
                mappings = new List<BlendShapeNameMapping>
                {
                    new BlendShapeNameMapping
                    {
                        meshLocalId = 1L,
                        meshName = "Body",
                        shapeName = "eye_blink_1",
                        headerText = "Eyes",
                        role = "blink",
                        variation = "1"
                    },
                    new BlendShapeNameMapping
                    {
                        meshLocalId = 1L,
                        meshName = "Body",
                        shapeName = "eye_blink_1_L",
                        role = "blink",
                        variation = "1",
                        side = "L"
                    },
                    new BlendShapeNameMapping
                    {
                        meshLocalId = 1L,
                        meshName = "Body",
                        shapeName = "eye_blink_1_R",
                        role = "blink",
                        variation = "1",
                        side = "R"
                    },
                    new BlendShapeNameMapping
                    {
                        meshLocalId = 1L,
                        meshName = "Body",
                        shapeName = "mouth_smile_1",
                        headerText = "Mouth",
                        role = "smile",
                        variation = "1"
                    }
                }
            };
        }

        private sealed class StorySettingsService : ISettingsService
        {
            private readonly List<SettingDefinitionBase> _definitions =
                new List<SettingDefinitionBase>();
            private readonly Dictionary<string, object> _values =
                new Dictionary<string, object>(StringComparer.Ordinal);

            public event EventHandler<SettingChangedEventArgs> Changed;

            public void Register(SettingDefinitionBase definition)
            {
                if (definition == null ||
                    _values.ContainsKey(definition.Key))
                {
                    return;
                }

                _definitions.Add(definition);
                _values.Add(definition.Key, definition.DefaultValue);
            }

            public IReadOnlyList<SettingDefinitionBase> GetDefinitions(
                SettingScope scope)
            {
                return _definitions.FindAll(
                    definition => definition.Scope == scope);
            }

            public void Preload(SettingScope scope)
            {
            }

            public T Get<T>(SettingDefinition<T> definition)
            {
                return (T)Get((SettingDefinitionBase)definition);
            }

            public object Get(SettingDefinitionBase definition)
            {
                return _values[definition.Key];
            }

            public void Set<T>(
                SettingDefinition<T> definition,
                T value,
                bool saveImmediately = true)
            {
                Set((SettingDefinitionBase)definition, value, saveImmediately);
            }

            public void Set(
                SettingDefinitionBase definition,
                object value,
                bool saveImmediately = true)
            {
                _values[definition.Key] = value;
                Changed?.Invoke(
                    this,
                    new SettingChangedEventArgs(definition, value));
            }

            public void Save(SettingScope? scope = null)
            {
            }
        }
    }
}
