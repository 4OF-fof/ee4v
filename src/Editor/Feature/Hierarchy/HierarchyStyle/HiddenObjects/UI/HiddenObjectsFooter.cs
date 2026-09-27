using System;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectsFooter : ActionBar
    {
        private const string RootClassName =
            "ee4v-hidden-objects-footer";
        private const string CompactClassName =
            "ee4v-hidden-objects-footer--compact";
        private const float CompactWidth = 440f;
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
            _summary.SetWhiteSpace(WhiteSpace.NoWrap);

            Actions.AddToClassList(ActionsClassName);
            _selectAllButton = new UiButton(
                text.SelectAllText,
                () => SelectAllRequested?.Invoke());
            _selectAllButton.AddToClassList(
                "ee4v-hidden-objects-footer__select-all");
            _clearSelectionButton = new UiButton(
                text.ClearSelectionText,
                () => ClearSelectionRequested?.Invoke());
            _revealButton = new UiButton(
                text.RevealText,
                () => RevealRequested?.Invoke());
            _revealButton.AddToClassList(RevealClassName);

            var selectionActions = new VisualElement();
            selectionActions.AddToClassList(
                "ee4v-hidden-objects-footer__selection-actions");
            selectionActions.Add(_selectAllButton);
            selectionActions.Add(_clearSelectionButton);
            Actions.Add(selectionActions);
            Actions.Add(_revealButton);
            Leading.Add(_summary);
            RegisterCallback<GeometryChangedEvent>(evt =>
                EnableInClassList(CompactClassName,
                    evt.newRect.width < CompactWidth));
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
