using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using Ee4v.Core.Testing;
using Ee4v.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Tests
{
    public sealed class CoreFeatureRegistrationTests
    {
        [SetUp]
        public void SetUp()
        {
            Ee4vCoreTestReset.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            Ee4vCoreTestReset.ResetAll();
            Ee4vCoreTestReset.RecoverEditorState();
        }

        [Test]
        [FeatureTestCase(
            "registrar の並び順は安定している",
            "FeatureTestRegistry が order と表示名に基づいて安定した順序で descriptor を並べることを確認します。",
            order: 10)]
        public void FeatureTestRegistry_BuildDescriptors_SortsByOrderAndDisplayName()
        {
            var descriptors = FeatureTestRegistry.BuildDescriptors(new[]
            {
                typeof(BravoRegistrar),
                typeof(AlphaRegistrar)
            });

            Assert.That(descriptors.Select(item => item.FeatureScope), Is.EqualTo(new[] { "Alpha", "Bravo" }));
        }

        [Test]
        [FeatureTestCase(
            "assembly 名の重複を拒否する",
            "FeatureTestRegistry が同じ assembly 名を返す registrar の組み合わせをエラーとして扱うことを確認します。",
            order: 11)]
        public void FeatureTestRegistry_BuildDescriptors_RejectsDuplicateAssemblyNames()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                FeatureTestRegistry.BuildDescriptors(new[]
                {
                    typeof(DuplicateAssemblyRegistrarA),
                    typeof(DuplicateAssemblyRegistrarB)
                }));

            Assert.That(exception.Message, Does.Contain("assembly name"));
        }

        [Test]
        [FeatureTestCase(
            "feature 単位の assembly filter で実行する",
            "FeatureTestRunnerService が対象 feature の test assembly だけを filter に入れて実行要求することを確認します。",
            order: 12)]
        public void FeatureTestRunnerService_TryRun_UsesFeatureAssemblyFilter()
        {
            var gateway = new FakeRunnerGateway();
            using (var service = new FeatureTestRunnerService(gateway))
            {
                var descriptor = new FeatureTestDescriptor("Core", "Core", "Ee4v.Core.Tests.Editor");

                var started = service.TryRun(descriptor, out var errorMessage);
                var assemblyNames = ReadAssemblyNames(gateway.LastExecutionSettings);

                Assert.That(started, Is.True);
                Assert.That(errorMessage, Is.Null);
                Assert.That(assemblyNames, Is.EqualTo(new[] { "Ee4v.Core.Tests.Editor" }));
                Assert.That(service.GetRecord("Core").Status, Is.EqualTo(FeatureTestRunStatus.Running));
                Assert.That(service.GetRecord("Core").Message, Does.Contain("テスト実行を要求"));
            }
        }

        [Test]
        [FeatureTestCase(
            "実行中は二重実行を拒否する",
            "FeatureTestRunnerService が run 中の追加実行要求を拒否することを確認します。",
            order: 13)]
        public void FeatureTestRunnerService_TryRunAll_PreventsConcurrentRuns()
        {
            var gateway = new FakeRunnerGateway();
            using (var service = new FeatureTestRunnerService(gateway))
            {
                var descriptors = new[]
                {
                    new FeatureTestDescriptor("Core", "Core", "Ee4v.Core.Tests.Editor"),
                    new FeatureTestDescriptor("Phase1", "Phase1", "Ee4v.Phase1.Tests.Editor")
                };

                Assert.That(service.TryRunAll(descriptors, out _), Is.True);
                Assert.That(service.TryRunAll(descriptors, out var secondError), Is.False);
                Assert.That(secondError, Does.Contain("already"));
            }
        }

        [Test]
        [FeatureTestCase(
            "結果未報告の実行を失敗扱いにする",
            "Unity Test Runner が assembly 結果を返さない場合に失敗として記録することを確認します。",
            order: 14)]
        public void FeatureTestRunnerService_UpdatesRecords_WhenRunCompletesWithoutAssemblyResults()
        {
            var gateway = new FakeRunnerGateway();
            using (var service = new FeatureTestRunnerService(gateway))
            {
                var descriptor = new FeatureTestDescriptor("Core", "Core", "Ee4v.Core.Tests.Editor");

                Assert.That(service.TryRun(descriptor, out _), Is.True);
                gateway.TriggerRunStarted();
                gateway.TriggerRunFinished(null);

                var record = service.GetRecord("Core");
                Assert.That(record.Status, Is.EqualTo(FeatureTestRunStatus.Failed));
                Assert.That(record.Message, Does.Contain("assembly 結果"));
                Assert.That(record.FinishedAtUtc.HasValue, Is.True);
            }
        }

        [Test]
        [FeatureTestCase(
            "run 状態を reload 後も維持する",
            "FeatureTestRunnerService が session state から active run と record を復元し、reload 後も NotRun に戻さないことを確認します。",
            order: 14)]
        public void FeatureTestRunnerService_RestoresRunningStateAcrossServiceRecreation()
        {
            var descriptor = new FeatureTestDescriptor(
                "Core",
                "Core",
                "Ee4v.Core.Tests.Editor",
                testCases: new[]
                {
                    new FeatureTestCaseDescriptor("Case 1", "Description")
                });

            var gateway1 = new FakeRunnerGateway();
            using (var service1 = new FeatureTestRunnerService(gateway1))
            {
                Assert.That(service1.TryRun(descriptor, out _), Is.True);
                gateway1.TriggerRunStarted();
            }

            var gateway2 = new FakeRunnerGateway();
            using (var service2 = new FeatureTestRunnerService(gateway2))
            {
                var restored = service2.GetRecord("Core");

                Assert.That(service2.IsRunInProgress, Is.True);
                Assert.That(restored.Status, Is.EqualTo(FeatureTestRunStatus.Running));
                Assert.That(restored.Message, Does.Contain("実行"));
            }
        }

        [Test]
        [FeatureTestCase(
            "discover した test case は result key を持つ",
            "FeatureTestRegistry が現在の Core suite から検出した test case に result key を付与することを確認します。",
            order: 15)]
        public void FeatureTestRegistry_Refresh_PopulatesResultKeysForDiscoveredCases()
        {
            Ee4vCoreTestReset.RecoverEditorState();

            var descriptors = FeatureTestRegistry.Refresh();
            var coreDescriptor = descriptors.Single(item => item.FeatureScope == "Core");

            Assert.That(coreDescriptor.TestCases.Count, Is.GreaterThan(0));
            Assert.That(coreDescriptor.TestCases.All(testCase => !string.IsNullOrWhiteSpace(testCase.ResultKey)), Is.True);
        }

        [Test]
        [FeatureTestCase(
            "case status を reload 後も復元する",
            "FeatureTestRunnerService が保存済み case status を session state から復元することを確認します。",
            order: 16)]
        public void FeatureTestRunnerService_RestoresCaseStatusesAcrossServiceRecreation()
        {
            var gateway1 = new FakeRunnerGateway();
            using (var service1 = new FeatureTestRunnerService(gateway1))
            {
                var record = service1.GetRecord("Core");
                record.Status = FeatureTestRunStatus.Failed;
                record.Message = "persisted";
                record.DetailedResult = "persisted details";
                record.FailCount = 1;
                record.CaseStatuses["Ee4v.Core.Tests.Sample.Case1"] = FeatureTestRunStatus.Failed;

                InvokePrivate(service1, "SaveState");
            }

            var gateway2 = new FakeRunnerGateway();
            using (var service2 = new FeatureTestRunnerService(gateway2))
            {
                var restored = service2.GetRecord("Core");

                Assert.That(restored.Status, Is.EqualTo(FeatureTestRunStatus.Failed));
                Assert.That(restored.Message, Is.EqualTo("persisted"));
                Assert.That(restored.DetailedResult, Is.EqualTo("persisted details"));
                Assert.That(restored.CaseStatuses["Ee4v.Core.Tests.Sample.Case1"], Is.EqualTo(FeatureTestRunStatus.Failed));
            }
        }

        [Test]
        [FeatureTestCase(
            "Core reset が実行中 runner state を維持する",
            "Ee4vCoreTestReset.ResetAll が Core suite 実行中の FeatureTestRunnerService を消さないことを確認します。",
            order: 17)]
        public void Ee4vCoreTestReset_ResetAll_PreservesActiveRunnerState()
        {
            var gateway = new FakeRunnerGateway();
            using (var service = new FeatureTestRunnerService(gateway))
            {
                try
                {
                    var descriptor = new FeatureTestDescriptor("Core", "Core", "Ee4v.Core.Tests.Editor");
                    ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", service);

                    Assert.That(service.TryRun(descriptor, out _), Is.True);
                    Assert.That(service.IsRunInProgress, Is.True);

                    Ee4vCoreTestReset.ResetAll();

                    var preserved = ReflectionReset.GetStaticField(typeof(FeatureTestManagerWindow), "_runnerService");
                    Assert.That(preserved, Is.SameAs(service));
                    Assert.That(service.IsRunInProgress, Is.True);
                    Assert.That(service.GetRecord("Core").Status, Is.EqualTo(FeatureTestRunStatus.Running));
                }
                finally
                {
                    ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", null);
                    FeatureTestRunnerService.ClearPersistedState();
                }
            }
        }

        [Test]
        [FeatureTestCase(
            "Test Manager が Core と Phase1 を列挙する",
            "FeatureTestManagerWindow の再読込で登録済み suite が一覧に並ぶことを確認します。",
            order: 18)]
        public void FeatureTestManagerWindow_RefreshDescriptors_FindsCoreAndPhase1Registrars()
        {
            var window = ScriptableObject.CreateInstance<FeatureTestManagerWindow>();
            try
            {
                InvokePrivate(window, "RefreshDescriptors");
                var descriptors = (IList)GetPrivateField(window, "_descriptors");
                var scopes = descriptors.Cast<FeatureTestDescriptor>().Select(item => item.FeatureScope).ToArray();

                Assert.That(scopes, Does.Contain("Core"));
                Assert.That(scopes, Does.Contain("Phase1"));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(window);
            }
        }

        [Test]
        [FeatureTestCase(
            "Test Manager の UI Toolkit 画面が suite card を構築する",
            "FeatureTestManagerWindow が CreateGUI 後に登録済み suite ごとの card を UI Toolkit で構築することを確認します。",
            order: 19)]
        public void FeatureTestManagerWindow_CreateGUI_BuildsSuiteCards()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var window = ScriptableObject.CreateInstance<FeatureTestManagerWindow>();
            try
            {
                InvokePrivate(window, "RefreshDescriptors");
                InvokePrivate(window, "CreateGUI");

                var cards = window.rootVisualElement.Query<TestResultGroup>(className: "ee4v-test-manager__suite-card").ToList();

                Assert.That(cards.Count, Is.GreaterThanOrEqualTo(2));
                Assert.That(cards[0].Q<Alerts>(className: UiClassNames.TestResultGroupSummaryAlert).style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(window);
            }
        }

        [Test]
        [FeatureTestCase(
            "Test Manager の検索が suite を絞り込み一致項目を展開する",
            "FeatureTestManagerWindow の検索入力が suite を絞り込み、検索一致時にテストケース section を自動展開することを確認します。",
            order: 20)]
        public void FeatureTestManagerWindow_Search_FiltersAndExpandsMatchingSuite()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var window = ScriptableObject.CreateInstance<FeatureTestManagerWindow>();
            try
            {
                InvokePrivate(window, "RefreshDescriptors");
                InvokePrivate(window, "CreateGUI");

                var searchField = window.rootVisualElement.Q<SearchField>();
                var cards = window.rootVisualElement.Query<TestResultGroup>(className: "ee4v-test-manager__suite-card").ToList();

                searchField.Value = "Phase1";

                var visibleCards = cards.Where(card => card.style.display.value != DisplayStyle.None).ToList();
                Assert.That(visibleCards.Count, Is.EqualTo(1));

                var result = visibleCards[0];
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Expanded, Is.True);
            }
            finally
            {
                ScriptableObject.DestroyImmediate(window);
            }
        }

        [Test]
        [FeatureTestCase(
            "Test Manager が run 状態を UI に反映する",
            "FeatureTestManagerWindow が FeatureTestRunnerService の record 更新を badge と結果 alert に反映することを確認します。",
            order: 21)]
        public void FeatureTestManagerWindow_RefreshWindowState_ReflectsRunnerRecord()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var gateway = new FakeRunnerGateway();
            using (var service = new FeatureTestRunnerService(gateway))
            {
                ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", service);

                var descriptor = new FeatureTestDescriptor("Core", "Core", "Ee4v.Core.Tests.Editor");
                Assert.That(service.TryRun(descriptor, out _), Is.True);

                var window = ScriptableObject.CreateInstance<FeatureTestManagerWindow>();
                try
                {
                    InvokePrivate(window, "RefreshDescriptors");
                    InvokePrivate(window, "CreateGUI");

                    var searchField = window.rootVisualElement.Q<SearchField>();
                    searchField.Value = "Core";

                    InvokePrivate(window, "RefreshWindowState");

                    var visibleCard = window.rootVisualElement
                        .Query<TestResultGroup>(className: "ee4v-test-manager__suite-card")
                        .ToList()
                        .Single(card => card.style.display.value != DisplayStyle.None);

                    var result = visibleCard as TestResultGroup;
                    var summaryAlert = result.Q<Alerts>(className: UiClassNames.TestResultGroupSummaryAlert);
                    var message = summaryAlert.Q<UiTextElement>(className: UiClassNames.BannerMessage);
                    var runButton = result.Q<Button>(className: UiClassNames.TestResultGroupRunButton);

                    Assert.That(message.Text, Does.Contain("テスト実行を要求"));
                    Assert.That(message.Text, Does.Contain("Pass 0"));
                    Assert.That(result.Badge.style.display.value, Is.EqualTo(DisplayStyle.None));
                    Assert.That(summaryAlert.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(runButton.enabledSelf, Is.False);
                }
                finally
                {
                    ScriptableObject.DestroyImmediate(window);
                    ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", null);
                }
            }
        }

        [Test]
        [FeatureTestCase(
            "Test Manager が counts 不在時は結果メッセージを表示する",
            "FeatureTestManagerWindow が counts 0 件の失敗でも record message を結果 banner に表示することを確認します。",
            order: 22)]
        public void FeatureTestManagerWindow_RefreshWindowState_ShowsRecordMessageWhenCountsAreMissing()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var gateway = new FakeRunnerGateway();
            using (var service = new FeatureTestRunnerService(gateway))
            {
                ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", service);

                var window = ScriptableObject.CreateInstance<FeatureTestManagerWindow>();
                try
                {
                    InvokePrivate(window, "RefreshDescriptors");

                    var record = service.GetRecord("Core");
                    record.Status = FeatureTestRunStatus.Failed;
                    record.Message = "Unity Test Runner は終了しましたが、この suite の assembly 結果を返しませんでした。";
                    record.DetailedResult = "Summary\nUnity Test Runner は終了しましたが、この suite の assembly 結果を返しませんでした。";
                    record.PassCount = 0;
                    record.FailCount = 0;
                    record.SkipCount = 0;
                    record.InconclusiveCount = 0;
                    record.DurationSeconds = 0d;

                    InvokePrivate(window, "CreateGUI");

                    var searchField = window.rootVisualElement.Q<SearchField>();
                    searchField.Value = "Core";

                    InvokePrivate(window, "RefreshWindowState");

                    var visibleCard = window.rootVisualElement
                        .Query<TestResultGroup>(className: "ee4v-test-manager__suite-card")
                        .ToList()
                        .Single(card => card.style.display.value != DisplayStyle.None);

                    var summaryAlert = visibleCard.Q<Alerts>(className: UiClassNames.TestResultGroupSummaryAlert);
                    var message = summaryAlert.Q<UiTextElement>(className: UiClassNames.BannerMessage);

                    Assert.That(summaryAlert.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(message.Text, Does.Contain("assembly 結果"));
                }
                finally
                {
                    ScriptableObject.DestroyImmediate(window);
                    ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", null);
                }
            }
        }

        [Test]
        [FeatureTestCase(
            "Test Manager が詳細結果欄を表示する",
            "FeatureTestManagerWindow が record に詳細結果がある場合、copy 可能なテキスト領域へ反映することを確認します。",
            order: 23)]
        public void FeatureTestManagerWindow_RefreshWindowState_ShowsDetailedResultField()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var gateway = new FakeRunnerGateway();
            using (var service = new FeatureTestRunnerService(gateway))
            {
                ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", service);

                var window = ScriptableObject.CreateInstance<FeatureTestManagerWindow>();
                try
                {
                    InvokePrivate(window, "RefreshDescriptors");

                    var record = service.GetRecord("Core");
                    record.Status = FeatureTestRunStatus.Failed;
                    record.Message = "failed";
                    record.DetailedResult = "Failure Details\nja-JP/Core: testing.window.copy";
                    record.FailCount = 1;

                    InvokePrivate(window, "CreateGUI");

                    var searchField = window.rootVisualElement.Q<SearchField>();
                    searchField.Value = "Core";

                    InvokePrivate(window, "RefreshWindowState");

                    var visibleCard = window.rootVisualElement
                        .Query<TestResultGroup>(className: "ee4v-test-manager__suite-card")
                        .ToList()
                        .Single(card => card.style.display.value != DisplayStyle.None);

                    var detailsPanel = visibleCard.Q<VisualElement>(className: UiClassNames.TestResultGroupDetailsPanel);
                    var detailsField = visibleCard.Q<CopyableTextArea>(className: UiClassNames.TestResultGroupDetailsField);
                    var copyButton = detailsField.Q<Button>(className: UiClassNames.CopyableTextAreaCopyButton);
                    var textField = detailsField.Q<TextField>(className: UiClassNames.CopyableTextAreaField);

                    Assert.That(detailsPanel, Is.Not.Null);
                    Assert.That(detailsPanel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(detailsField, Is.Not.Null);
                    Assert.That(copyButton.text, Is.EqualTo(I18N.Get("testing.window.copy")));
                    Assert.That(textField.value, Does.Contain("testing.window.copy"));
                }
                finally
                {
                    ScriptableObject.DestroyImmediate(window);
                    ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", null);
                }
            }
        }

        [Test]
        [FeatureTestCase(
            "Test Manager が成功時の詳細結果欄を隠す",
            "FeatureTestManagerWindow が passed record の message を詳細結果欄へは表示しないことを確認します。",
            order: 24)]
        public void FeatureTestManagerWindow_RefreshWindowState_HidesDetailedResultForPassedRecord()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var gateway = new FakeRunnerGateway();
            using (var service = new FeatureTestRunnerService(gateway))
            {
                ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", service);

                var window = ScriptableObject.CreateInstance<FeatureTestManagerWindow>();
                try
                {
                    InvokePrivate(window, "RefreshDescriptors");

                    var record = service.GetRecord("Core");
                    record.Status = FeatureTestRunStatus.Passed;
                    record.Message = "登録されたテストはすべて成功しました。";
                    record.DetailedResult = "Case Results\n- dummy: Passed";
                    record.PassCount = 1;

                    InvokePrivate(window, "CreateGUI");

                    var searchField = window.rootVisualElement.Q<SearchField>();
                    searchField.Value = "Core";

                    InvokePrivate(window, "RefreshWindowState");

                    var visibleCard = window.rootVisualElement
                        .Query<TestResultGroup>(className: "ee4v-test-manager__suite-card")
                        .ToList()
                        .Single(card => card.style.display.value != DisplayStyle.None);

                    var detailsPanel = visibleCard.Q<VisualElement>(className: UiClassNames.TestResultGroupDetailsPanel);

                    Assert.That(detailsPanel, Is.Not.Null);
                    Assert.That(detailsPanel.style.display.value, Is.EqualTo(DisplayStyle.None));
                }
                finally
                {
                    ScriptableObject.DestroyImmediate(window);
                    ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", null);
                }
            }
        }

        [Test]
        [FeatureTestCase(
            "Test Manager がケース別バッジを優先表示する",
            "FeatureTestManagerWindow が suite 全体の失敗状態ではなく case ごとの結果バッジを表示することを確認します。",
            order: 25)]
        public void FeatureTestManagerWindow_RefreshWindowState_UsesPerCaseStatuses()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var gateway = new FakeRunnerGateway();
            using (var service = new FeatureTestRunnerService(gateway))
            {
                ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", service);

                var window = ScriptableObject.CreateInstance<FeatureTestManagerWindow>();
                try
                {
                    InvokePrivate(window, "RefreshDescriptors");
                    var descriptors = (IList)GetPrivateField(window, "_descriptors");
                    var coreDescriptor = descriptors.Cast<FeatureTestDescriptor>().Single(item => item.FeatureScope == "Core");
                    Assert.That(coreDescriptor.TestCases.Count, Is.GreaterThanOrEqualTo(2));

                    var record = service.GetRecord("Core");
                    record.Status = FeatureTestRunStatus.Failed;
                    record.Message = "mixed";
                    record.PassCount = 14;
                    record.FailCount = 2;
                    record.CaseStatuses[coreDescriptor.TestCases[0].ResultKey] = FeatureTestRunStatus.Passed;
                    record.CaseStatuses[coreDescriptor.TestCases[1].ResultKey] = FeatureTestRunStatus.Failed;

                    InvokePrivate(window, "CreateGUI");

                    var searchField = window.rootVisualElement.Q<SearchField>();
                    searchField.Value = "Core";

                    InvokePrivate(window, "RefreshWindowState");

                    var visibleCard = window.rootVisualElement
                        .Query<TestResultGroup>(className: "ee4v-test-manager__suite-card")
                        .ToList()
                        .Single(card => card.style.display.value != DisplayStyle.None);

                    var caseCards = visibleCard
                        .Q<VisualElement>(className: UiClassNames.TestResultGroupCasesBody)
                        .Query<InfoCard>(className: UiClassNames.TestResultGroupCaseCard)
                        .ToList();

                    var firstCaseBadge = caseCards[0].Badge.Q<UiTextElement>(className: UiClassNames.StatusBadge);
                    var secondCaseBadge = caseCards[1].Badge.Q<UiTextElement>(className: UiClassNames.StatusBadge);

                    Assert.That(firstCaseBadge.Text, Is.EqualTo(I18N.Get("testing.status.passed")));
                    Assert.That(secondCaseBadge.Text, Is.EqualTo(I18N.Get("testing.status.failed")));
                }
                finally
                {
                    ScriptableObject.DestroyImmediate(window);
                    ReflectionReset.SetStaticField(typeof(FeatureTestManagerWindow), "_runnerService", null);
                }
            }
        }

        [Test]
        [FeatureTestCase(
            "Test Manager が load error を alert で表示する",
            "FeatureTestManagerWindow が suite 読み込みエラー時に state alert をエラー表示へ切り替えることを確認します。",
            order: 26)]
        public void FeatureTestManagerWindow_RefreshWindowState_ShowsLoadErrorAlert()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var window = ScriptableObject.CreateInstance<FeatureTestManagerWindow>();
            try
            {
                InvokePrivate(window, "CreateGUI");
                SetPrivateField(window, "_loadError", "boom");
                InvokePrivate(window, "RefreshWindowState");

                var alert = window.rootVisualElement.Q<Alerts>(className: "ee4v-test-manager__state-alert");

                Assert.That(alert, Is.Not.Null);
                Assert.That(alert.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(window);
            }
        }

        [Test]
        [FeatureTestCase(
            "Core の static 状態を reset できる",
            "Ee4vCoreTestReset が SettingApi と InjectorApi の static 登録状態をクリアすることを確認します。",
            order: 27)]
        public void Ee4vCoreTestReset_ResetAll_ClearsStaticRegistrationsAndHandlers()
        {
            var definition = new SettingDefinition<bool>(
                "tests.reset.flag",
                SettingScope.User,
                "settings.section.localization",
                "settings.language.label",
                "settings.language.tooltip",
                defaultValue: true);
            SettingApi.Register(definition);
            SettingApi.Set(definition, false, saveImmediately: false);
            SettingApi.Changed += OnSettingChanged;

            InjectorApi.Register(new ItemInjectionRegistration(
                "tests.reset.injector",
                InjectionChannel.HierarchyItem,
                context => { }));

            Ee4vCoreTestReset.ResetAll();

            Assert.That(((IDictionary)ReflectionReset.GetStaticField(typeof(SettingApi), "Definitions")).Count, Is.EqualTo(0));
            Assert.That(((IList)ReflectionReset.GetStaticField(typeof(InjectorApi), "Registrations")).Count, Is.EqualTo(0));
            Assert.That(ReflectionReset.GetStaticField(typeof(SettingApi), "Changed"), Is.Null);
        }

        private static void OnSettingChanged(SettingDefinitionBase definition, object value)
        {
        }

        private static string[] ReadAssemblyNames(object executionSettings)
        {
            var filtersField = executionSettings.GetType().GetField("filters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var filters = (IEnumerable)filtersField.GetValue(executionSettings);
            foreach (var filter in filters)
            {
                var assemblyNamesField = filter.GetType().GetField("assemblyNames", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var assemblyNames = assemblyNamesField.GetValue(filter) as string[];
                if (assemblyNames != null)
                {
                    return assemblyNames;
                }
            }

            return Array.Empty<string>();
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private sealed class FakeRunnerGateway : IFeatureTestRunnerGateway
        {
            private ICallbacks _callbacks;

            public object LastExecutionSettings { get; private set; }

            public void RegisterCallbacks(ICallbacks callbacks)
            {
                _callbacks = callbacks;
            }

            public void UnregisterCallbacks(ICallbacks callbacks)
            {
                if (ReferenceEquals(_callbacks, callbacks))
                {
                    _callbacks = null;
                }
            }

            public string Execute(ExecutionSettings executionSettings)
            {
                LastExecutionSettings = executionSettings;
                return "fake-run-id";
            }

            public void TriggerRunFinished(ITestResultAdaptor result)
            {
                _callbacks.RunFinished(result);
            }

            public void TriggerRunStarted()
            {
                _callbacks.RunStarted(null);
            }
        }

        private sealed class AlphaRegistrar : IFeatureTestRegistrar
        {
            public FeatureTestDescriptor CreateDescriptor()
            {
                return new FeatureTestDescriptor("Alpha", "Alpha", "Tests.Alpha", order: 0);
            }
        }

        private sealed class BravoRegistrar : IFeatureTestRegistrar
        {
            public FeatureTestDescriptor CreateDescriptor()
            {
                return new FeatureTestDescriptor("Bravo", "Bravo", "Tests.Bravo", order: 1);
            }
        }

        private sealed class DuplicateAssemblyRegistrarA : IFeatureTestRegistrar
        {
            public FeatureTestDescriptor CreateDescriptor()
            {
                return new FeatureTestDescriptor("Alpha", "Alpha", "Tests.Duplicate", order: 0);
            }
        }

        private sealed class DuplicateAssemblyRegistrarB : IFeatureTestRegistrar
        {
            public FeatureTestDescriptor CreateDescriptor()
            {
                return new FeatureTestDescriptor("Bravo", "Bravo", "Tests.Duplicate", order: 1);
            }
        }
    }
}
