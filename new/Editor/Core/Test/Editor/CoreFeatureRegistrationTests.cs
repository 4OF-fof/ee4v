using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using Ee4v.Core.Testing;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

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
                Assert.That(service.GetRecord("Core").Message, Does.Contain("Execution requested"));
            }
        }

        [Test]
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
                Assert.That(record.Message, Does.Contain("without reporting"));
                Assert.That(record.FinishedAtUtc.HasValue, Is.True);
            }
        }

        [Test]
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
