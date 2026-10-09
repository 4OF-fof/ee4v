using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
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
                    BuildPrefabTab, "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs"),
                Story("body-part-selector", "Inputs", "BodyPartSelector",
                    "部位の選択と利用できない部位の無効表示を管理します。",
                    BuildBodyPartSelector, "Editor/AssetManager/UI/AssetModificationWorkflowView.EditingSession.cs"),
                Story("prefab-scene-preview", "Displays", "PrefabScenePreview",
                    "共通の3Dプレビューです。独立したPreview Sceneのサンプルでカメラと選択を確認します。",
                    BuildScenePreview, "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs",
                    "Editor/AssetManager/UI/AssetModificationWorkflowView.Selection.cs", "Editor/AssetManager/UI/AssetManagerView.Items.cs",
                    "Editor/AssetManager/Simulation/AvatarExecutionView.cs",
                    "Editor/Feature/Avatar/ExpressionMenu/ExpressionMenuPreview.cs"),
                Story("prefab-selector", "Inputs", "PrefabSelector",
                    "呼び出し側が渡したPrefab候補を選択する入力です。ProjectのAssetは変更しません。",
                    BuildPrefabSelector, "Editor/AssetManager/UI/AssetModificationWorkflowView.Selection.cs", "Editor/AssetManager/UI/AssetManagerView.Items.cs"),
                Story("prefab-thumbnail", "Displays", "PrefabThumbnail",
                    "UnityのAssetPreviewを使うPrefabサムネイルです。",
                    BuildPrefabPreview, "Editor/AssetManager/UI/AssetModificationWorkflowView.Selection.cs",
                    "Editor/UI/Components/Inputs/PrefabSelector/PrefabPickerWindow.cs")
            };
        }

        private static UiStory Story(string id, string group, string title,
            string description, Action<VisualElement> build, params string[] usages)
        {
            return new UiStory(id, group, title, description, description, parent =>
            {
                var surface = new VisualElement();
                surface.AddToClassList("ee4v-ui-editor-tools-story");
                surface.AddToClassList("ee4v-ui-editor-tools-story--" + id);
                build(surface);
                var controls = new InfoCard(new InfoCardState("コントロール"));
                var enabled = UiTextFactory.CreateToggle();
                enabled.SetValueWithoutNotify(true);
                enabled.RegisterValueChangedCallback(evt => surface.SetEnabled(evt.newValue));
                controls.Body.Add(new FormInput("有効", enabled));
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

        private static void BuildPrefabTab(VisualElement parent)
        {
            var tab = new SelectionTab("Long sample prefab name");
            var selected = false;
            tab.RegisterCallback<ClickEvent>(_ => tab.SetSelected(selected = !selected));
            parent.Add(tab);
        }

        private static void BuildBodyPartSelector(VisualElement parent)
        {
            BodyPartSelector selector = null;
            selector = new BodyPartSelector(null, part => part != BodyPartCategory.Hands,
                part => selector.SetSelected(part));
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
            var preview = new PrefabScenePreview();
            preview.SetFlexibleLayout(true);
            preview.SetFullBodyFraming(true);
            preview.PreviewObjectClicked += (key, material) => preview.SetListSelection(key, material);
            preview.PreviewSelectionCleared += () => preview.SetListSelection(null, null);
            preview.SetPrefab(source);
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

        private static GameObject[] GetPrefabCandidates()
        {
            return AssetDatabase.FindAssets("t:Prefab").Take(12)
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(prefab => prefab != null).ToArray();
        }

        private static void BuildPrefabSelector(VisualElement parent)
        {
            parent.Add(new PrefabSelector(GetPrefabCandidates()));
        }

        private static void BuildPrefabPreview(VisualElement parent)
        {
            var preview = new PrefabThumbnail(GetPrefabCandidates().FirstOrDefault());
            preview.AddToClassList("ee4v-ui-editor-tools-story__prefab-thumbnail");
            parent.Add(preview);
        }

    }
}
