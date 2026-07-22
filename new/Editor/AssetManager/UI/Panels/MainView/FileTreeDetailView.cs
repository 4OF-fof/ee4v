using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class FileTreeDetailView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-file-detail";
        private const string NameClassName = "ee4v-asset-manager-file-detail__name";
        private readonly UiTextElement _nameLabel;

        public FileTreeDetailView()
        {
            AddToClassList(RootClassName);
            _nameLabel = UiTextFactory.Create(string.Empty, NameClassName);
            _nameLabel.SetWhiteSpace(WhiteSpace.Normal);
            Add(_nameLabel);
        }

        public void SetState(FileTreeDetailState state)
        {
            _nameLabel.SetText(state == null ? string.Empty : state.Name);
        }
    }
}
