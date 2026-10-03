using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class PreviewPane : VisualElement
    {
        private readonly UiTextElement _title;

        public PreviewPane(string title)
        {
            AddToClassList("ee4v-ui-preview-pane");
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-ui-preview-pane__toolbar");
            _title = UiTextFactory.Create(title, UiClassNames.SectionTitle,
                "ee4v-ui-preview-pane__title");
            toolbar.Add(_title);
            Actions = new VisualElement();
            Actions.AddToClassList("ee4v-ui-preview-pane__actions");
            toolbar.Add(Actions);
            Add(toolbar);
            Content = new VisualElement();
            Content.AddToClassList("ee4v-ui-preview-pane__content");
            Add(Content);
        }

        public VisualElement Actions { get; }
        public VisualElement Content { get; }

        public void SetTitle(string title)
        {
            _title.SetText(title);
        }
    }
}
