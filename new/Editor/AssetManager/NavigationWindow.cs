using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class NavigationWindow : EditorWindow
    {
        private const string WindowTitle = "Navigation";
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName = "ee4v-asset-manager-window";
        private const string BodyClassName = "ee4v-asset-manager-window__standalone-panel-body";

        [MenuItem("ee4v/Asset Manager/Navigation", false, 1)]
        private static void ShowWindow()
        {
            var window = GetWindow<NavigationWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(320f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(320f, 420f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/AssetManager/panels.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/info-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Interactive/single-select-button-group.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/asset-manager-window.uss");

            var body = new VisualElement();
            body.AddToClassList(BodyClassName);
            body.Add(new NavigationPanel());

            root.Add(body);
            WindowToastApi.EnsureHost(this);
        }
    }
}
