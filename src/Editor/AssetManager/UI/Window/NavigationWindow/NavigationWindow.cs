using Ee4v.UI;
using Ee4v.AssetManager.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class NavigationWindow : EditorWindow
    {
        private const string WindowTitle = "Navigation";
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName = "ee4v-asset-manager-window";
        private const string BodyClassName = "ee4v-asset-manager-window__standalone-panel-body";
        private CollectionNavigationController _collectionController;
        private NavigationPanel _navigationPanel;
        private StandaloneAssetManagerViewSession
            _standaloneViewSession;
        private string _selectedItemId =
            AssetManagerNavigationCatalog.DefaultItemId;

        [MenuItem("ee4v/Window/Navigation", false, 1)]
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

        private void OnDisable()
        {
            DisposeCollectionController();
        }

        private void CreateGUI()
        {
            DisposeCollectionController();
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/Button/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/NavigationPanel/navigation-panel.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/Selection/SingleSelectButtonGroup/single-select-button-group.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Window/NavigationWindow/navigation-window.uss");

            var body = new VisualElement();
            body.AddToClassList(BodyClassName);
            _navigationPanel = new NavigationPanel();
            body.Add(_navigationPanel);

            root.Add(body);
            WindowToastApi.EnsureHost(this);

            _collectionController =
                new CollectionNavigationController();
            _standaloneViewSession =
                AssetManagerUiDependencies.StandaloneViewSession;
            _selectedItemId =
                _standaloneViewSession.SelectedNavigationItemId;
            _navigationPanel.SetSelectedItem(_selectedItemId);
            _standaloneViewSession.NavigationChanged +=
                OnStandaloneNavigationChanged;
            _collectionController.CollectionsChanged +=
                OnCollectionsChanged;
            _collectionController.CollectionCreated +=
                OnCollectionCreated;
            _collectionController.ErrorChanged +=
                _navigationPanel.SetCollectionError;
            _navigationPanel.CreateCollectionRequested +=
                OnCreateCollectionRequested;
            _navigationPanel.CreateSmartCollectionRequested +=
                OnCreateSmartCollectionRequested;
            _navigationPanel.SelectionChanged +=
                OnSelectionChanged;
            _navigationPanel.ManualSyncRequested +=
                AssetManagerUiDependencies.RequestManualSync;
            _collectionController.Activate();
        }

        private void OnCollectionsChanged(
            System.Collections.Generic.IReadOnlyList<
                AssetCollection> collections)
        {
            _navigationPanel?.SetCollections(
                collections,
                _selectedItemId);
        }

        private void OnCollectionCreated(AssetCollection collection)
        {
            if (collection != null)
            {
                _selectedItemId =
                    AssetManagerCollectionViewId.Encode(collection.Id);
                _navigationPanel?.SetSelectedItem(_selectedItemId);
                _standaloneViewSession?.SetNavigation(
                    _selectedItemId);
            }
        }

        private void OnSelectionChanged(string itemId)
        {
            _selectedItemId = itemId ?? string.Empty;
            _standaloneViewSession?.SetNavigation(
                _selectedItemId);
        }

        private void OnStandaloneNavigationChanged(
            string itemId)
        {
            _selectedItemId = itemId ??
                              AssetManagerNavigationCatalog
                                  .DefaultItemId;
            _navigationPanel?.SetSelectedItem(
                _selectedItemId);
        }

        private void OnCreateCollectionRequested(
            VisualElement anchor)
        {
            ShowCreationWindow(anchor, false);
        }

        private void OnCreateSmartCollectionRequested(
            VisualElement anchor)
        {
            ShowCreationWindow(anchor, true);
        }

        private void ShowCreationWindow(
            VisualElement anchor,
            bool smart)
        {
            if (_collectionController == null)
            {
                return;
            }

            CollectionCreationWindow.Show(
                anchor,
                smart,
                _collectionController.CreateCollection,
                _collectionController.CreateSmartCollection);
        }

        private void DisposeCollectionController()
        {
            if (_navigationPanel != null)
            {
                _navigationPanel.CreateCollectionRequested -=
                    OnCreateCollectionRequested;
                _navigationPanel.CreateSmartCollectionRequested -=
                    OnCreateSmartCollectionRequested;
                _navigationPanel.SelectionChanged -=
                    OnSelectionChanged;
                _navigationPanel.ManualSyncRequested -=
                    AssetManagerUiDependencies.RequestManualSync;
            }

            if (_standaloneViewSession != null)
            {
                _standaloneViewSession.NavigationChanged -=
                    OnStandaloneNavigationChanged;
                _standaloneViewSession = null;
            }

            if (_collectionController == null)
            {
                return;
            }

            _collectionController.CollectionsChanged -=
                OnCollectionsChanged;
            _collectionController.CollectionCreated -=
                OnCollectionCreated;
            if (_navigationPanel != null)
            {
                _collectionController.ErrorChanged -=
                    _navigationPanel.SetCollectionError;
            }

            _collectionController.Dispose();
            _collectionController = null;
        }
    }
}
