using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Testing
{
    internal sealed class FeatureTestManagerWindow : EditorWindow
    {
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

        private static FeatureTestRunnerService _runnerService;

        private readonly List<FeatureTestDescriptor> _descriptors = new List<FeatureTestDescriptor>();
        private readonly List<DescriptorView> _descriptorViews = new List<DescriptorView>();
        private SearchField _searchField;
        private Alerts _stateAlert;
        private ScrollView _suiteScrollView;
        private VisualElement _suiteListHost;
        private string _searchQuery = string.Empty;
        private string _loadError;

        [MenuItem("Debug/ee4v Test Manager")]
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
            root.AddToClassList(UiClassNames.Root);
            root.AddToClassList("ee4v-test-manager");

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/DataView/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/info-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/alerts.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/status-badge.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/copyable-text-area.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/test-result-group.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/Testing/feature-test-manager-window.uss");

            var shell = new VisualElement();
            shell.AddToClassList("ee4v-test-manager__shell");

            _searchField = new SearchField(new SearchFieldState(_searchQuery, I18N.Get("testing.window.searchPlaceholder")));
            _searchField.AddToClassList("ee4v-test-manager__search");
            _searchField.ValueChanged += ApplySearchQuery;

            _stateAlert = new Alerts();
            _stateAlert.AddToClassList("ee4v-test-manager__state-alert");

            _suiteScrollView = new ScrollView();
            _suiteScrollView.AddToClassList("ee4v-test-manager__scroll");

            _suiteListHost = new VisualElement();
            _suiteListHost.AddToClassList("ee4v-test-manager__list");
            _suiteScrollView.Add(_suiteListHost);

            shell.Add(_searchField);
            shell.Add(_stateAlert);
            shell.Add(_suiteScrollView);
            root.Add(shell);

            RebuildDescriptorViews();
        }

        private void RefreshDescriptors()
        {
            EnsureRunnerService();
            _descriptors.Clear();
            _loadError = null;

            try
            {
                _descriptors.AddRange(FeatureTestRegistry.Refresh());
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

            for (var i = 0; i < _descriptorViews.Count; i++)
            {
                UpdateDescriptorView(_descriptorViews[i]);
            }

            ApplySearchQuery(_searchQuery);
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
                detailsTitle: I18N.Get("testing.window.detailsTitle"),
                detailsText: BuildDetailedResult(record),
                detailsCopyButtonText: I18N.Get("testing.window.copy"),
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
                EditorUtility.DisplayDialog(I18N.Get("testing.window.title"), errorMessage, "OK");
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
                return !string.IsNullOrWhiteSpace(record.Message)
                    ? record.Message + "\n" + counts
                    : counts;
            }

            if (HasResultCounts(record))
            {
                return counts;
            }

            return !string.IsNullOrWhiteSpace(record.Message)
                ? record.Message
                : counts;
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

        private static string BuildDetailedResult(FeatureTestRunRecord record)
        {
            if (record == null || record.Status == FeatureTestRunStatus.NotRun)
            {
                return string.Empty;
            }

            if (!IsProblemStatus(record.Status))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(record.DetailedResult))
            {
                return record.DetailedResult;
            }

            if (record.Status == FeatureTestRunStatus.Running)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(record.Message))
            {
                return record.Message;
            }

            return HasResultCounts(record)
                ? string.Format(
                    I18N.Get("testing.window.countsFormat"),
                    record.PassCount,
                    record.FailCount,
                    record.SkipCount,
                    record.InconclusiveCount,
                    record.DurationSeconds)
                : string.Empty;
        }

        private static bool IsProblemStatus(FeatureTestRunStatus status)
        {
            return status == FeatureTestRunStatus.Failed
                || status == FeatureTestRunStatus.Skipped
                || status == FeatureTestRunStatus.Inconclusive;
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
                    GetCategoryDisplayLabel(testCases[i].Category, includeStandard: false));
            }

            return items;
        }

        private static string BuildSuiteEyebrow(FeatureTestDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return string.Empty;
            }

            var categorySummary = BuildSuiteCategorySummary(descriptor);
            if (string.IsNullOrWhiteSpace(categorySummary))
            {
                return descriptor.AssemblyName ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(descriptor.AssemblyName))
            {
                return categorySummary;
            }

            return categorySummary + " · " + descriptor.AssemblyName;
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

            _runnerService = new FeatureTestRunnerService();
            _runnerService.Changed += RefreshAllOpenWindows;
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
