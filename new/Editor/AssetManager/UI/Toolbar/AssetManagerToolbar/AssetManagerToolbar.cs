using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class AssetManagerToolbar : VisualElement
    {
        private const string RootClassName = "ee4v-ui-asset-manager-toolbar";
        private const string ContentClassName = "ee4v-ui-asset-manager-toolbar__content";

        public AssetManagerToolbar()
        {
            AddToClassList(RootClassName);

            Content = new VisualElement();
            Content.AddToClassList(ContentClassName);
            Add(Content);
        }

        public VisualElement Content { get; }
    }
}
