using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class InfomationWindow : EditorWindow
    {
        private const string WindowTitle = "Infomation";
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName = "ee4v-asset-manager-window";
        private const string BodyClassName = "ee4v-asset-manager-window__standalone-panel-body";

        [MenuItem("ee4v/Asset Manager/Infomation", false, 2)]
        private static void ShowWindow()
        {
            var window = GetWindow<InfomationWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(360f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(360f, 420f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Content/ItemImage/item-image.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Content/ImageStack/image-stack.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Inputs/InputField/input-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Inputs/SearchField/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Collections/SearchableTreeView/searchable-tree-view.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Content/Interactive/ViewToggleTabs/view-toggle-tabs.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/InfomationPanel/infomation-panel.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Window/InfomationWindow/infomation-window.uss");

            var body = new VisualElement();
            body.AddToClassList(BodyClassName);
            body.Add(new InfomationPanel());

            root.Add(body);
            WindowToastApi.EnsureHost(this);
        }
    }
}
