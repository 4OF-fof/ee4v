using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.Images;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerStoryProvider : IUiStoryProvider
    {
        public int Order => 200;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory("asset-modification-workflow", "Domain/AssetManager", "AssetModificationWorkflowView",
                    "ダミーアバターで統合編集画面のカテゴリと外見タブを確認します。",
                    "保存・SDK操作を無効にし、専用Preview Sceneとメモリ上のManagerだけを使用します。",
                    BuildWorkflow,
                    usageLocations: new[] { "Editor/AssetManager/UI/AssetModificationWorkflowWindow.cs" },
                    styleSheetPaths: new[] {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/asset-detail.uss",
                        "Editor/AssetManager/UI/asset-modification-workflow.uss" }),
                new UiStory("variant-save-overlay", "Domain/AssetManager/Inputs", "AssetVariantSaveOverlay",
                    "保存メッセージ、キャンセル、背景操作の抑止を確認します。",
                    "入力値を画面へ表示するだけで、Variantやライブラリを保存しません。",
                    BuildSaveOverlay,
                    usageLocations: new[] { "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs" },
                    styleSheetPaths: new[] { "Editor/AssetManager/UI/asset-manager.uss" }),
                new UiStory("asset-prefab-picker", "Domain/AssetManager", "AssetPrefabPickerWindow",
                    "ダミーPrefab候補と大きなプレビューを確認します。", "選択結果は画面内にだけ反映します。",
                    BuildPrefabPicker,
                    usageLocations: new[] { "Editor/AssetManager/UI/AssetModificationWorkflowView.Selection.cs",
                        "Editor/AssetManager/UI/AssetModificationWorkflowView.Composition.cs" },
                    styleSheetPaths: new[] { "Editor/AssetManager/UI/asset-modification-workflow.uss" }),
                new UiStory("asset-search-filter-popup", "Domain/AssetManager", "AssetSearchFilterPopup",
                    "検索条件の編集と確定を確認します。", "ダミー条件とタグ候補だけを使用します。",
                    BuildSearchFilter,
                    usageLocations: new[] { "Editor/AssetManager/UI/AssetManagerView.Navigation.cs" }),
                new UiStory("target-import-popup", "Domain/AssetManager", "TargetImportPopup",
                    "グループ別の取り込み候補選択を確認します。", "確定結果を表示するだけでファイルを取り込みません。",
                    AssetManagerView.BuildTargetImportSample,
                    usageLocations: new[] { "Editor/AssetManager/UI/AssetManagerView.Actions.cs" }),
                new UiStory("target-editor-popup", "Domain/AssetManager", "TargetEditorPopup",
                    "ダミーファイルのTarget編集を確認します。", "メモリ上のManagerだけを使用します。",
                    AssetManagerView.BuildTargetEditorSample,
                    usageLocations: new[] { "Editor/AssetManager/UI/AssetManagerView.Actions.cs" }),
                new UiStory(
                    "variant-creation",
                    "Domain/AssetManager",
                    "VariantCreationView",
                    "AssetManagerと統合ee4vで共有するVariant作成画面です。",
                    "表示幅、Prefabの有無、名前の検証、保存先の表示とエラーを確認できます。プレビュー用Prefabを指定すると実際の3Dプレビューを表示します。StoryではAssetを作成しません。",
                    BuildVariantCreation,
                    dependencies: new[]
                    {
                        "SectionHeader", "FormInput", "InputField", "PrefabSelector", "AssetPrefabPickerWindow", "PrefabScenePreview", "UiButton"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Items.cs",
                        "Editor/AssetManager/UI/AssetModificationWorkflowView.Selection.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-detail.uss",
                        "Editor/AssetManager/UI/asset-modification-workflow.uss"
                    }),
                new UiStory(
                    "asset-manager-workspace",
                    "Domain/AssetManager",
                    "AssetManagerWorkspaceView",
                    "ee4vウィンドウのアセット選択を担うAssetManagerの3ペインです。",
                    "Navigation、Main、Informationをサンプルデータで操作し、選択アセットからVariant作成へ進みます。Variantタブは現在のProject内の作成済みVariantを表示します。",
                    BuildWorkspace,
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetModificationWorkflowView.Selection.cs",
                        "Editor/AssetManager/UI/AssetManagerLibraryWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/searchable-file-tree.uss",
                        "Editor/AssetManager/UI/asset-detail.uss",
                        "Editor/AssetManager/UI/asset-modification-workflow.uss"
                    }),
                new UiStory(
                    "asset-manager-view",
                    "Domain/AssetManager",
                    "AssetManagerView · Main",
                    "AssetManager Main Windowの一覧と詳細操作を確認する画面です。",
                    "実画面と同じMain modeのAssetManagerViewをサンプルデータで表示します。Item一覧、File Tree、Target設定、絞り込みアイコンと文字検索以外の条件が有効な状態を確認できます。",
                    Build,
                    dependencies: new[]
                    {
                        "UiTextFactory",
                        "InfoCard",
                        "Badge",
                        "AssetDetailComponents",
                        "Fluent UI System Icons"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerStandaloneWindows.cs",
                        "Editor/AssetManager/UI/AssetManagerWorkspaceView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/searchable-file-tree.uss",
                        "Editor/AssetManager/UI/asset-detail.uss"
                    }),
                new UiStory(
                    "asset-manager-tag-list",
                    "Domain/AssetManager/Collections",
                    "AssetTagListView",
                    "タグPillの折り返し配置、検索、Item数と並び順を確認するStoryです。",
                    "階層prefixと件数を補助文字にした軽いTagPillを横へ並べます。全幅の検索欄、下線付き文字タブ、名前順での頭文字の見出しと漢字の「その他」グループ、表示幅に合わせた折り返しとタグ選択を確認できます。",
                    BuildTagList,
                    dependencies: new[]
                    {
                        "TagPill", "SearchField", "UiButton"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Catalog.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),

                new UiStory(
                    "asset-manager-separated-windows",
                    "Domain/AssetManager",
                    "AssetManagerView · Separated modes",
                    "分離した3ウィンドウの連動状態を確認するStoryです。",
                    "同じIAssetManagerと表示状態を共有するNavigation、Main、Informationを並べ、ページ切替と選択が別ペインへ反映されることを確認できます。",
                    BuildSeparatedWindows,
                    dependencies: new[]
                    {
                        "AssetManagerView",
                        "AssetManagerViewState"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerStandaloneWindows.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/searchable-file-tree.uss",
                        "Editor/AssetManager/UI/asset-detail.uss"
                    }),
                new UiStory(
                    "asset-manager-collection-popup",
                    "Domain/AssetManager",
                    "AssetCollectionCreationPopup",
                    "コレクションの作成、アイコン選択と条件編集を確認するStoryです。",
                    "ボタンから実際のポップアップを開き、アイコン選択、入れ子のAND、OR、条件とグループの反転、条件とグループの追加と削除を確認できます。保存時はStory内のサンプルだけを更新します。",
                    BuildCollectionPopup,
                    dependencies: new[]
                    {
                        "UiTextFactory",
                        "AssetManagerControls"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Collections.cs",
                        "Editor/AssetManager/UI/AssetManagerView.Navigation.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-file-tree",
                    "Domain/AssetManager/Collections",
                    "SearchableFileTree",
                    "Item詳細で使用する検索付きFile Treeです。",
                    "FileをルートにしてZIPやUnityPackageの内容を階層表示し、選択した要素を右ペインへ渡します。",
                    BuildFileTree,
                    dependencies: new[]
                    {
                        "SearchableTreeView",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/AssetManager/UI/AssetManagerView.Items.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/searchable-file-tree.uss"
                    }),

                new UiStory(
                    "asset-manager-filter-editor",
                    "Domain/AssetManager/Inputs",
                    "AssetFilterEditor",
                    "絞り込みとCollectionの条件を編集するパーツです。",
                    "種類別の追加ボタン、種類と値を並べた条件行、一致方法の切り替えと入れ子Groupを表示します。",
                    BuildFilterEditor,
                    dependencies: new[]
                    {
                        "ActionBar",
                        "FormInput",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetCollectionCreationPopup.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),

                };
        }

        private static void BuildWorkflow(VisualElement parent)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var avatar = new GameObject("Sample Work") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(avatar, scene);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.hideFlags = HideFlags.HideAndDontSave;
            body.transform.SetParent(avatar.transform, false);
            var material = new Material(Shader.Find("Standard")) { hideFlags = HideFlags.HideAndDontSave,
                color = new Color(0.2f, 0.5f, 0.65f) };
            body.GetComponent<Renderer>().sharedMaterial = material;
            var view = new AssetModificationWorkflowView(parent.MarkDirtyRepaint,
                new AssetManagerStoryManager(), avatar);
            view.style.width = 1360;
            view.style.height = 740;
            view.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != view) { return; }
                view.Dispose();
                UnityEngine.Object.DestroyImmediate(avatar);
                UnityEngine.Object.DestroyImmediate(material);
                if (scene.IsValid()) { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            });
            parent.Add(view);
        }

        private static void BuildSaveOverlay(VisualElement parent)
        {
            var host = new VisualElement();
            host.style.height = 420;
            host.style.width = 900;
            var result = UiTextFactory.Create("保存操作はメモリ内だけで確認します。", UiClassNames.SecondaryText);
            host.Add(result);
            host.Add(new UiButton("保存フォームを開く", () =>
                AssetVariantSaveOverlay.Show(host, message => result.SetText("入力: " + message))));
            parent.Add(host);
        }

        private static void BuildPrefabPicker(VisualElement parent)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            prefab.name = "Sample Prefab";
            prefab.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prefab, scene);
            var result = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            UiButton open = null;
            open = new UiButton("Prefab候補を開く", () => AssetPrefabPickerWindow.ShowForSource(open,
                new AssetModificationWorkflowView.VariantSourceOption
                {
                    Item = new AssetItem { Id = "story-prefab", Name = "Sample Item" },
                    Prefabs = new[] { prefab }
                }, prefab, selected => result.SetText("選択: " + selected.name)));
            open.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != open) { return; }
                foreach (var popup in Resources.FindObjectsOfTypeAll<AssetPrefabPickerWindow>()) { popup.Close(); }
                UnityEngine.Object.DestroyImmediate(prefab);
                if (scene.IsValid()) { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            });
            parent.Add(open);
            parent.Add(result);
        }

        private static void BuildSearchFilter(VisualElement parent)
        {
            var result = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            UiButton open = null;
            open = new UiButton("検索条件を開く", () => AssetSearchFilterPopup.Show(open,
                new[] { new TagOption("Avatar", 12), new TagOption("Costume", 5) },
                AssetFilterNode.Condition(AssetFilterConditionType.NameContains, "Sample"),
                filter => result.SetText("検索条件を確定しました"),
                filter => result.SetText("検索条件をメモリ上で保存しました")));
            parent.Add(open);
            parent.Add(result);
        }

        private static void BuildFilterEditor(VisualElement parent)
        {
            var editor = new AssetFilterEditor(AssetFilterNode.And(
                AssetFilterNode.Condition(
                    AssetFilterConditionType.HasTag,
                    "Avatar"),
                AssetFilterNode.Or(
                    AssetFilterNode.Condition(AssetFilterConditionType.NameContains, "Shinano"),
                    AssetFilterNode.Not(AssetFilterNode.Condition(
                        AssetFilterConditionType.HasFileExtension,
                        "zip")))), tagOptions: new[]
                {
                    new TagOption("Avatar", 12),
                    new TagOption("Cloth", 8)
                });
            editor.style.width = 620f;
            parent.Add(editor);
        }

        private static void BuildFileTree(VisualElement parent)
        {
            var manager = new AssetManagerStoryManager();
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-asset-manager");
            surface.style.width = 360f;
            surface.style.height = 420f;
            var tree = new SearchableFileTree(manager);
            tree.SetItem(
                "item-avatar",
                manager.GetFiles("item-avatar"));
            surface.RegisterCallback<DetachFromPanelEvent>(_ => tree.Dispose());
            surface.Add(tree);
            parent.Add(surface);
        }

        private static void BuildVariantCreation(VisualElement parent)
        {
            var width = UiTextFactory.CreateSlider("表示幅", 420f, 1040f);
            width.value = 1040f;
            width.showInputField = true;
            parent.Add(width);
            var prefab = UiTextFactory.CreateObjectField();
            prefab.objectType = typeof(GameObject);
            prefab.allowSceneObjects = false;
            parent.Add(new FormInput("プレビュー用Prefab", prefab));
            var original = UiTextFactory.CreateToggle("マイアセットの新規作成");
            parent.Add(original);
            var surface = new VisualElement();
            surface.style.height = 620f;
            surface.style.width = width.value;
            surface.style.maxWidth = Length.Percent(100f);
            parent.Add(surface);
            width.RegisterValueChangedCallback(evt => surface.style.width = evt.newValue);
            VariantCreationView form = null;
            System.Action rebuild = () =>
            {
                var name = form?.Name.Value ?? "Sample Avatar Variant";
                var description = form?.Description.Value ?? "衣装・マテリアルを編集するVariant";
                surface.Clear();
                var source = prefab.value as GameObject;
                if (source != null && !AssetDatabase.GetAssetPath(source).EndsWith(
                    ".prefab", System.StringComparison.OrdinalIgnoreCase)) { source = null; }
                form = new VariantCreationView(
                    new AssetItem { Id = "story-variant-source", Name = "Sample Avatar" },
                    source == null ? System.Array.Empty<GameObject>() : new[] { source },
                    name, description, source, I18N.Get("workflow.selection.createAndStart"),
                    () => form.SetFeedback(string.Empty), original.value);
                form.Create.clicked += () => form.SetFeedback(I18N.Get("notice.derivedAssetCreateFailed"));
                surface.Add(form);
            };
            prefab.RegisterValueChangedCallback(_ => rebuild());
            original.RegisterValueChangedCallback(_ => rebuild());
            rebuild();
        }

        private static void Build(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.height = 620f;
            surface.style.minWidth = 920f;
            var view = new AssetManagerView(new AssetManagerStoryManager());
            view.SetOriginalAssetCreationHandler(() => ShowOriginalCreationStory(surface, view));
            view.RegisterCallback<DetachFromPanelEvent>(_ => view.Dispose());
            surface.Add(view);
            parent.Add(surface);
        }

        private static void BuildWorkspace(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.height = 620f;
            surface.style.minWidth = 1040f;
            var manager = new AssetManagerStoryManager();
            var state = new AssetManagerViewState();
            AssetManagerWorkspaceView workspace = null;
            workspace = new AssetManagerWorkspaceView(
                _ => { },
                itemId =>
                {
                    state.OpenItemDetail(itemId);
                    state.OpenDerivedAssetsPage(itemId);
                },
                (mode, createDerivedAsset) => new AssetManagerView(
                    manager, state, mode, createDerivedAsset: createDerivedAsset),
                createOriginalAsset: () => ShowOriginalCreationStory(surface, workspace));
            workspace.RegisterCallback<DetachFromPanelEvent>(_ => workspace.Dispose());
            surface.Add(workspace);
            parent.Add(surface);
        }

        private static void ShowOriginalCreationStory(VisualElement surface, VisualElement library)
        {
            library.style.display = DisplayStyle.None;
            VariantCreationView form = null;
            form = new VariantCreationView(null, null, "Sample Work", string.Empty, null,
                I18N.Get("workflow.original.create"), () =>
                {
                    form.RemoveFromHierarchy();
                    library.style.display = DisplayStyle.Flex;
                }, original: true);
            form.Create.clicked += () => form.SetFeedback(I18N.Get("notice.derivedAssetCreateFailed"));
            surface.Add(form);
        }

        private static void BuildTagList(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.height = 480f;
            surface.style.minWidth = 360f;
            var selected = UiTextFactory.Create("Select a tag.");
            var list = new AssetTagListView(path => selected.SetText(path));
            var tags = new[]
            {
                new AssetTag { Path = "avatar" },
                new AssetTag { Path = "avatar/costume" },
                new AssetTag { Path = "avatar/accessory/hair" },
                new AssetTag { Path = "world/lighting" },
                new AssetTag { Path = "shader" },
                new AssetTag { Path = "archived-only" },
                new AssetTag { Path = "衣装" },
                new AssetTag { Path = "髪型" },
                new AssetTag { Path = "あくせさり" },
                new AssetTag { Path = "アバター" }
            };
            list.SetData(tags, new[]
            {
                new AssetItem { Tags = new[] { tags[0], tags[1], tags[2] } },
                new AssetItem { Tags = new[] { tags[1] } },
                new AssetItem { Tags = new[] { tags[3], tags[4] } },
                new AssetItem { Tags = new[] { tags[5] }, IsArchived = true },
                new AssetItem { Tags = new[] { tags[6], tags[7], tags[8], tags[9] } }
            });
            surface.Add(list);
            surface.Add(selected);
            parent.Add(surface);
        }

        private static void BuildSeparatedWindows(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.height = 620f;
            surface.style.minWidth = 1120f;
            surface.style.flexDirection = FlexDirection.Row;

            var manager = new AssetManagerStoryManager();
            var state = new AssetManagerViewState();
            var navigation = new AssetManagerView(
                manager,
                state,
                AssetManagerViewMode.Navigation);
            navigation.style.width = 240f;
            navigation.style.flexShrink = 0f;
            var main = new AssetManagerView(
                manager,
                state,
                AssetManagerViewMode.Main);
            main.style.flexGrow = 1f;
            main.SetOriginalAssetCreationHandler(() => ShowOriginalCreationStory(surface, main));
            var information = new AssetManagerView(
                manager,
                state,
                AssetManagerViewMode.Information);
            information.style.width = 300f;
            information.style.flexShrink = 0f;

            surface.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                navigation.Dispose();
                main.Dispose();
                information.Dispose();
            });
            surface.Add(navigation);
            surface.Add(main);
            surface.Add(information);
            state.SelectItem("item-avatar");
            parent.Add(surface);
        }

        private static void BuildCollectionPopup(VisualElement parent)
        {
            var surface = new VisualElement();
            var result = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            surface.style.width = 420f;
            surface.AddToClassList("ee4v-asset-manager-controls-story");
            surface.Add(AssetManagerControls.CreateNotice(
                "各ボタンの直下にコレクション編集ポップアップを表示します。"));

            var createButton = AssetManagerControls.CreateButton(
                "新規コレクションを開く");
            createButton.clicked += () =>
                AssetCollectionCreationPopup.Show(
                    createButton,
                    null,
                    (name, icon, root) => { result.SetText("作成: " + name); return true; },
                    tagOptions: new[] { new TagOption("Avatar", 12), new TagOption("Cloth", 8) });
            surface.Add(createButton);

            var sample = new AssetCollection
            {
                Id = "collection-story",
                Name = "Avatar favorites",
                Root = AssetFilterNode.Or(
                    AssetFilterNode.And(
                        AssetFilterNode.Condition(
                            AssetFilterConditionType.HasTag,
                            "Avatar"),
                        AssetFilterNode.Or(
                            AssetFilterNode.Condition(
                                AssetFilterConditionType.NameContains,
                                "Summer"),
                            AssetFilterNode.Condition(
                                AssetFilterConditionType.DescriptionContains,
                                "Favorite"))),
                    AssetFilterNode.Not(AssetFilterNode.Condition(
                        AssetFilterConditionType.HasFileExtension,
                        "zip")))
            };
            var editButton = AssetManagerControls.CreateButton(
                "既存コレクションを編集");
            editButton.clicked += () =>
                AssetCollectionCreationPopup.Show(
                    editButton,
                    sample,
                    (name, icon, root) =>
                    {
                        sample.Name = name;
                        sample.Icon = icon;
                        sample.Root = root;
                        result.SetText("更新: " + name);
                        return true;
                    },
                    tagOptions: new[] { new TagOption("Avatar", 12), new TagOption("Cloth", 8) });
            surface.Add(editButton);
            surface.Add(result);
            parent.Add(surface);
        }

    }

    internal sealed partial class AssetManagerView
    {
        internal static void BuildTargetImportSample(VisualElement parent)
        {
            var result = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            UiButton open = null;
            open = new UiButton("取り込み候補を開く", () => TargetImportPopup.Show(open,
                new[] { new TargetImportGroup { Name = "Sample Avatar", Choices = new[]
                {
                    new TargetImportChoice { Label = "Avatar.unitypackage", Target = new AssetFileTarget
                        { FileId = "story-file", TargetPath = "Avatar.unitypackage" } },
                    new TargetImportChoice { Label = "Costume.unitypackage", Target = new AssetFileTarget
                        { FileId = "story-file", TargetPath = "Costume.unitypackage" } }
                } } }, selected => result.SetText("選択した候補数: " + selected.Count)));
            parent.Add(open);
            parent.Add(result);
        }

        internal static void BuildTargetEditorSample(VisualElement parent)
        {
            var manager = new AssetManagerStoryManager();
            var result = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            UiButton open = null;
            open = new UiButton("Target編集を開く", () => TargetEditorPopup.Show(open,
                manager, "item-avatar", manager.GetFiles("item-avatar"), manager.GetItemTargets("item-avatar"),
                "Sample Targets", "確定", selected => result.SetText("設定したTarget数: " + selected.Count)));
            parent.Add(open);
            parent.Add(result);
        }
    }
}
