using System.Collections.Generic;
using Ee4v.AvatarEditing;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    internal sealed class AvatarPartsStoryProvider : IUiStoryProvider
    {
        public int Order => 202;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[] { new UiStory("avatar-parts-editor", "Domain/AvatarParts/Containers", "AvatarPartsEditor",
                "パーツ階層と体型編集を切り替えます。サンプルは読み取り専用です。",
                "パーツ・体型の編集機能です。", Build,
                usageLocations: new[] { "Editor/AssetManager/UI/AssetModificationWorkflowView.MaterialAssets.cs",
                    "Editor/Feature/Avatar/AvatarParts/AvatarShapePartsWindow.cs" },
                styleSheetPaths: new[] {
                    "Editor/Feature/Shared/AvatarEditing/avatar-editing.uss",
                    "Editor/Feature/Avatar/AvatarParts/avatar-parts.uss" }) };
        }

        private static void Build(VisualElement parent)
        {
            var avatar = new GameObject("Sample Avatar") { hideFlags = HideFlags.HideAndDontSave };
            foreach (var name in new[] { "Head", "Gloves", "Boots" })
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = name;
                part.hideFlags = HideFlags.HideAndDontSave;
                part.transform.SetParent(avatar.transform, false);
            }
            var header = new VisualElement();
            var controls = new ScrollView();
            controls.style.maxHeight = 420;
            AvatarPartsEditor editor = null;
            var context = new AvatarEditingContext(
                new AvatarEditingServices(() => true, _ => false, () => true,
                    () => string.Empty, _ => { }, () => { }, _ => { }, _ => { }),
                new AvatarEditingHost(Render, Render, () => { }, parent.MarkDirtyRepaint, () => { }))
            {
                Root = avatar, UiRoot = parent, ControlsHost = controls
            };
            editor = new AvatarPartsEditor(context);
            void Render()
            {
                header.Clear();
                header.Add(editor.BuildShapePartsTabs());
                controls.Clear();
                controls.Add(editor.BuildControls());
                controls.SetEnabled(false);
            }
            parent.Add(header);
            parent.Add(controls);
            parent.RegisterCallback<DetachFromPanelEvent>(_ => UnityEngine.Object.DestroyImmediate(avatar));
            Render();
        }
    }
}
