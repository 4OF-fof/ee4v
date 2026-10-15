using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class EditorToolsStoryProvider : IUiStoryProvider
    {
        public int Order => 25;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                Story("selection-tab-bar", "Containers", "SelectionTabBar",
                    "固定タブ、スクロールする選択タブと操作を配置します。",
                    BuildHeader, "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs"),
                Story("selection-tab", "Inputs", "SelectionTab",
                    "選択状態と長い名前を持つタブです。クリックで選択状態を確認します。",
                    BuildSelectionTab, "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs"),
                Story("exclusive-button-group", "Inputs", "ExclusiveButtonGroup",
                    "選択肢のボタンから一つを選ぶ汎用入力です。無効な選択肢も表示します。",
                    BuildExclusiveButtonGroup, "Editor/Feature/Shared/AvatarEditing/AvatarEditingUi.cs"),
                Story("game-object-preview", "Displays", "GameObjectPreview",
                    "共通の3Dプレビューです。独立したPreview Sceneのサンプルでカメラと選択を確認します。",
                    BuildScenePreview, "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs",
                    "Editor/AssetManager/UI/VariantCreationView.cs",
                    "Editor/AssetManager/Simulation/AvatarExecutionView.cs",
                    "Editor/Feature/Avatar/ExpressionMenu/ExpressionMenuPreview.cs"),
                Story("object-selector", "Inputs", "ObjectSelector",
                    "呼び出し側が渡したUnity Object候補を検索・選択します。",
                    BuildObjectSelector, "Editor/AssetManager/UI/VariantCreationView.cs"),
                Story("object-thumbnail", "Displays", "ObjectThumbnail",
                    "UnityのAssetPreviewを使うObjectのサムネイルです。",
                    BuildObjectThumbnail, "Editor/UI/Components/Inputs/ObjectSelector/ObjectPickerWindow.cs")
            };
        }

        private static UiStory Story(string id, string group, string title,
            string description, Action<VisualElement> build, params string[] usages)
        {
            return new UiStory(id, group, title, description, description, parent =>
            {
                if (id == "selection-tab") { build(parent); return; }
                var surface = new VisualElement();
                surface.AddToClassList("ee4v-ui-editor-tools-story");
                surface.AddToClassList("ee4v-ui-editor-tools-story--" + id);
                build(surface);
                var controls = new InfoCard(new InfoCardState("コントロール"));
                var enabled = UiTextFactory.CreateToggle();
                enabled.SetValueWithoutNotify(true);
                enabled.RegisterValueChangedCallback(evt => surface.SetEnabled(evt.newValue));
                controls.Body.Add(new FormInput("有効", enabled));
                var width = UiTextFactory.CreateSlider(lowValue: 320, highValue: 1200);
                width.SetValueWithoutNotify(960);
                width.showInputField = true;
                width.RegisterValueChangedCallback(evt => surface.style.width = evt.newValue);
                controls.Body.Add(new FormInput("表示幅", width));
                parent.Add(controls);
                var preview = new InfoCard(new InfoCardState("プレビュー"));
                preview.Body.Add(surface);
                parent.Add(preview);
            }, usageLocations: usages,
                styleSheetPaths: new[] { "Editor/UI/Catalog/editor-tools-story.uss" });
        }

        private static void BuildHeader(VisualElement parent)
        {
            var header = new SelectionTabBar();
            var tabs = new List<SelectionTab>();
            var leading = new SelectionTab("Sample Avatar", SelectionTabVariant.Primary);
            leading.RegisterCallback<ClickEvent>(_ =>
            {
                foreach (var candidate in tabs) { candidate.SetSelected(candidate == leading); }
            });
            tabs.Add(leading);
            header.SetLeadingTab(leading);
            void AddTab(string name)
            {
                var tab = new SelectionTab(name);
                tab.RegisterCallback<ClickEvent>(_ =>
                {
                    foreach (var candidate in tabs) { candidate.SetSelected(candidate == tab); }
                });
                tabs.Add(tab);
                header.Tabs.Add(tab);
            }
            for (var index = 0; index < 12; index++) { AddTab("Prefab " + (index + 1)); }
            header.Items.Add(new UiButton("タブを追加", () => AddTab("Added tab")));
            parent.Add(header);
        }

        private static void BuildSelectionTab(VisualElement parent)
        {
            var controls = new InfoCard(new InfoCardState("コントロール"));
            var preview = new InfoCard(new InfoCardState("プレビュー"));
            var label = new InputField(new InputFieldState("Long sample prefab name"));
            controls.Body.Add(new FormInput("文字", label));
            var variant = UiTextFactory.CreateEnumField(string.Empty, SelectionTabVariant.Default);
            controls.Body.Add(new FormInput("種類", variant));
            var toggle = UiTextFactory.CreateToggle();
            controls.Body.Add(new FormInput("選択", toggle));
            var selected = false;
            void Render()
            {
                preview.Body.Clear();
                var tab = new SelectionTab(label.Value, (SelectionTabVariant)variant.value);
                tab.SetSelected(selected);
                tab.RegisterCallback<ClickEvent>(_ =>
                {
                    selected = !selected;
                    toggle.SetValueWithoutNotify(selected);
                    tab.SetSelected(selected);
                });
                preview.Body.Add(tab);
            }
            label.ValueChanged += _ => Render();
            variant.RegisterValueChangedCallback(_ => Render());
            toggle.RegisterValueChangedCallback(evt => { selected = evt.newValue; Render(); });
            parent.Add(controls);
            parent.Add(preview);
            Render();
        }

        private static void BuildExclusiveButtonGroup(VisualElement parent)
        {
            var selector = new ExclusiveButtonGroup<string>(new[]
            {
                new ExclusiveButtonOption<string>("all", "すべて"),
                new ExclusiveButtonOption<string>("first", "選択肢A"),
                new ExclusiveButtonOption<string>("second", "選択肢B"),
                new ExclusiveButtonOption<string>("disabled", "無効な選択肢")
            }, "all", available: value => value != "disabled");
            parent.Add(selector);
        }

        private static void BuildScenePreview(VisualElement parent)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var source = new GameObject("Preview sample") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(source, scene);
            AddPrimitive(source, "Body", PrimitiveType.Capsule, new Vector3(0, 0.9f, 0),
                new Vector3(0.5f, 0.7f, 0.3f));
            AddPrimitive(source, "Head", PrimitiveType.Sphere, new Vector3(0, 1.8f, 0),
                Vector3.one * 0.45f);
            AddPrimitive(source, "Part", PrimitiveType.Cube, new Vector3(0.5f, 0.9f, 0),
                new Vector3(0.2f, 0.8f, 0.2f));
            var preview = new GameObjectPreview();
            preview.SetFlexibleLayout(true);
            preview.SetFullBodyFraming(true);
            preview.PreviewObjectClicked += (key, material) => preview.SetListSelection(key, material);
            preview.PreviewSelectionCleared += () => preview.SetListSelection(null, null);
            preview.SetTarget(source);
            parent.Add(preview);
            preview.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                preview.Dispose();
                if (source != null) { UnityEngine.Object.DestroyImmediate(source); }
                if (scene.IsValid()) { EditorSceneManager.ClosePreviewScene(scene); }
            });
        }

        private static void AddPrimitive(GameObject root, string name,
            PrimitiveType type, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.hideFlags = HideFlags.HideAndDontSave;
            part.name = name;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static GameObject[] GetObjectCandidates(VisualElement host)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var candidates = new List<GameObject>();
            for (var index = 0; index < 3; index++)
            {
                var sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sample.name = "Sample Object " + (index + 1);
                sample.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(sample, scene);
                candidates.Add(sample);
            }
            host.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != host) { return; }
                foreach (var sample in candidates) { UnityEngine.Object.DestroyImmediate(sample); }
                if (scene.IsValid()) { EditorSceneManager.ClosePreviewScene(scene); }
            });
            return candidates.ToArray();
        }

        private static void BuildObjectSelector(VisualElement parent)
        {
            parent.Add(new ObjectSelector<GameObject>(GetObjectCandidates(parent)));
        }

        private static void BuildObjectThumbnail(VisualElement parent)
        {
            var preview = new ObjectThumbnail(GetObjectCandidates(parent).FirstOrDefault());
            preview.AddToClassList("ee4v-ui-editor-tools-story__object-thumbnail");
            parent.Add(preview);
        }

    }
}
