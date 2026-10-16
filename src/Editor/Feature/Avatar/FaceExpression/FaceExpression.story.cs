using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
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
                new UiStory("expression-conversion", "Domain/FaceExpression", "ExpressionConversionView",
                    "ダミーClipのBlendShape対応付け、検証と変換を確認します。",
                    "変換結果はメモリ上だけで作成して破棄し、ファイルダイアログと保存は行いません。",
                    BuildConversion,
                    usageLocations: new[] { "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs" },
                    styleSheetPaths: new[] { "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss" }),
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
                    "左右ジェスチャー64通りをサムネイル付きセルで確認する画面です。",
                    "割り当てセッションを読み取り、確認セルからClipの変更やDrag & Dropは行いません。",
                    BuildAssignments,
                    dependencies: new[] { "UiButton", "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Inputs/ui-button.uss",
                        "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss"
                    }),
                new UiStory(
                    "gesture-assignment-picker",
                    "Domain/FaceExpression/Inputs",
                    "GestureAssignmentPickerView",
                    "表情の条件を追加し、左右の手またはメニューの選択を即時に反映する操作欄です。",
                    "条件ボタンの即時割り当てと複数条件を、保存しないセッションで確認します。",
                    BuildAssignmentPicker,
                    dependencies: new[] { "UiButton", "UiTextFactory", "SectionHeader" },
                    usageLocations: new[] { "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionWindow.cs" },
                    styleSheetPaths: new[] { "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss" }),
                new UiStory(
                    "gesture-assignment-settings",
                    "Domain/FaceExpression",
                    "Gesture Assignment Settings",
                    "選択したGestureと全体設定を編集する画面です。",
                    "表情画面とGestureAssignmentSettingsWindowが使用する実際の設定Viewを、保存しないメモリ上の設定で表示します。",
                    BuildAssignmentSettings,
                    dependencies: new[]
                    {
                        "FormInput",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionWindow.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentSettingsWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
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
                    },
                    styleSheetPaths: new[] { "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss" }),
                new UiStory(
                    "gesture-assignment-cell",
                    "Domain/FaceExpression/Inputs",
                    "GestureAssignmentCell",
                    "一つのGestureにAnimationClipとPreviewを割り当てるCardです。",
                    "AnimationClipの選択とDrag & Dropに対応し、Previewと未割り当て状態を切り替えます。",
                    BuildAssignmentCell,
                    dependencies: new[]
                    {
                        "PreviewContainer",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss"
                    }),
                new UiStory(
                    "blend-shape-preset-mapping-row",
                    "Domain/FaceExpression/Inputs",
                    "BlendShapePresetMappingRow",
                    "BlendShape名にSideを割り当てる編集行です。",
                    "Source名を確認しながらSideを編集し、Drag & Dropによる移動を処理します。",
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
                    "Domain/FaceExpression/Inputs",
                    "BlendShapeRow",
                    "BlendShape一件のAnimationと値を編集する行です。",
                    "通常版を親行、左右版を子行として個別に編集します。",
                    BuildBlendShapeRow,
                    dependencies: new[]
                    {
                        "ItemRow",
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

        private static void BuildConversion(VisualElement parent)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var avatar = new GameObject("Conversion Sample") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(avatar, scene);
            var body = new GameObject("Body") { hideFlags = HideFlags.HideAndDontSave };
            body.transform.SetParent(avatar.transform, false);
            var mesh = new Mesh { name = "Sample Face", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            var delta = new[] { Vector3.up * 0.1f, Vector3.zero, Vector3.zero };
            mesh.AddBlendShapeFrame("Smile", 100, delta, new Vector3[3], new Vector3[3]);
            body.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;
            var clip = new AnimationClip { name = "Smile", hideFlags = HideFlags.HideAndDontSave };
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile"),
                AnimationCurve.Linear(0, 0, 1, 100));
            var result = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            var view = new ExpressionConversionView(avatar, clip,
                converted => result.SetText("メモリ上で変換しました: " + converted.name), previewOnly: true);
            view.style.width = 1000;
            view.style.height = 620;
            view.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != view) { return; }
                UnityEngine.Object.DestroyImmediate(avatar);
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(clip);
                if (scene.IsValid()) { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            });
            parent.Add(view);
            parent.Add(result);
        }

        private static void BuildAssignmentCell(VisualElement parent)
        {
            var clip = new AnimationClip
            {
                name = "Smile"
            };
            var result = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            GestureAssignmentCell cell = null;
            cell = new GestureAssignmentCell(
                "Expression clip",
                "Unassigned",
                () => result.SetText("セルを選択しました"),
                selected =>
                {
                    cell.SetAssignment(new FaceExpressionAssignment(selected));
                    result.SetText(selected != null ? selected.name : "未割り当て");
                },
                rect => EditorGUI.DrawRect(
                    rect,
                    new Color(0.18f, 0.28f, 0.42f, 1f)));
            cell.SetAssignment(new FaceExpressionAssignment(clip));
            cell.style.width = 220f;
            cell.style.height = 180f;
            cell.RegisterCallback<DetachFromPanelEvent>(_ =>
                UnityEngine.Object.DestroyImmediate(clip));
            parent.Add(cell);
            parent.Add(result);
        }

        private static void BuildPresetMappingRow(VisualElement parent)
        {
            var result = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            var row = new BlendShapePresetMappingRow(
                () => result.SetText("マッピングをメモリ内で変更しました"),
                _ => null,
                (_, __) => result.SetText("マッピングを受け取りました"));
            row.Bind(new BlendShapeNameMapping
            {
                meshName = "Body",
                shapeName = "mouth_smile_L",
                side = "L",
                mouthMorph = true
            });
            row.style.width = 620f;
            row.style.height = 34f;
            parent.Add(row);
            parent.Add(result);
        }

        private static void BuildBlendShapeRow(VisualElement parent)
        {
            var favorites = new HashSet<string>(StringComparer.Ordinal);
            var items = BlendShapeRowItem.Create(
                new[]
                {
                    new BlendShapeChannel("Body", "---EYE---", 0f, false, "Eye"),
                    CreateStoryChannel("eye_blink_1", 80f, true),
                    CreateStoryChannel("eye_blink_1_L", 0f, false),
                    CreateStoryChannel("eye_blink_1_R", 0f, false),
                    new BlendShapeChannel("Body", "---MOUTH---", 0f, false, "Mouth"),
                    CreateStoryChannel("mouth_smile", 25f, true)
                },
                CreateStoryRule(),
                false);
            foreach (var item in items)
            {
                var row = new BlendShapeRow(isFavorite: key => favorites.Contains(key),
                    toggleFavorite: key => { if (!favorites.Remove(key)) { favorites.Add(key); } });
                row.SetItem(item);
                row.style.width = 720f;
                parent.Add(row);
            }
        }

        private static void Build(VisualElement parent)
        {
            var result = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            var view = new FaceExpressionView(
                new FaceExpressionViewText
                {
                    Avatar = "Avatar",
                    Clip = "Expression clip",
                    NewClip = "New expression",
                    CopyAndEdit = "Copy and edit",
                    Edit = "Edit",
                    ResetView = "Reset view",
                    PreviewBackground = "Toggle background brightness",
                    BackToLibrary = "Back",
                    BackToAssignments = "Back to assignments",
                    SearchPlaceholder = "Search BlendShapes",
                    SearchTooltip = "Filter BlendShapes by name",
                    ClearSearchTooltip = "Clear search",
                    LibrarySearchPlaceholder = "Search expression library",
                    LibrarySearchTooltip = "Filter expressions and folders by name",
                    BlendShapes = "BlendShapes",
                    Library = "Expression Library",
                    ClipOnly = "In clip only",
                    ClipOnlyTooltip = "Show only BlendShapes stored in the clip",
                    NoBlendShapes = "No BlendShapes were found.",
                    Play = "Play the expression animation",
                    Pause = "Pause playback",
                    Loop = "Loop",
                    Sequence = "Expression sequence",
                    Pose = "Pose {0}",
                    AddPose = "+ Pose",
                    AddPoseTooltip = "Duplicate the selected pose after it",
                    AddPoseUnavailable = "Edit a BlendShape before adding another pose",
                    InsertPose = "+",
                    InsertPoseTooltip = "Insert a pose here",
                    MovePoseEarlier = "Move this pose earlier",
                    MovePoseLater = "Move this pose later",
                    RenamePose = "Rename pose...",
                    ResetPoseName = "Reset pose name",
                    AddClip = "Source clip",
                    AddClipTooltip = "Assign an existing clip to this read-only pose",
                    RemovePose = "Remove this pose",
                    Transition = "Transition",
                    TimelineTooltip = "Scrub the expression preview"
                },
                _ => { },
                (_, time, rect) => EditorGUI.DrawRect(
                    rect,
                    Color.Lerp(
                        new Color(0.18f, 0.28f, 0.42f, 1f),
                        new Color(0.42f, 0.22f, 0.3f, 1f),
                        Mathf.Clamp01(time))));
            var smile = new AnimationClip { name = "Smile" };
            var blink = new AnimationClip { name = "Blink" };
            view.SetLibrary(
                new[] { "Assets/Expressions/Happy" },
                new[] { smile, blink },
                false,
                (_, rect) => EditorGUI.DrawRect(
                    rect,
                    new Color(0.18f, 0.28f, 0.42f, 1f)));
            view.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                UnityEngine.Object.DestroyImmediate(smile);
                UnityEngine.Object.DestroyImmediate(blink);
            });
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
            view.SetClip(smile);
            view.SetAnimationState(
                0.5f,
                1f,
                true,
                false,
                new[] { 0f, 0.5f, 1f },
                new AnimationClip[] { null, blink, null },
                new[] { "Start", "Blink", "Smile" },
                1,
                true,
                "Play the expression animation",
                "Pause playback");
            var surface = new VisualElement();
            surface.style.width = 920f;
            surface.style.height = 640f;
            surface.Add(view);
            surface.Add(result);
            view.SaveRequested += () => result.SetText("サンプルをメモリ内で確定しました");
            view.NewClipRequested += () => result.SetText("新規表情を要求しました（Storyでは保存しません）");
            view.CopyClipRequested += clip => result.SetText("コピーを要求: " + clip?.name);
            view.EditClipRequested += clip => result.SetText("編集を要求: " + clip?.name);
            view.LibraryClipSelected += clip => { view.SetClip(clip); result.SetText("選択: " + clip?.name); };
            view.ClipChanged += clip => result.SetText("Clip変更: " + clip?.name);
            view.LibraryFolderRequested += folder => result.SetText("フォルダー選択: " + folder);
            view.ChannelChanged += _ => result.SetText("BlendShapeをメモリ内で変更しました");
            view.ConversionRequested += () => result.SetText("変換画面はExpressionConversionView Storyで確認できます");
            view.BackRequested += () => result.SetText("ライブラリへ戻る操作を受け取りました");
            view.ClearClipAssignmentsRequested += clip => result.SetText("割り当て解除: " + clip?.name);
            view.AssignmentSelectionCleared += () => result.SetText("選択を解除しました");
            view.PoseSelected += pose => result.SetText("Pose: " + pose);
            view.PoseAddRequested += () => result.SetText("Poseの追加を要求しました");
            view.PoseInsertRequested += pose => result.SetText("Poseの挿入: " + pose);
            view.PoseMoveRequested += (from, to) => result.SetText("Poseの移動: " + from + " → " + to);
            view.PoseNameChanged += (pose, name) => result.SetText("Pose名: " + name);
            view.PoseSourceChanged += (pose, clip) => result.SetText("PoseのClip: " + clip?.name);
            view.PoseRemoveRequested += pose => result.SetText("Poseの削除: " + pose);
            view.TransitionDurationChanged += (pose, value) => result.SetText("遷移時間: " + value);
            view.ResetViewRequested += () => result.SetText("カメラをリセットしました");
            view.PlaybackChanged += () => result.SetText("再生状態を切り替えました");
            view.TimeChanged += value => result.SetText("再生位置: " + value);
            view.LoopChanged += value => result.SetText("ループ: " + value);
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
                sourceMeshLocalId: 1L,
                initialValue: 0f);
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
                    LeftHand = "Left hand",
                    RightHand = "Right hand",
                    Selection = "Selected",
                    ExpressionSettings = "Expression settings",
                    GlobalSettings = "Global settings",
                    Clip = "Expression clip",
                    EnableBlink = "Enable blinking",
                    FixMouth = "Fix mouth movement",
                    Unassigned = "Unassigned",
                    MenuOnly = "Extra (menu only)",
                    MenuOnlyHint = "Select an Extra cell below the divider to edit it in the settings window.",
                    MenuName = "Menu name",
                    EnableMenuIcons = "Generate thumbnails",
                    AddMenuExpression = "Add menu-only expression",
                    Remove = "Remove",
                    GestureName = gesture => I18N.Get("gesture." + gesture)
                }, readOnly: true);
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
        }

        private static void BuildAssignmentPicker(VisualElement parent)
        {
            var session = new GestureAssignmentSession();
            var avatar = new GameObject("Assignment preview") { hideFlags = HideFlags.HideAndDontSave };
            var clip = new AnimationClip { name = "Smile", hideFlags = HideFlags.HideAndDontSave };
            session.SetConfiguration(new FaceExpressionConfiguration(new Dictionary<GestureCombination, FaceExpressionAssignment>(), null));
            var picker = new GestureAssignmentPickerView(GestureAssignmentWindow.CreateText(), session, new StorySettingsService());
            picker.SetAvatar(avatar);
            picker.SetCandidate(clip);
            picker.CandidateChanged += picker.SetCandidate;
            picker.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                session.Dispose();
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(avatar);
            });
            parent.Add(picker);
        }

        private static void BuildAssignmentSettings(VisualElement parent)
        {
            var settingsSurface = new VisualElement();
            settingsSurface.style.width = 420f;
            settingsSurface.style.height = 400f;
            settingsSurface.Add(new GestureAssignmentSettingsView(
                new GestureAssignmentViewText
                {
                    Selection = "Selected",
                    ExpressionSettings = "Expression settings",
                    GlobalSettings = "Global settings",
                    EnableBlink = "Enable blinking",
                    FixMouth = "Fix mouth movement",
                    MenuOnly = "Extra (menu only)",
                    MenuName = "Menu name",
                    EnableMenuIcons = "Generate thumbnails",
                    Remove = "Remove",
                    GestureName = gesture => I18N.Get("gesture." + gesture)
                },
                new StorySettingsService()));
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
                    CopyBlendShapes = "Copy as BlendShapes",
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
                new StorySettingsService(),
                new StoryBlendShapePresetStore());
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
                        role = "blink_1"
                    },
                    new BlendShapeNameMapping
                    {
                        meshLocalId = 1L,
                        meshName = "Body",
                        shapeName = "eye_blink_1_L",
                        role = "blink_1",
                        side = "L"
                    },
                    new BlendShapeNameMapping
                    {
                        meshLocalId = 1L,
                        meshName = "Body",
                        shapeName = "eye_blink_1_R",
                        role = "blink_1",
                        side = "R"
                    },
                    new BlendShapeNameMapping
                    {
                        meshLocalId = 1L,
                        meshName = "Body",
                        shapeName = "mouth_smile_1",
                        headerText = "Mouth",
                        role = "smile_1"
                    }
                }
            };
        }

        private sealed class StoryBlendShapePresetStore :
            IBlendShapePresetStore
        {
            private readonly BlendShapeNamePresetState _state =
                new BlendShapeNamePresetState();

            public event Action Changed;

            public BlendShapeNamePresetState Load()
            {
                return _state;
            }

            public void Save(BlendShapeFbxPreset preset)
            {
                BlendShapeNamePresetSetting.Upsert(_state, preset);
                Changed?.Invoke();
            }

            public void OpenDirectory()
            {
            }
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
