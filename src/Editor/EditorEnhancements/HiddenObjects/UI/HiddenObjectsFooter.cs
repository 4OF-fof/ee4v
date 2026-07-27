using System;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectsFooter : VisualElement
    {
        private const string RootClassName =
            "ee4v-hidden-objects-footer";
        private const string SummaryClassName =
            "ee4v-hidden-objects-footer__summary";
        private const string ActionsClassName =
            "ee4v-hidden-objects-footer__actions";
        private const string RevealClassName =
            "ee4v-hidden-objects-footer__reveal";

        private readonly UiTextElement _summary;
        private readonly Button _selectAllButton;
        private readonly Button _clearSelectionButton;
        private readonly Button _revealButton;

        public HiddenObjectsFooter(HiddenObjectsViewText text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            AddToClassList(RootClassName);
            _summary = UiTextFactory.Create(
                string.Empty,
                SummaryClassName,
                UiClassNames.SecondaryText);

            var actions = new VisualElement();
            actions.AddToClassList(ActionsClassName);
            _selectAllButton = new Button(
                () => SelectAllRequested?.Invoke())
            {
                text = text.SelectAllText
            };
            _clearSelectionButton = new Button(
                () => ClearSelectionRequested?.Invoke())
            {
                text = text.ClearSelectionText
            };
            _revealButton = new Button(
                () => RevealRequested?.Invoke())
            {
                text = text.RevealText
            };
            _revealButton.AddToClassList(RevealClassName);

            actions.Add(_selectAllButton);
            actions.Add(_clearSelectionButton);
            actions.Add(_revealButton);
            Add(_summary);
            Add(actions);
        }

        public event Action SelectAllRequested;

        public event Action ClearSelectionRequested;

        public event Action RevealRequested;

        public void SetState(
            string summaryText,
            int visibleHiddenCount,
            int selectedCount)
        {
            _summary.SetText(summaryText);
            _selectAllButton.SetEnabled(visibleHiddenCount > 0);
            _clearSelectionButton.SetEnabled(selectedCount > 0);
            _revealButton.SetEnabled(selectedCount > 0);
        }
    }
}
