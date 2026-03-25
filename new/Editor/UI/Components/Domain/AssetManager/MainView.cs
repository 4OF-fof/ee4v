using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class MainView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--main-view";

        public MainView()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RefreshContent();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssetManagerViewState.SelectedItemChanged += OnSelectedItemChanged;
            RefreshContent();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssetManagerViewState.SelectedItemChanged -= OnSelectedItemChanged;
        }

        private void OnSelectedItemChanged(string itemId)
        {
            RefreshContent();
        }

        private void RefreshContent()
        {
            var selectedItem = AssetManagerViewState.SelectedItem;
            AssetManagerPanelFactory.Populate(
                this,
                RootClassName,
                AssetManagerPanelFactory.CreateCard(
                    selectedItem.Eyebrow,
                    selectedItem.Title,
                    selectedItem.Description,
                    selectedItem.Rows),
                AssetManagerPanelFactory.CreateCard(
                    "Workflow",
                    "Main View",
                    "Navigation で選択された要素に応じて主領域の文脈を切り替えます。",
                    string.Format("Current Selection: {0}", selectedItem.Label),
                    string.Format("Context: {0}", selectedItem.Meta),
                    "ドラッグ＆ドロップ導線",
                    "進行中タスクの状態表示"));
        }
    }
}
