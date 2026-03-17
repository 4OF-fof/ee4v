using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class TestResultGroupCaseState
    {
        public TestResultGroupCaseState(
            string title,
            string description = null,
            StatusBadgeState badgeState = null,
            string eyebrow = null)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            BadgeState = badgeState;
            Eyebrow = eyebrow ?? string.Empty;
        }

        public string Title { get; }

        public string Description { get; }

        public StatusBadgeState BadgeState { get; }

        public string Eyebrow { get; }
    }

    internal sealed class TestResultGroupState
    {
        public TestResultGroupState(
            InfoCardState cardState,
            string runText = null,
            bool runEnabled = true,
            string summaryMessage = null,
            UiBannerTone summaryTone = UiBannerTone.Info,
            string casesTitle = null,
            string casesMeta = null,
            bool expanded = false,
            IReadOnlyList<TestResultGroupCaseState> cases = null)
        {
            CardState = cardState ?? new InfoCardState(string.Empty);
            RunText = runText ?? string.Empty;
            RunEnabled = runEnabled;
            SummaryMessage = summaryMessage ?? string.Empty;
            SummaryTone = summaryTone;
            CasesTitle = casesTitle ?? string.Empty;
            CasesMeta = casesMeta ?? string.Empty;
            Expanded = expanded;
            Cases = cases ?? Array.Empty<TestResultGroupCaseState>();
        }

        public InfoCardState CardState { get; }

        public string RunText { get; }

        public bool RunEnabled { get; }

        public string SummaryMessage { get; }

        public UiBannerTone SummaryTone { get; }

        public string CasesTitle { get; }

        public string CasesMeta { get; }

        public bool Expanded { get; }

        public IReadOnlyList<TestResultGroupCaseState> Cases { get; }
    }

    internal sealed class TestResultGroup : InfoCard
    {
        private readonly Alerts _summaryAlert;
        private readonly Button _runButton;
        private readonly VisualElement _casesPanel;
        private readonly Button _casesToggle;
        private readonly Icon _casesChevron;
        private readonly UiTextElement _casesTitle;
        private readonly UiTextElement _casesMeta;
        private readonly VisualElement _casesBody;
        private TestResultGroupState _state;

        public TestResultGroup(TestResultGroupState state = null)
            : base(new InfoCardState(string.Empty))
        {
            AddToClassList(UiClassNames.TestResultGroup);

            _runButton = new Button(RaiseRunRequested);
            _runButton.AddToClassList(UiClassNames.TestResultGroupRunButton);
            HeaderRight.Add(_runButton);

            _summaryAlert = new Alerts();
            _summaryAlert.AddToClassList(UiClassNames.TestResultGroupSummaryAlert);

            _casesPanel = new VisualElement();
            _casesPanel.AddToClassList(UiClassNames.TestResultGroupCasesPanel);

            _casesToggle = new Button(ToggleExpanded);
            _casesToggle.AddToClassList(UiClassNames.TestResultGroupCasesToggle);

            _casesChevron = new Icon(IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureClosed, size: 12f));
            _casesChevron.AddToClassList(UiClassNames.TestResultGroupCasesChevron);
            _casesTitle = UiTextFactory.Create(string.Empty, UiClassNames.TestResultGroupCasesTitle);
            _casesMeta = UiTextFactory.Create(string.Empty, UiClassNames.TestResultGroupCasesMeta);

            var casesHeaderText = new VisualElement();
            casesHeaderText.style.flexGrow = 1f;
            casesHeaderText.style.flexDirection = FlexDirection.Row;
            casesHeaderText.style.alignItems = Align.Center;
            casesHeaderText.style.justifyContent = Justify.SpaceBetween;
            casesHeaderText.Add(_casesTitle);
            casesHeaderText.Add(_casesMeta);

            _casesToggle.Add(_casesChevron);
            _casesToggle.Add(casesHeaderText);

            _casesBody = new VisualElement();
            _casesBody.AddToClassList(UiClassNames.TestResultGroupCasesBody);

            Body.Add(_summaryAlert);
            _casesPanel.Add(_casesToggle);
            _casesPanel.Add(_casesBody);
            Body.Add(_casesPanel);

            SetState(state ?? new TestResultGroupState(new InfoCardState(string.Empty)));
        }

        public event Action<bool> ExpandedChanged;

        public event Action RunRequested;

        public bool Expanded { get; private set; }

        public void SetState(TestResultGroupState state)
        {
            _state = state ?? new TestResultGroupState(new InfoCardState(string.Empty));

            _runButton.text = _state.RunText;
            _runButton.style.display = string.IsNullOrWhiteSpace(_state.RunText) ? DisplayStyle.None : DisplayStyle.Flex;
            _runButton.SetEnabled(!string.IsNullOrWhiteSpace(_state.RunText) && _state.RunEnabled);

            base.SetState(_state.CardState);

            _summaryAlert.SetState(new AlertsState(_state.SummaryTone, string.Empty, _state.SummaryMessage));
            _summaryAlert.style.display = string.IsNullOrWhiteSpace(_state.SummaryMessage) ? DisplayStyle.None : DisplayStyle.Flex;

            _casesTitle.SetText(_state.CasesTitle);
            _casesMeta.SetText(_state.CasesMeta);
            _casesMeta.style.display = string.IsNullOrWhiteSpace(_state.CasesMeta) ? DisplayStyle.None : DisplayStyle.Flex;
            _casesPanel.style.display = _state.Cases.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            RebuildCases();
            SetExpanded(_state.Expanded, notify: false);
            RefreshLayout();
        }

        public void SetExpanded(bool expanded, bool notify = true)
        {
            Expanded = expanded;
            EnableInClassList(UiClassNames.TestResultGroupExpanded, expanded);
            _casesChevron.SetState(IconState.FromBuiltinIcon(
                expanded ? UiBuiltinIcon.DisclosureOpen : UiBuiltinIcon.DisclosureClosed,
                size: 12f));
            _casesBody.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshLayout();

            if (notify)
            {
                ExpandedChanged?.Invoke(expanded);
            }
        }

        private void RebuildCases()
        {
            _casesBody.Clear();
            for (var i = 0; i < _state.Cases.Count; i++)
            {
                var testCase = _state.Cases[i];
                var entry = new InfoCard(new InfoCardState(
                    testCase.Title,
                    testCase.Description,
                    testCase.Eyebrow,
                    testCase.BadgeState));
                entry.AddToClassList(UiClassNames.TestResultGroupCaseCard);
                _casesBody.Add(entry);
            }
        }

        private void ToggleExpanded()
        {
            SetExpanded(!Expanded);
        }

        private void RaiseRunRequested()
        {
            RunRequested?.Invoke();
        }
    }
}
