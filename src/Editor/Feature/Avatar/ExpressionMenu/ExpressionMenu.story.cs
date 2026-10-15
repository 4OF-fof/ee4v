using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Ee4v.ExpressionMenu
{
    internal sealed class ExpressionMenuStoryProvider : IUiStoryProvider
    {
        public int Order => 205;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                Story("expression-menu-view", "Domain/ExpressionMenu", "ExpressionMenuView",
                    "ダミーメニューの階層移動と項目詳細を確認します。", BuildMenu,
                    "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs",
                    "Editor/Feature/Avatar/ExpressionMenu/ExpressionMenuWindow.cs"),
                Story("expression-menu-template", "Domain/ExpressionMenu/Inputs", "ExpressionMenuTemplateEditor",
                    "ダミーMA項目の設定フォームを読み取り専用で表示します。", BuildTemplate,
                    "Editor/Feature/Avatar/ExpressionMenu/ExpressionMenuView.cs"),
                Story("expression-menu-preview", "Domain/ExpressionMenu/Containers", "ExpressionMenuPreview",
                    "ダミーアバターとToggleのプレビューを操作します。", BuildPreview,
                    "Editor/Feature/Avatar/ExpressionMenu/ExpressionMenuView.cs")
            };
        }

        private static UiStory Story(string id, string group, string title, string description,
            Action<VisualElement> build, params string[] usages)
        {
            return new UiStory(id, group, title, description,
                "専用Preview Sceneと一時オブジェクトだけを使い、Project・ライブラリへ保存しません。",
                build, usageLocations: usages,
                styleSheetPaths: new[] { "Editor/Feature/Avatar/ExpressionMenu/expression-menu.uss" });
        }

        private static AvatarEditingContext Fixture(VisualElement host, out ModularAvatarMenuItem item)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var avatar = new GameObject("Sample Avatar") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(avatar, scene);
            var descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.hideFlags = HideFlags.HideAndDontSave;
            menu.name = "Sample Menu";
            var submenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            submenu.hideFlags = HideFlags.HideAndDontSave;
            submenu.name = "Expressions";
            submenu.controls.Add(new VRCExpressionsMenu.Control
            {
                name = "Smile", type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "Smile" }, value = 1
            });
            menu.controls.Add(new VRCExpressionsMenu.Control
            {
                name = "Expressions", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = submenu
            });
            menu.controls.Add(new VRCExpressionsMenu.Control
            {
                name = "Outfit", type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "Outfit" }, value = 1
            });
            descriptor.expressionsMenu = menu;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.hideFlags = HideFlags.HideAndDontSave;
            body.transform.SetParent(avatar.transform, false);
            var source = new GameObject("Sample Menu Item") { hideFlags = HideFlags.HideAndDontSave };
            source.transform.SetParent(avatar.transform, false);
            item = source.AddComponent<ModularAvatarMenuItem>();
            item.PortableControl.SetFrom(new VRCExpressionsMenu.Control
            {
                name = "Sample Toggle", type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "Sample" }, value = 1
            });
            var context = new AvatarEditingContext(
                new AvatarEditingServices(() => false, _ => false, () => true, () => string.Empty,
                    _ => { }, host.MarkDirtyRepaint, _ => { }, _ => { }),
                new AvatarEditingHost(host.MarkDirtyRepaint, host.MarkDirtyRepaint, () => { },
                    host.MarkDirtyRepaint, () => { }))
            { Root = avatar, UiRoot = host, ControlsHost = new ScrollView() };
            host.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != host) { return; }
                UnityEngine.Object.DestroyImmediate(avatar);
                UnityEngine.Object.DestroyImmediate(menu);
                UnityEngine.Object.DestroyImmediate(submenu);
                if (scene.IsValid()) { EditorSceneManager.ClosePreviewScene(scene); }
            });
            return context;
        }

        private static VisualElement Surface(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.width = 1120;
            surface.style.height = 680;
            parent.Add(surface);
            return surface;
        }

        private static void BuildMenu(VisualElement parent)
        {
            var surface = Surface(parent);
            var context = Fixture(surface, out _);
            surface.Add(new ExpressionMenuView(context));
        }

        private static void BuildTemplate(VisualElement parent)
        {
            var surface = Surface(parent);
            var context = Fixture(surface, out var item);
            surface.Add(new ExpressionMenuTemplateEditor(context, item, surface.MarkDirtyRepaint));
        }

        private static void BuildPreview(VisualElement parent)
        {
            var surface = Surface(parent);
            var context = Fixture(surface, out var item);
            ExpressionMenuModel.Read(context.Root, out var sources);
            var entry = sources.SelectMany(page => page.Controls).First(value => value.Owner == item);
            var result = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            surface.Add(new ExpressionMenuPreview(context, entry, () => result.SetText("プレビューから戻りました")));
            surface.Q<PrefabScenePreview>().SetFullBodyFraming(true);
            surface.Add(result);
        }
    }
}
