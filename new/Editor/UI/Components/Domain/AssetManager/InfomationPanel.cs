using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class InfomationPanel : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--infomation";

        public InfomationPanel()
        {
            AssetManagerPanelFactory.Populate(
                this,
                RootClassName,
                AssetManagerPanelFactory.CreateCard(
                    "Selection",
                    "Inspector",
                    "選択中アセットの詳細と編集操作を置く領域です。",
                    "メタデータ",
                    "タグ / ラベル",
                    "依存関係と参照先"),
                AssetManagerPanelFactory.CreateCard(
                    "Preview",
                    "Details",
                    "未選択時の空状態と、プレビュー・検証結果をここに出します。",
                    "Thumbnail / Preview",
                    "Validation",
                    "Version Notes"));
        }
    }
}
