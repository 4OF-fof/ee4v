using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Testing.Application;
using Ee4v.Testing.Contracts;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Testing.UI
{
    internal sealed class FeatureTestManagerWindow : EditorWindow
    {
        private const string RootClassName = "ee4v-ui";
        private sealed class DescriptorView
        {
            public DescriptorView(
                FeatureTestDescriptor descriptor,
                TestResultGroup card,
                string searchText)
            {
                Descriptor = descriptor;
                Card = card;
                SearchText = searchText ?? string.Empty;
            }

            public FeatureTestDescriptor Descriptor { get; }

            public TestResultGroup Card { get; }

            public string SearchText { get; }

            public bool UserExpanded { get; set; }
        }

        private static IFeatureTestRunner _runnerService;

        private readonly List<FeatureTestDescriptor> _descriptors = new List<FeatureTestDescriptor>();
        private readonly List<DescriptorView> _descriptorViews = new List<DescriptorView>();
        private InfoCard _overallCard;
        private Alerts _overallAlert;
        private UiButton _runAllButton;
        private SearchField _searchField;
        private Alerts _stateAlert;
        private ScrollView _suiteScrollView;
        private VisualElement _suiteListHost;
        private string _searchQuery = string.Empty;
        private string _loadError;

        [MenuItem("ee4v/Debug/Test List")]
        private static void ShowWindow()
        {
            var window = GetWindow<FeatureTestManagerWindow>();
            window.titleContent = new GUIContent(I18N.Get("testing.window.title"));
            window.minSize = new Vector2(640f, 280f);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureRunnerService();
            RefreshDescriptors();
        }

        private void OnInspectorUpdate()
        {
            EnsureRunnerService();
            if (_runnerService != null && _runnerService.IsRunInProgress)
            {
                RefreshWindowState();
                Repaint();
            }
        }

        private void CreateGUI()
        {
            RebuildWindow();
            RefreshWindowState();
        }

        private void RebuildWindow()
        {
            titleContent = new GUIContent(I18N.Get("testing.window.title"));

            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList("ee4v-test-manager");

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/Button/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/SearchField/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/InfoCard/info-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/Alerts/alerts.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/StatusBadge/status-badge.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/CopyableTextArea/copyable-text-area.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Testing/UI/TestResultGroup/test-result-group.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Testing/UI/feature-test-manager-window.uss");

            var shell = new VisualElement();
            shell.AddToClassList("ee4v-test-manager__shell");

            _overallCard = new InfoCard();
            _overallCard.AddToClassList("ee4v-test-manager__overall");

            _runAllButton = new UiButton(
                new UiButtonState(
                    label: I18N.Get("testing.window.runAll")),
                TryRunAll);
            _runAllButton.AddToClassList("ee4v-test-manager__run-all");
            _overallCard.HeaderRight.Add(_runAllButton);

            _overallAlert = new Alerts();
            _overallAlert.AddToClassList("ee4v-test-manager__overall-alert");
            _overallCard.Body.Add(_overallAlert);

            _searchField = new SearchField(new SearchFieldState(
                _searchQuery,
                I18N.Get("testing.window.searchPlaceholder"),
                I18N.GetForScope("UI", "ui.search.tooltip"),
                I18N.GetForScope("UI", "ui.clear.tooltip")));
            _searchField.AddToClassList("ee4v-test-manager__search");
            _searchField.ValueChanged += ApplySearchQuery;

            _stateAlert = new Alerts();
            _stateAlert.AddToClassList("ee4v-test-manager__state-alert");

            _suiteScrollView = new ScrollView();
            _suiteScrollView.AddToClassList("ee4v-test-manager__scroll");

            _suiteListHost = new VisualElement();
            _suiteListHost.AddToClassList("ee4v-test-manager__list");
            _suiteScrollView.Add(_suiteListHost);

            shell.Add(_overallCard);
            shell.Add(_searchField);
            shell.Add(_stateAlert);
            shell.Add(_suiteScrollView);
            root.Add(shell);

            WindowToastApi.EnsureHost(this);

            RebuildDescriptorViews();
        }

        private void RefreshDescriptors()
        {
            EnsureRunnerService();
            _descriptors.Clear();
            _loadError = null;

            try
            {
                _descriptors.AddRange(TestingUiDependencies.Catalog.Refresh());
            }
            catch (Exception exception)
            {
                _loadError = exception.Message;
            }

            RebuildDescriptorViews();
            RefreshWindowState();
            Repaint();
        }

        private void ApplySearchQuery(string value)
        {
            _searchQuery = (value ?? string.Empty).Trim();

            for (var i = 0; i < _descriptorViews.Count; i++)
            {
                var view = _descriptorViews[i];
                var isMatch = IsDescriptorVisible(view);
                view.Card.style.display = isMatch ? DisplayStyle.Flex : DisplayStyle.None;

                view.Card.SetExpanded(
                    !string.IsNullOrWhiteSpace(_searchQuery) && isMatch
                        ? true
                        : view.UserExpanded,
                    notify: false);
            }

            RefreshStateAlert();
        }

        private void RefreshWindowState()
        {
            if (_suiteListHost == null)
            {
                return;
            }

            RefreshOverallSummary();

            for (var i = 0; i < _descriptorViews.Count; i++)
            {
                UpdateDescriptorView(_descriptorViews[i]);
            }

            ApplySearchQuery(_searchQuery);
        }

        private void RefreshOverallSummary()
        {
            if (_overallCard == null || _overallAlert == null || _runAllButton == null)
            {
                return;
            }

            var summary = FeatureTestOverallSummary.Build(
                _descriptors,
                featureScope => _runnerService != null
                    ? _runnerService.GetRecord(featureScope)
                    : null);
            _overallCard.SetState(new InfoCardState(
                I18N.Get("testing.window.overallSummary"),
                description: null,
                eyebrow: null,
                badgeState: new StatusBadgeState(
                    FormatStatus(summary.Status),
                    ToBadgeTone(summary.Status))));
            _overallAlert.SetState(new AlertsState(
                ToAlertTone(summary.Status),
                string.Empty,
                BuildOverallSummaryMessage(summary)));
            _runAllButton.SetInteractable(
                _runnerService != null
                && !_runnerService.IsRunInProgress
                && string.IsNullOrWhiteSpace(_loadError)
                && _descriptors.Count > 0);
        }

        private void RebuildDescriptorViews()
        {
            _descriptorViews.Clear();
            if (_suiteListHost == null)
            {
                return;
            }

            _suiteListHost.Clear();
            for (var i = 0; i < _descriptors.Count; i++)
            {
                var view = CreateDescriptorView(_descriptors[i]);
                _descriptorViews.Add(view);
                _suiteListHost.Add(view.Card);
            }
        }

        private DescriptorView CreateDescriptorView(FeatureTestDescriptor descriptor)
        {
            var card = new TestResultGroup();
            card.AddToClassList("ee4v-test-manager__suite-card");
            var view = new DescriptorView(
                descriptor,
                card,
                BuildSearchText(descriptor));

            card.RunRequested += () => TryRun(descriptor);
            card.ExpandedChanged += expanded =>
            {
                if (string.IsNullOrWhiteSpace(_searchQuery))
                {
                    view.UserExpanded = expanded;
                }
            };

            UpdateDescriptorView(view);
            return view;
        }

        private void UpdateDescriptorView(DescriptorView view)
        {
            var descriptor = view.Descriptor;
            var record = _runnerService != null
                ? _runnerService.GetRecord(descriptor.FeatureScope)
                : new FeatureTestRunRecord();

            view.Card.SetState(new TestResultGroupState(
                new InfoCardState(
                    descriptor.DisplayName,
                    descriptor.Description,
                    BuildSuiteEyebrow(descriptor)),
                runText: I18N.Get("testing.window.run"),
                runEnabled: _runnerService != null && !_runnerService.IsRunInProgress,
                summaryMessage: BuildSummaryMessage(record),
                summaryTone: ToAlertTone(record.Status),
                casesTitle: I18N.Get("testing.window.tests"),
                casesMeta: string.Format(I18N.Get("testing.window.testCasesMeta"), descriptor.TestCases != null ? descriptor.TestCases.Count : 0),
                expanded: view.UserExpanded,
                cases: ToCaseStates(descriptor.TestCases, record)));
        }

        private void RefreshStateAlert()
        {
            if (_stateAlert == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_loadError))
            {
                _stateAlert.style.display = DisplayStyle.Flex;
                _stateAlert.SetState(new AlertsState(UiBannerTone.Error, I18N.Get("testing.window.title"), _loadError));
                return;
            }

            if (_descriptors.Count == 0)
            {
                _stateAlert.style.display = DisplayStyle.Flex;
                _stateAlert.SetState(new AlertsState(UiBannerTone.Info, string.Empty, I18N.Get("testing.window.noSuites")));
                return;
            }

            if (!string.IsNullOrWhiteSpace(_searchQuery) && !_descriptorViews.Any(IsDescriptorVisible))
            {
                _stateAlert.style.display = DisplayStyle.Flex;
                _stateAlert.SetState(new AlertsState(UiBannerTone.Info, string.Empty, I18N.Get("testing.window.noMatches")));
                return;
            }

            _stateAlert.style.display = DisplayStyle.None;
        }

        private bool IsDescriptorVisible(DescriptorView view)
        {
            if (view == null)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(_searchQuery)
                || view.SearchText.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void TryRun(FeatureTestDescriptor descriptor)
        {
            EnsureRunnerService();
            if (_runnerService == null)
            {
                return;
            }

            if (!_runnerService.TryRun(descriptor, out var errorMessage))
            {
                WindowToastApi.Show(this, new WindowToastRequest(
                    WindowToastTone.Error,
                    I18N.Get("testing.window.title"),
                    ResolveRunnerMessage(
                        errorMessage,
                        string.Empty),
                    dismissTooltip: I18N.GetForScope(
                        "UI",
                        "ui.dismiss.tooltip")));
                return;
            }

            RefreshWindowState();
        }

        private void TryRunAll()
        {
            EnsureRunnerService();
            if (_runnerService == null)
            {
                return;
            }

            if (!_runnerService.TryRunAll(_descriptors, out var errorMessage))
            {
                WindowToastApi.Show(this, new WindowToastRequest(
                    WindowToastTone.Error,
                    I18N.Get("testing.window.title"),
                    ResolveRunnerMessage(
                        errorMessage,
                        string.Empty),
                    dismissTooltip: I18N.GetForScope(
                        "UI",
                        "ui.dismiss.tooltip")));
                return;
            }

            RefreshWindowState();
        }

        private static string FormatStatus(FeatureTestRunRecord record)
        {
            return FormatStatus(record.Status);
        }

        private static string FormatStatus(FeatureTestRunStatus status)
        {
            switch (status)
            {
                case FeatureTestRunStatus.Running:
                    return I18N.Get("testing.status.running");
                case FeatureTestRunStatus.Passed:
                    return I18N.Get("testing.status.passed");
                case FeatureTestRunStatus.Failed:
                    return I18N.Get("testing.status.failed");
                case FeatureTestRunStatus.Skipped:
                    return I18N.Get("testing.status.skipped");
                case FeatureTestRunStatus.Inconclusive:
                    return I18N.Get("testing.status.inconclusive");
                case FeatureTestRunStatus.NotRun:
                default:
                    return I18N.Get("testing.status.notRun");
            }
        }

        private static string BuildSummaryMessage(FeatureTestRunRecord record)
        {
            if (record == null || record.Status == FeatureTestRunStatus.NotRun)
            {
                return string.Empty;
            }

            var counts = string.Format(
                I18N.Get("testing.window.countsFormat"),
                record.PassCount,
                record.FailCount,
                record.SkipCount,
                record.InconclusiveCount,
                record.DurationSeconds);

            if (record.Status == FeatureTestRunStatus.Running)
            {
                var message = ResolveRunnerMessage(
                    record.Message,
                    record.MessageArgument);
                return !string.IsNullOrWhiteSpace(message)
                    ? message + "\n" + counts
                    : counts;
            }

            if (HasResultCounts(record))
            {
                return counts;
            }

            var resolvedMessage = ResolveRunnerMessage(
                record.Message,
                record.MessageArgument);
            return !string.IsNullOrWhiteSpace(resolvedMessage)
                ? resolvedMessage
                : counts;
        }

        private static string BuildOverallSummaryMessage(
            FeatureTestOverallSummary summary)
        {
            var suites = string.Format(
                I18N.Get("testing.window.overallSuitesFormat"),
                summary.SuiteCount,
                summary.PassedSuiteCount,
                summary.FailedSuiteCount,
                summary.SkippedSuiteCount,
                summary.InconclusiveSuiteCount,
                summary.RunningSuiteCount,
                summary.NotRunSuiteCount);
            var counts = string.Format(
                I18N.Get("testing.window.countsFormat"),
                summary.PassCount,
                summary.FailCount,
                summary.SkipCount,
                summary.InconclusiveCount,
                summary.DurationSeconds);
            return suites + "\n" + counts;
        }

        private static string ResolveRunnerMessage(
            string message,
            string argument)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            switch (message)
            {
                case "testing.runner.requested":
                    return I18N.Get("testing.runner.requested");
                case "testing.runner.pending":
                    return I18N.Get("testing.runner.pending");
                case "testing.runner.running":
                    return I18N.Get("testing.runner.running");
                case "testing.runner.runningTest":
                    return I18N.Get("testing.runner.runningTest", argument);
                case "testing.runner.missingResult":
                    return I18N.Get("testing.runner.missingResult");
                case "testing.runner.missingStart":
                    return I18N.Get("testing.runner.missingStart");
                case "testing.runner.startTimeout":
                    return I18N.Get("testing.runner.startTimeout");
                case "testing.runner.heartbeatTimeout":
                    return I18N.Get("testing.runner.heartbeatTimeout");
                case "testing.runner.passed":
                    return I18N.Get("testing.runner.passed");
                case "testing.runner.failed":
                    return I18N.Get("testing.runner.failed");
                case "testing.runner.skipped":
                    return I18N.Get("testing.runner.skipped");
                case "testing.runner.inconclusive":
                    return I18N.Get("testing.runner.inconclusive");
                case "testing.runner.error.descriptorRequired":
                    return I18N.Get("testing.runner.error.descriptorRequired");
                case "testing.runner.error.noSuites":
                    return I18N.Get("testing.runner.error.noSuites");
                case "testing.runner.error.alreadyRunning":
                    return I18N.Get("testing.runner.error.alreadyRunning");
                default:
                    return message;
            }
        }

        private static bool HasResultCounts(FeatureTestRunRecord record)
        {
            return record != null
                && (record.PassCount > 0
                    || record.FailCount > 0
                    || record.SkipCount > 0
                    || record.InconclusiveCount > 0
                    || record.DurationSeconds > 0d);
        }

        private static string ExtractFailureDetails(string detailedResult)
        {
            return string.IsNullOrWhiteSpace(detailedResult)
                ? string.Empty
                : detailedResult.Trim();
        }

        private static string LocalizeStatusMarkers(string details)
        {
            return (details ?? string.Empty)
                .Replace(
                    "[Failed]",
                    "[" + I18N.Get("testing.status.failed") + "]")
                .Replace(
                    "[Skipped]",
                    "[" + I18N.Get("testing.status.skipped") + "]")
                .Replace(
                    "[Inconclusive]",
                    "[" + I18N.Get(
                        "testing.status.inconclusive") + "]");
        }

        private static UiStatusTone ToBadgeTone(FeatureTestRunStatus status)
        {
            switch (status)
            {
                case FeatureTestRunStatus.Running:
                    return UiStatusTone.Running;
                case FeatureTestRunStatus.Passed:
                    return UiStatusTone.Passed;
                case FeatureTestRunStatus.Failed:
                    return UiStatusTone.Failed;
                case FeatureTestRunStatus.Skipped:
                    return UiStatusTone.Skipped;
                case FeatureTestRunStatus.Inconclusive:
                    return UiStatusTone.Inconclusive;
                case FeatureTestRunStatus.NotRun:
                default:
                    return UiStatusTone.Idle;
            }
        }

        private static UiBannerTone ToAlertTone(FeatureTestRunStatus status)
        {
            switch (status)
            {
                case FeatureTestRunStatus.Failed:
                    return UiBannerTone.Error;
                case FeatureTestRunStatus.Skipped:
                case FeatureTestRunStatus.Inconclusive:
                    return UiBannerTone.Warning;
                case FeatureTestRunStatus.Running:
                case FeatureTestRunStatus.Passed:
                case FeatureTestRunStatus.NotRun:
                default:
                    return UiBannerTone.Info;
            }
        }

        private static string BuildSearchText(FeatureTestDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return string.Empty;
            }

            var parts = new List<string>
            {
                descriptor.DisplayName ?? string.Empty,
                descriptor.FeatureScope ?? string.Empty,
                descriptor.AssemblyName ?? string.Empty,
                descriptor.Description ?? string.Empty,
                BuildSuiteEyebrow(descriptor),
                BuildSuiteCategorySummary(descriptor)
            };

            if (descriptor.TestCases != null)
            {
                for (var i = 0; i < descriptor.TestCases.Count; i++)
                {
                    parts.Add(descriptor.TestCases[i].Title ?? string.Empty);
                    parts.Add(descriptor.TestCases[i].Description ?? string.Empty);
                    parts.Add(GetCategoryDisplayLabel(descriptor.TestCases[i].Category, includeStandard: true));
                }
            }

            return string.Join("\n", parts);
        }

        private static IReadOnlyList<TestResultGroupCaseState> ToCaseStates(IReadOnlyList<FeatureTestCaseDescriptor> testCases, FeatureTestRunRecord record)
        {
            if (testCases == null || testCases.Count == 0)
            {
                return Array.Empty<TestResultGroupCaseState>();
            }

            var items = new TestResultGroupCaseState[testCases.Count];
            for (var i = 0; i < testCases.Count; i++)
            {
                items[i] = new TestResultGroupCaseState(
                    testCases[i].Title,
                    testCases[i].Description,
                    ToCaseBadgeState(testCases[i], record),
                    string.Empty,
                    BuildCaseDetailsToggleText(testCases[i], record, testCases.Count),
                    BuildCaseDetailsText(testCases[i], record, testCases.Count),
                    BuildCaseDetailsCopyButtonText(testCases[i], record, testCases.Count));
            }

            return items;
        }

        private static string BuildCaseDetailsToggleText(FeatureTestCaseDescriptor testCase, FeatureTestRunRecord record, int totalCaseCount)
        {
            return string.IsNullOrWhiteSpace(BuildCaseDetailsText(testCase, record, totalCaseCount))
                ? string.Empty
                : I18N.Get("testing.window.failureDetailsTitle");
        }

        private static string BuildCaseDetailsText(FeatureTestCaseDescriptor testCase, FeatureTestRunRecord record, int totalCaseCount)
        {
            if (testCase == null || record == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(testCase.ResultKey)
                && record.CaseDetails.TryGetValue(testCase.ResultKey, out var details))
            {
                return FormatCaseDetailsText(
                    testCase,
                    LocalizeStatusMarkers(
                        ExtractFailureDetails(details)));
            }

            return totalCaseCount == 1
                ? FormatCaseDetailsText(
                    testCase,
                    LocalizeStatusMarkers(
                        ExtractFailureDetails(
                            record.DetailedResult)))
                : string.Empty;
        }

        private static string BuildCaseDetailsCopyButtonText(FeatureTestCaseDescriptor testCase, FeatureTestRunRecord record, int totalCaseCount)
        {
            return string.IsNullOrWhiteSpace(BuildCaseDetailsText(testCase, record, totalCaseCount))
                ? string.Empty
                : I18N.Get("testing.window.copy");
        }

        private static string FormatCaseDetailsText(FeatureTestCaseDescriptor testCase, string details)
        {
            if (testCase == null || string.IsNullOrWhiteSpace(details))
            {
                return string.Empty;
            }

            var sections = new List<string>();
            if (!string.IsNullOrWhiteSpace(testCase.Title))
            {
                sections.Add(
                    I18N.Get("testing.window.testLabel") +
                    "\n" +
                    testCase.Title);
            }

            if (!string.IsNullOrWhiteSpace(testCase.Description))
            {
                sections.Add(
                    I18N.Get(
                        "testing.window.descriptionLabel") +
                    "\n" +
                    testCase.Description);
            }

            sections.Add(details);
            return string.Join("\n\n", sections);
        }

        private static string BuildSuiteEyebrow(FeatureTestDescriptor descriptor)
        {
            return descriptor != null
                ? descriptor.AssemblyName ?? string.Empty
                : string.Empty;
        }

        private static string BuildSuiteCategorySummary(FeatureTestDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return string.Empty;
            }

            var categories = new List<FeatureTestCategory> { descriptor.Category };
            if (descriptor.TestCases != null)
            {
                for (var i = 0; i < descriptor.TestCases.Count; i++)
                {
                    categories.Add(descriptor.TestCases[i].Category);
                }
            }

            return string.Join(
                ", ",
                categories
                    .Distinct()
                    .Where(category => category != FeatureTestCategory.Standard)
                    .Select(category => GetCategoryDisplayLabel(category, includeStandard: false))
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .ToArray());
        }

        private static string GetCategoryDisplayLabel(FeatureTestCategory category, bool includeStandard)
        {
            switch (category)
            {
                case FeatureTestCategory.Ui:
                    return I18N.Get("testing.category.ui");
                case FeatureTestCategory.StaticAudit:
                    return I18N.Get("testing.category.staticAudit");
                case FeatureTestCategory.Standard:
                    return includeStandard ? I18N.Get("testing.category.standard") : string.Empty;
                default:
                    return category.ToString();
            }
        }

        private static StatusBadgeState ToCaseBadgeState(FeatureTestCaseDescriptor testCase, FeatureTestRunRecord record)
        {
            if (testCase == null || record == null || string.IsNullOrWhiteSpace(testCase.ResultKey))
            {
                return null;
            }

            if (!record.CaseStatuses.TryGetValue(testCase.ResultKey, out var status))
            {
                return null;
            }

            return new StatusBadgeState(FormatStatus(status), ToBadgeTone(status));
        }

        private static void EnsureRunnerService()
        {
            if (_runnerService != null)
            {
                return;
            }

            _runnerService = TestingUiDependencies.Runner;
            _runnerService.Changed += RefreshAllOpenWindows;
        }

        internal static void ResetForTests()
        {
            if (_runnerService == null ||
                _runnerService.IsRunInProgress)
            {
                return;
            }

            _runnerService.Changed -= RefreshAllOpenWindows;
            _runnerService = null;
        }

        private static void RefreshAllOpenWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<FeatureTestManagerWindow>();
            foreach (var window in windows)
            {
                window.RefreshWindowState();
                window.Repaint();
            }
        }
    }
}
