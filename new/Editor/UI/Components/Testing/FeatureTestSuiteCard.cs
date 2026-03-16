using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class FeatureTestSuiteCaseState
    {
        public FeatureTestSuiteCaseState(string title, string description = "")
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string Title { get; }

        public string Description { get; }
    }

    internal sealed class FeatureTestSuiteCardState
    {
        public FeatureTestSuiteCardState(
            string title,
            string scope,
            string assemblyName,
            string description,
            UiMetaListState metaListState,
            IReadOnlyList<FeatureTestSuiteCaseState> cases,
            string runButtonText,
            Action onRun,
            bool canRun,
            string statusText,
            UiStatusTone statusTone,
            string resultTitle,
            string resultCounts,
            string resultMessage,
            UiBannerTone resultTone)
        {
            Title = title ?? string.Empty;
            Scope = scope ?? string.Empty;
            AssemblyName = assemblyName ?? string.Empty;
            Description = description ?? string.Empty;
            MetaListState = metaListState ?? new UiMetaListState(new UiMetaListItem[0]);
            Cases = cases ?? new FeatureTestSuiteCaseState[0];
            RunButtonText = runButtonText ?? string.Empty;
            OnRun = onRun;
            CanRun = canRun;
            StatusText = statusText ?? string.Empty;
            StatusTone = statusTone;
            ResultTitle = resultTitle ?? string.Empty;
            ResultCounts = resultCounts ?? string.Empty;
            ResultMessage = resultMessage ?? string.Empty;
            ResultTone = resultTone;
        }

        public string Title { get; }

        public string Scope { get; }

        public string AssemblyName { get; }

        public string Description { get; }

        public UiMetaListState MetaListState { get; }

        public IReadOnlyList<FeatureTestSuiteCaseState> Cases { get; }

        public string RunButtonText { get; }

        public Action OnRun { get; }

        public bool CanRun { get; }

        public string StatusText { get; }

        public UiStatusTone StatusTone { get; }

        public string ResultTitle { get; }

        public string ResultCounts { get; }

        public string ResultMessage { get; }

        public UiBannerTone ResultTone { get; }
    }

    internal sealed class FeatureTestSuiteCard : VisualElement
    {
        private readonly UiCard _card;
        private readonly StatusBadge _statusBadge;
        private readonly UiMetaList _metaList;
        private readonly Label _descriptionLabel;
        private readonly VisualElement _casesContainer;
        private readonly Label _resultTitleLabel;
        private readonly Label _resultCountsLabel;
        private readonly Alerts _resultBanner;
        private readonly Button _runButton;
        private Action _runAction;

        public FeatureTestSuiteCard(FeatureTestSuiteCardState state = null)
        {
            AddToClassList(UiClassNames.FeatureSuite);

            _card = new UiCard();
            _statusBadge = new StatusBadge();
            _metaList = new UiMetaList();
            _descriptionLabel = new Label();
            _descriptionLabel.AddToClassList(UiClassNames.CardDescription);

            _runButton = new Button();
            _runButton.style.minWidth = UiTokens.ActionButtonWidth;

            var headerActions = new UiActionRow(new UiActionRowState(true));
            headerActions.RightSlot.Add(_runButton);

            _card.HeaderRight.Add(_statusBadge);
            _card.Body.Add(_metaList);
            _card.Body.Add(_descriptionLabel);
            _card.Body.Add(headerActions);

            _casesContainer = new VisualElement();
            _casesContainer.AddToClassList(UiClassNames.FeatureSuiteCases);
            _card.Body.Add(_casesContainer);

            var resultContainer = new VisualElement();
            resultContainer.AddToClassList(UiClassNames.FeatureSuiteResult);

            _resultTitleLabel = new Label();
            _resultTitleLabel.AddToClassList(UiClassNames.FeatureSuiteResultTitle);

            _resultCountsLabel = new Label();
            _resultCountsLabel.AddToClassList(UiClassNames.FeatureSuiteResultCounts);

            _resultBanner = new Alerts();

            resultContainer.Add(_resultTitleLabel);
            resultContainer.Add(_resultCountsLabel);
            resultContainer.Add(_resultBanner);
            _card.Body.Add(resultContainer);

            Add(_card);

            SetState(state ?? new FeatureTestSuiteCardState(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                string.Empty,
                null,
                false,
                string.Empty,
                UiStatusTone.Idle,
                string.Empty,
                string.Empty,
                string.Empty,
                UiBannerTone.Info));
        }

        public void SetState(FeatureTestSuiteCardState state)
        {
            state = state ?? new FeatureTestSuiteCardState(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                string.Empty,
                null,
                false,
                string.Empty,
                UiStatusTone.Idle,
                string.Empty,
                string.Empty,
                string.Empty,
                UiBannerTone.Info);

            _card.SetState(new UiCardState(state.Title, string.Empty, state.Scope));
            _statusBadge.SetState(new StatusBadgeState(state.StatusText, state.StatusTone));
            _metaList.SetState(state.MetaListState);

            _descriptionLabel.text = state.Description;
            _descriptionLabel.style.display = string.IsNullOrWhiteSpace(state.Description) ? DisplayStyle.None : DisplayStyle.Flex;

            if (_runAction != null)
            {
                _runButton.clicked -= _runAction;
            }

            _runAction = state.OnRun;
            if (_runAction != null)
            {
                _runButton.clicked += _runAction;
            }

            _runButton.text = state.RunButtonText;
            _runButton.style.display = string.IsNullOrWhiteSpace(state.RunButtonText) ? DisplayStyle.None : DisplayStyle.Flex;
            _runButton.SetEnabled(state.CanRun && _runAction != null);

            RebuildCases(state.Cases);

            _resultTitleLabel.text = state.ResultTitle;
            _resultTitleLabel.style.display = string.IsNullOrWhiteSpace(state.ResultTitle) ? DisplayStyle.None : DisplayStyle.Flex;

            _resultCountsLabel.text = state.ResultCounts;
            _resultCountsLabel.style.display = string.IsNullOrWhiteSpace(state.ResultCounts) ? DisplayStyle.None : DisplayStyle.Flex;

            _resultBanner.SetState(new AlertsState(state.ResultTone, string.Empty, state.ResultMessage));
            _resultBanner.style.display = string.IsNullOrWhiteSpace(state.ResultMessage) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RebuildCases(IReadOnlyList<FeatureTestSuiteCaseState> cases)
        {
            _casesContainer.Clear();
            _casesContainer.style.display = cases != null && cases.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            if (cases == null)
            {
                return;
            }

            for (var i = 0; i < cases.Count; i++)
            {
                var testCase = cases[i];
                var row = new VisualElement();
                row.AddToClassList(UiClassNames.FeatureSuiteCase);

                var title = new Label(testCase.Title);
                title.AddToClassList(UiClassNames.FeatureSuiteCaseTitle);
                row.Add(title);

                if (!string.IsNullOrWhiteSpace(testCase.Description))
                {
                    var description = new Label(testCase.Description);
                    description.AddToClassList(UiClassNames.FeatureSuiteCaseDescription);
                    row.Add(description);
                }

                _casesContainer.Add(row);
            }
        }
    }
}
