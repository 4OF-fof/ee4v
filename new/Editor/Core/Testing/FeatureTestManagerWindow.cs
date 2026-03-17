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
                InfoCard card,
                StatusBadge statusBadge,
                Button runButton,
                UiTextElement scopeLabel,
                UiTextElement assemblyLabel,
                UiTextElement descriptionLabel,
                UiTextElement resultSummaryLabel,
                UiTextElement countsLabel,
                Alerts resultAlert,
                CollapsibleSection testCasesSection,
                string searchText)
            {
                Descriptor = descriptor;
                Card = card;
                StatusBadge = statusBadge;
                RunButton = runButton;
                ScopeLabel = scopeLabel;
                AssemblyLabel = assemblyLabel;
                DescriptionLabel = descriptionLabel;
                ResultSummaryLabel = resultSummaryLabel;
                CountsLabel = countsLabel;
                ResultAlert = resultAlert;
                TestCasesSection = testCasesSection;
                SearchText = searchText ?? string.Empty;
            }

            public FeatureTestDescriptor Descriptor { get; }

            public InfoCard Card { get; }

            public StatusBadge StatusBadge { get; }

            public Button RunButton { get; }

            public UiTextElement ScopeLabel { get; }

            public UiTextElement AssemblyLabel { get; }

            public UiTextElement DescriptionLabel { get; }

            public UiTextElement ResultSummaryLabel { get; }

            public UiTextElement CountsLabel { get; }

            public Alerts ResultAlert { get; }

            public CollapsibleSection TestCasesSection { get; }

            public string SearchText { get; }

            public bool UserExpanded { get; set; }
        }

        private static FeatureTestRunnerService _runnerService;

        private readonly List<FeatureTestDescriptor> _descriptors = new List<FeatureTestDescriptor>();
        private readonly List<DescriptorView> _descriptorViews = new List<DescriptorView>();
        private SearchField _searchField;
        private Button _refreshButton;
        private Button _runAllButton;
        private StatusBadge _overallStatusBadge;
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
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Interactive/collapsible-section.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/Testing/feature-test-manager-window.uss");

            var shell = new VisualElement();
            shell.AddToClassList("ee4v-test-manager__shell");

            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-test-manager__toolbar");

            var actions = new VisualElement();
            actions.AddToClassList("ee4v-test-manager__actions");

            _refreshButton = new Button(RefreshDescriptors)
            {
                text = I18N.Get("testing.window.refresh")
            };
            _refreshButton.AddToClassList("ee4v-test-manager__toolbar-button");

            _runAllButton = new Button(TryRunAll)
            {
                text = I18N.Get("testing.window.runAll")
            };
            _runAllButton.AddToClassList("ee4v-test-manager__toolbar-button");

            actions.Add(_refreshButton);
            actions.Add(_runAllButton);

            var toolbarStatus = new VisualElement();
            toolbarStatus.AddToClassList("ee4v-test-manager__toolbar-status");
            _overallStatusBadge = new StatusBadge();
            toolbarStatus.Add(_overallStatusBadge);

            toolbar.Add(actions);
            toolbar.Add(toolbarStatus);

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

            shell.Add(toolbar);
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

                if (view.TestCasesSection != null)
                {
                    view.TestCasesSection.SetExpanded(
                        !string.IsNullOrWhiteSpace(_searchQuery) && isMatch
                            ? true
                            : view.UserExpanded,
                        notify: false);
                }
            }

            RefreshStateAlert();
        }

        private void RefreshWindowState()
        {
            if (_refreshButton == null)
            {
                return;
            }

            var isRunning = _runnerService != null && _runnerService.IsRunInProgress;
            _refreshButton.SetEnabled(!isRunning);
            _runAllButton.SetEnabled(!isRunning && _descriptors.Count > 0);
            _overallStatusBadge.SetState(new StatusBadgeState(
                isRunning ? I18N.Get("testing.window.running") : I18N.Get("testing.window.idle"),
                isRunning ? UiStatusTone.Running : UiStatusTone.Idle));

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
            var card = new InfoCard(new InfoCardState(descriptor.DisplayName));
            card.AddToClassList("ee4v-test-manager__suite-card");

            var headerActions = new VisualElement();
            headerActions.AddToClassList("ee4v-test-manager__suite-header-actions");

            var badge = new StatusBadge();
            var runButton = new Button(() => TryRun(descriptor))
            {
                text = I18N.Get("testing.window.run")
            };
            runButton.AddToClassList("ee4v-test-manager__run-button");

            headerActions.Add(badge);
            headerActions.Add(runButton);
            card.HeaderRight.Add(headerActions);

            var metaBlock = new VisualElement();
            metaBlock.AddToClassList("ee4v-test-manager__meta");

            var scopeLabel = CreateBodyLabel();
            scopeLabel.AddToClassList("ee4v-test-manager__meta-label");
            var assemblyLabel = CreateBodyLabel();
            assemblyLabel.AddToClassList("ee4v-test-manager__meta-label");
            var descriptionLabel = CreateBodyLabel();
            descriptionLabel.AddToClassList("ee4v-test-manager__description");

            metaBlock.Add(scopeLabel);
            metaBlock.Add(assemblyLabel);
            metaBlock.Add(descriptionLabel);
            card.Body.Add(metaBlock);

            var resultSummaryLabel = CreateBodyLabel(string.Empty, UiClassNames.TestManagerResultSummary);
            resultSummaryLabel.AddToClassList("ee4v-test-manager__result-summary");
            var countsLabel = CreateBodyLabel();
            countsLabel.AddToClassList("ee4v-test-manager__counts");

            card.Body.Add(resultSummaryLabel);
            card.Body.Add(countsLabel);

            var resultAlert = new Alerts();
            resultAlert.AddToClassList("ee4v-test-manager__result-alert");
            card.Body.Add(resultAlert);

            CollapsibleSection testCasesSection = null;
            if (descriptor.TestCases != null && descriptor.TestCases.Count > 0)
            {
                testCasesSection = new CollapsibleSection(new CollapsibleSectionState(
                    I18N.Get("testing.window.tests"),
                    string.Format(I18N.Get("testing.window.testCasesMeta"), descriptor.TestCases.Count),
                    expanded: false));
                testCasesSection.AddToClassList("ee4v-test-manager__test-cases");

                for (var i = 0; i < descriptor.TestCases.Count; i++)
                {
                    var testCase = descriptor.TestCases[i];
                    var entry = new VisualElement();
                    entry.AddToClassList("ee4v-test-manager__test-case");

                    var title = CreateBodyLabel("- " + testCase.Title);
                    title.AddToClassList("ee4v-test-manager__test-case-title");
                    entry.Add(title);

                    if (!string.IsNullOrWhiteSpace(testCase.Description))
                    {
                        var description = CreateBodyLabel(testCase.Description);
                        description.AddToClassList("ee4v-test-manager__test-case-description");
                        entry.Add(description);
                    }

                    testCasesSection.Content.Add(entry);
                }

                card.Body.Add(testCasesSection);
            }

            var view = new DescriptorView(
                descriptor,
                card,
                badge,
                runButton,
                scopeLabel,
                assemblyLabel,
                descriptionLabel,
                resultSummaryLabel,
                countsLabel,
                resultAlert,
                testCasesSection,
                BuildSearchText(descriptor));

            if (testCasesSection != null)
            {
                testCasesSection.ExpandedChanged += expanded =>
                {
                    if (string.IsNullOrWhiteSpace(_searchQuery))
                    {
                        view.UserExpanded = expanded;
                    }
                };
            }

            UpdateDescriptorView(view);
            return view;
        }

        private void UpdateDescriptorView(DescriptorView view)
        {
            var descriptor = view.Descriptor;
            var record = _runnerService != null
                ? _runnerService.GetRecord(descriptor.FeatureScope)
                : new FeatureTestRunRecord();

            view.ScopeLabel.SetText(I18N.Get("testing.window.scope") + ": " + descriptor.FeatureScope);
            view.AssemblyLabel.SetText(I18N.Get("testing.window.assembly") + ": " + descriptor.AssemblyName);
            view.DescriptionLabel.SetText(descriptor.Description ?? string.Empty);
            view.DescriptionLabel.style.display = string.IsNullOrWhiteSpace(descriptor.Description)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            view.StatusBadge.SetState(new StatusBadgeState(FormatStatus(record), ToBadgeTone(record.Status)));
            view.RunButton.SetEnabled(_runnerService != null && !_runnerService.IsRunInProgress);

            view.ResultSummaryLabel.SetText(I18N.Get("testing.window.lastResult") + ": " + FormatStatus(record));
            view.CountsLabel.SetText(FormatCounts(record));

            if (string.IsNullOrWhiteSpace(record.Message))
            {
                view.ResultAlert.style.display = DisplayStyle.None;
            }
            else
            {
                view.ResultAlert.style.display = DisplayStyle.Flex;
                view.ResultAlert.SetState(new AlertsState(ToBannerTone(record.Status), string.Empty, record.Message));
            }
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

        private void TryRunAll()
        {
            EnsureRunnerService();
            if (_runnerService == null)
            {
                return;
            }

            if (!_runnerService.TryRunAll(_descriptors, out var errorMessage))
            {
                EditorUtility.DisplayDialog(I18N.Get("testing.window.title"), errorMessage, "OK");
                return;
            }

            RefreshWindowState();
        }

        private static string FormatStatus(FeatureTestRunRecord record)
        {
            switch (record.Status)
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

        private static string FormatCounts(FeatureTestRunRecord record)
        {
            if (record.Status == FeatureTestRunStatus.NotRun)
            {
                return I18N.Get("testing.window.notRunYet");
            }

            return string.Format(
                I18N.Get("testing.window.countsFormat"),
                record.PassCount,
                record.FailCount,
                record.SkipCount,
                record.InconclusiveCount,
                record.DurationSeconds);
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

        private static UiBannerTone ToBannerTone(FeatureTestRunStatus status)
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
                descriptor.Description ?? string.Empty
            };

            if (descriptor.TestCases != null)
            {
                for (var i = 0; i < descriptor.TestCases.Count; i++)
                {
                    parts.Add(descriptor.TestCases[i].Title ?? string.Empty);
                    parts.Add(descriptor.TestCases[i].Description ?? string.Empty);
                }
            }

            return string.Join("\n", parts);
        }

        private static UiTextElement CreateBodyLabel(string text = "", params string[] classNames)
        {
            var label = UiTextFactory.Create(text, classNames);
            label.SetWhiteSpace(WhiteSpace.Normal);
            return label;
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
