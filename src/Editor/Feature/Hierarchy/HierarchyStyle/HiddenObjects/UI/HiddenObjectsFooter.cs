using System;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectsFooter : ActionBar
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
        private readonly UiButton _selectAllButton;
        private readonly UiButton _clearSelectionButton;
        private readonly UiButton _revealButton;

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

            Actions.AddToClassList(ActionsClassName);
            _selectAllButton = new UiButton(
                text.SelectAllText,
                () => SelectAllRequested?.Invoke());
            _clearSelectionButton = new UiButton(
                text.ClearSelectionText,
                () => ClearSelectionRequested?.Invoke());
            _revealButton = new UiButton(
                text.RevealText,
                () => RevealRequested?.Invoke());
            _revealButton.AddToClassList(RevealClassName);

            Actions.Add(_selectAllButton);
            Actions.Add(_clearSelectionButton);
            Actions.Add(_revealButton);
            Leading.Add(_summary);
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
