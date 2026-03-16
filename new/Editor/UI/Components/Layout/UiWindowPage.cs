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
        private readonly UiTextElement _titleLabel;
        private readonly UiTextElement _descriptionLabel;
        private readonly UiToolbarRow _toolbar;
        private readonly ScrollView _body;

        public UiWindowPage(UiWindowPageState state = null)
        {
            AddToClassList(UiClassNames.Page);

            var header = new VisualElement();
            header.AddToClassList(UiClassNames.PageHeader);

            _titleLabel = UiTextFactory.Create(string.Empty, UiClassNames.PageTitle);

            _descriptionLabel = UiTextFactory.Create(string.Empty, UiClassNames.PageDescription);

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

            _titleLabel.SetText(state.Title);

            _descriptionLabel.SetText(state.Description);

            _toolbar.style.display = state.ShowToolbar ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
