using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class MainViewWindow : EditorWindow
    {
        private const string WindowTitle = "Main View";
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName = "ee4v-asset-manager-window";
        private const string BodyClassName = "ee4v-asset-manager-window__main-view-window-body";
        private const string ContentClassName = "ee4v-asset-manager-window__main-view-window-content";

        [MenuItem("ee4v/Asset Manager/Main View", false, 3)]
        private static void ShowWindow()
        {
            var window = GetWindow<MainViewWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(640f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(640f, 420f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/MainView/main-view.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Toolbar/AssetManagerToolbar/asset-manager-toolbar.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Display/ItemCard/item-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/DataView/ItemGrid/item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/DataView/AssetItemGrid/asset-item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Window/MainViewWindow/main-view-window.uss");

            var body = new VisualElement();
            body.AddToClassList(BodyClassName);

            var toolbar = new AssetManagerToolbar();
            var mainView = new MainView();
            mainView.AddToClassList(ContentClassName);

            body.Add(toolbar);
            body.Add(mainView);

            root.Add(body);
            WindowToastApi.EnsureHost(this);
        }
    }
}
