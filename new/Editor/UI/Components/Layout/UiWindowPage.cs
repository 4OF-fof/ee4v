using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class UiWindowPageState
    {
        public UiWindowPageState(string title, string description, bool showToolbar = true)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            ShowToolbar = showToolbar;
        }

        public string Title { get; }

        public string Description { get; }

        public bool ShowToolbar { get; }
    }

    internal sealed class UiWindowPage : VisualElement
    {
        private readonly Label _titleLabel;
        private readonly Label _descriptionLabel;
        private readonly UiToolbarRow _toolbar;
        private readonly ScrollView _body;

        public UiWindowPage(UiWindowPageState state = null)
        {
            AddToClassList(UiClassNames.Page);

            var header = new VisualElement();
            header.AddToClassList(UiClassNames.PageHeader);

            _titleLabel = new Label();
            _titleLabel.AddToClassList(UiClassNames.PageTitle);

            _descriptionLabel = new Label();
            _descriptionLabel.AddToClassList(UiClassNames.PageDescription);

            header.Add(_titleLabel);
            header.Add(_descriptionLabel);

            _toolbar = new UiToolbarRow();
            _toolbar.AddToClassList(UiClassNames.PageToolbar);

            _body = new ScrollView();
            _body.AddToClassList(UiClassNames.PageBody);

            Add(header);
            Add(_toolbar);
            Add(_body);

            SetState(state ?? new UiWindowPageState(string.Empty, string.Empty));
        }

        public VisualElement ToolbarLeft
        {
            get { return _toolbar.LeftSlot; }
        }

        public VisualElement ToolbarRight
        {
            get { return _toolbar.RightSlot; }
        }

        public VisualElement Body
        {
            get { return _body.contentContainer; }
        }

        public void SetState(UiWindowPageState state)
        {
            state = state ?? new UiWindowPageState(string.Empty, string.Empty);

            _titleLabel.text = state.Title;
            _titleLabel.style.display = string.IsNullOrWhiteSpace(state.Title) ? DisplayStyle.None : DisplayStyle.Flex;

            _descriptionLabel.text = state.Description;
            _descriptionLabel.style.display = string.IsNullOrWhiteSpace(state.Description) ? DisplayStyle.None : DisplayStyle.Flex;

            _toolbar.style.display = state.ShowToolbar ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
