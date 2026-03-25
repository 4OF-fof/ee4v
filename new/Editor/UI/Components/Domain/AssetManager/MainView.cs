using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class MainView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--main-view";

        public MainView()
        {
            AssetManagerPanelFactory.Populate(
                this,
                RootClassName,
                AssetManagerPanelFactory.CreateCard(
                    "AssetManager",
                    "Assets",
                    "検索、一覧、選択状態の主要導線を置く主領域です。",
                    "検索バーとフィルタ",
                    "グリッド / リスト切り替え",
                    "一括操作と import/export 導線"),
                AssetManagerPanelFactory.CreateCard(
                    "Workflow",
                    "Main View",
                    "ここに Asset 一覧や空状態、読み込み中表示を組み込みます。",
                    "検索結果一覧",
                    "ドラッグ＆ドロップ導線",
                    "進行中タスクの状態表示"));
        }
    }
}
