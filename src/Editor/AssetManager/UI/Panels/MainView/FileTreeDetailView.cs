using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class FileTreeDetailView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-file-detail";
        private readonly VisualElement _contentHost;

        public FileTreeDetailView()
        {
            AddToClassList(RootClassName);
            _contentHost = new VisualElement();
            _contentHost.style.flexGrow = 1f;
            Add(_contentHost);
        }

        public void SetState(FileTreeDetailState state)
        {
            _contentHost.Clear();
            if (state == null)
            {
                return;
            }

            var presentation =
                FileTreeDetailContentCatalog.Resolve(
                    state.Extension);
            _contentHost.Add(
                presentation.CreateContent(state));
        }
    }
}
