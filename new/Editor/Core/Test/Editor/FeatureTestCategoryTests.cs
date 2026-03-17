using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ee4v.Core.I18n;
using Ee4v.Core.Testing;
using Ee4v.UI;
using NUnit.Framework;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Tests
{
    public sealed class FeatureTestCategoryTests
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
            "discover した case からカテゴリを取得できる",
            "FeatureTestCaseDiscovery が FeatureTestCaseAttribute の category を test case descriptor に反映することを確認します。",
            order: 26)]
        public void FeatureTestCaseDiscovery_DiscoversStaticAuditCategory()
        {
            var cases = FeatureTestCaseDiscovery.Discover("Ee4v.StaticAudit.Tests.Editor");
            var descriptor = cases.Single(item => item.Title == "ローカライズに重複キーがない");

            Assert.That(descriptor.Category, Is.EqualTo(FeatureTestCategory.StaticAudit));
        }

        [Test]
        [FeatureTestCase(
            "run 状態復元時にカテゴリ情報を維持する",
            "FeatureTestRunnerService が session state から active run を復元する際に suite / case の category を保持することを確認します。",
            order: 27)]
        public void FeatureTestRunnerService_RestoresCategoriesAcrossServiceRecreation()
        {
            var descriptor = new FeatureTestDescriptor(
                "StaticAudit",
                "Static Audit",
                "Ee4v.StaticAudit.Tests.Editor",
                testCases: new[]
                {
                    new FeatureTestCaseDescriptor(
                        "Static Audit Case",
                        "Description",
                        resultKey: "Ee4v.Core.Tests.StaticAuditCase",
                        category: FeatureTestCategory.StaticAudit)
                },
                category: FeatureTestCategory.StaticAudit);

            var gateway1 = new FakeRunnerGateway();
            using (var service1 = new FeatureTestRunnerService(gateway1))
            {
                Assert.That(service1.TryRun(descriptor, out _), Is.True);
                gateway1.TriggerRunStarted();
            }

            var gateway2 = new FakeRunnerGateway();
            using (var service2 = new FeatureTestRunnerService(gateway2))
            {
                var activeRun = GetPrivateField(service2, "_activeRun");
                Assert.That(activeRun, Is.Not.Null);

                var descriptors = (IReadOnlyList<FeatureTestDescriptor>)activeRun
                    .GetType()
                    .GetProperty("Descriptors", BindingFlags.Instance | BindingFlags.Public)
                    .GetValue(activeRun, null);

                Assert.That(descriptors[0].Category, Is.EqualTo(FeatureTestCategory.StaticAudit));
                Assert.That(descriptors[0].TestCases[0].Category, Is.EqualTo(FeatureTestCategory.StaticAudit));
            }
        }

        [Test]
        [FeatureTestCase(
            "Test Manager が静的監査カテゴリで検索できる",
            "FeatureTestManagerWindow の検索が Static Audit カテゴリ文字列に反応し、suite / case のカテゴリ表示を維持することを確認します。",
            order: 28)]
        public void FeatureTestManagerWindow_Search_FindsStaticAuditCategory()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var window = ScriptableObject.CreateInstance<FeatureTestManagerWindow>();
            try
            {
                InvokePrivate(window, "RefreshDescriptors");
                InvokePrivate(window, "CreateGUI");

                var searchField = window.rootVisualElement.Q<SearchField>();
                searchField.Value = I18N.Get("testing.category.staticAudit");

                var visibleCard = window.rootVisualElement
                    .Query<TestResultGroup>(className: "ee4v-test-manager__suite-card")
                    .ToList()
                    .Single(card =>
                    {
                        if (card.style.display.value == DisplayStyle.None)
                        {
                            return false;
                        }

                        var title = card.Q<UiTextElement>(className: UiClassNames.InfoCardTitle);
                        return title != null && title.Text == "Static Audit";
                    });

                var suiteEyebrow = visibleCard.Q<UiTextElement>(className: UiClassNames.InfoCardEyebrow);
                var caseEyebrows = visibleCard
                    .Q<VisualElement>(className: UiClassNames.TestResultGroupCasesBody)
                    .Query<UiTextElement>(className: UiClassNames.InfoCardEyebrow)
                    .ToList();

                Assert.That(visibleCard.Expanded, Is.True);
                Assert.That(suiteEyebrow.Text, Does.Contain(I18N.Get("testing.category.staticAudit")));
                Assert.That(caseEyebrows.Any(item => item.Text == I18N.Get("testing.category.staticAudit")), Is.True);
            }
            finally
            {
                ScriptableObject.DestroyImmediate(window);
            }
        }

        [Test]
        [FeatureTestCase(
            "Refresh 後に Static Audit suite が列挙される",
            "FeatureTestRegistry.Refresh が Static Audit suite とその case を返すことを確認します。",
            order: 29)]
        public void FeatureTestRegistry_Refresh_ListsStaticAuditSuite()
        {
            Ee4vCoreTestReset.RecoverEditorState();

            var descriptors = FeatureTestRegistry.Refresh();
            var staticAuditDescriptor = descriptors.Single(item => item.FeatureScope == "StaticAudit");

            Assert.That(staticAuditDescriptor.Category, Is.EqualTo(FeatureTestCategory.StaticAudit));
            Assert.That(staticAuditDescriptor.TestCases.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(staticAuditDescriptor.TestCases.All(item => item.Category == FeatureTestCategory.StaticAudit), Is.True);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private sealed class FakeRunnerGateway : IFeatureTestRunnerGateway
        {
            private ICallbacks _callbacks;

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
                return "fake-run-id";
            }

            public void TriggerRunStarted()
            {
                _callbacks.RunStarted(null);
            }
        }
    }
}
