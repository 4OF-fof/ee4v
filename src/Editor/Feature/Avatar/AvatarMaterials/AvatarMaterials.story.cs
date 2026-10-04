using System.Collections.Generic;
using Ee4v.AvatarEditing;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarMaterials
{
    internal sealed class AvatarMaterialsStoryProvider : IUiStoryProvider
    {
        public int Order => 203;

        public IReadOnlyList<UiStory> GetStories()
        {
            var styles = new[] {
                "Editor/Feature/Shared/AvatarEditing/avatar-editing.uss",
                "Editor/Feature/Avatar/AvatarMaterials/avatar-materials.uss" };
            return new[] {
                new UiStory("avatar-materials-editor", "Domain/AvatarMaterials/Containers", "AvatarMaterialsEditor",
                    "使用Materialとプレビュー表示を管理します。サンプルの割り当ては読み取り専用です。",
                    "Material編集機能です。", Build,
                    usageLocations: new[] { "Editor/AssetManager/UI/AssetModificationWorkflowView.MaterialAssets.cs",
                        "Editor/Feature/Avatar/AvatarMaterials/AvatarMaterialsWindow.cs" },
                    styleSheetPaths: styles),
                new UiStory("embedded-material-inspector", "Domain/AvatarMaterials/Inputs", "EmbeddedMaterialInspector",
                    "MaterialEditorのInspectorを可変幅の領域に埋め込みます。", "メモリー上のMaterialを編集します。",
                    BuildInspector,
                    usageLocations: new[] { "Editor/Feature/Avatar/AvatarMaterials/AvatarMaterialsEditor.Materials.cs" },
                    styleSheetPaths: styles)
            };
        }

        private static void Build(VisualElement parent)
        {
            var avatar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            avatar.name = "Sample Avatar";
            avatar.hideFlags = HideFlags.HideAndDontSave;
            var material = new Material(Shader.Find("Standard")) {
                name = "Sample Material", hideFlags = HideFlags.HideAndDontSave };
            avatar.GetComponent<Renderer>().sharedMaterial = material;
            var controls = new ScrollView();
            controls.style.maxHeight = 420;
            AvatarMaterialsEditor editor = null;
            var context = new AvatarEditingContext(
                new AvatarEditingServices(() => false, _ => false, () => true,
                    () => string.Empty, _ => { }, () => { }, _ => { }, _ => { }),
                new AvatarEditingHost(Render, Render, () => { }, parent.MarkDirtyRepaint, () => { }))
            {
                Root = avatar, UiRoot = parent, ControlsHost = controls
            };
            editor = new AvatarMaterialsEditor(context);
            void Render() { controls.Clear(); controls.Add(editor.BuildControls()); }
            parent.Add(controls);
            parent.RegisterCallback<DetachFromPanelEvent>(_ => {
                editor.Dispose();
                UnityEngine.Object.DestroyImmediate(avatar);
                UnityEngine.Object.DestroyImmediate(material);
            });
            Render();
        }

        private static void BuildInspector(VisualElement parent)
        {
            var material = new Material(Shader.Find("Standard")) {
                name = "Sample Material", hideFlags = HideFlags.HideAndDontSave };
            var inspector = new EmbeddedMaterialInspector(material, parent.MarkDirtyRepaint);
            parent.Add(inspector);
            parent.RegisterCallback<DetachFromPanelEvent>(_ => {
                inspector.Dispose();
                UnityEngine.Object.DestroyImmediate(material);
            });
        }
    }
}
