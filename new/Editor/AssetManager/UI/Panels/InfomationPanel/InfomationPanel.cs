using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class InfomationPanel : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--infomation";

        public InfomationPanel()
        {
            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);
        }
    }
}
