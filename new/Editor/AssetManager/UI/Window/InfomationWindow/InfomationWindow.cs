using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class InfomationWindow : EditorWindow
    {
        private const string WindowTitle = "Infomation";
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName = "ee4v-asset-manager-window";
        private const string BodyClassName = "ee4v-asset-manager-window__standalone-panel-body";
        private InfomationPanel _infomationPanel;
        private StandaloneAssetManagerViewSession _standaloneViewSession;

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

        private void OnDisable()
        {
            UnbindStandaloneSession();
            _infomationPanel = null;
        }

        private void CreateGUI()
        {
            UnbindStandaloneSession();
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/ItemImage/item-image.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/ImageStack/image-stack.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/InputField/input-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/SearchField/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Collections/SearchableTreeView/searchable-tree-view.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/Interactive/ViewToggleTabs/view-toggle-tabs.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/InfomationPanel/infomation-panel.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Window/InfomationWindow/infomation-window.uss");

            var body = new VisualElement();
            body.AddToClassList(BodyClassName);
            _infomationPanel = new InfomationPanel();
            _infomationPanel.FileDetailRequested += MainViewWindow.ShowFileDetail;
            body.Add(_infomationPanel);

            root.Add(body);
            WindowToastApi.EnsureHost(this);
            BindStandaloneSession();
        }

        private void BindStandaloneSession()
        {
            _standaloneViewSession =
                AssetManagerUiDependencies.StandaloneViewSession;
            _standaloneViewSession.SelectionChanged +=
                OnStandaloneSelectionChanged;
            _standaloneViewSession.DetailTabRequested +=
                OnStandaloneDetailTabRequested;
            OnStandaloneSelectionChanged(
                _standaloneViewSession.SelectedItems,
                _standaloneViewSession.SelectionContentKind);
            if (!string.IsNullOrWhiteSpace(
                    _standaloneViewSession.SelectedDetailTabId))
            {
                OnStandaloneDetailTabRequested(
                    _standaloneViewSession.SelectedDetailTabId);
            }
        }

        private void UnbindStandaloneSession()
        {
            if (_standaloneViewSession == null)
            {
                return;
            }

            _standaloneViewSession.SelectionChanged -=
                OnStandaloneSelectionChanged;
            _standaloneViewSession.DetailTabRequested -=
                OnStandaloneDetailTabRequested;
            _standaloneViewSession = null;
        }

        private void OnStandaloneSelectionChanged(
            System.Collections.Generic.IReadOnlyList<ItemCardState> items,
            AssetSelectionContentKind contentKind)
        {
            _infomationPanel?.SetSelectedAssetItems(items, contentKind);
        }

        private void OnStandaloneDetailTabRequested(string tabId)
        {
            _infomationPanel?.SetSelectedAssetDetailTab(tabId);
        }
    }
}
