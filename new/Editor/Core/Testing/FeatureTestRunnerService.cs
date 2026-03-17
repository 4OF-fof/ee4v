using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Ee4v.Core.Testing
{
    internal sealed class FeatureTestRunnerService : IDisposable
    {
        private const string SessionStateKey = "ee4v.core.testing.runner-service.state";
        private static readonly TimeSpan RunStartTimeout = TimeSpan.FromSeconds(15d);
        private static readonly TimeSpan RunHeartbeatTimeout = TimeSpan.FromSeconds(120d);

        private readonly IFeatureTestRunnerGateway _gateway;
        private readonly CallbackForwarder _callbacks;
        private readonly Dictionary<string, FeatureTestRunRecord> _records =
            new Dictionary<string, FeatureTestRunRecord>(StringComparer.OrdinalIgnoreCase);

        private ActiveRun _activeRun;

        public FeatureTestRunnerService()
            : this(new UnityFeatureTestRunnerGateway())
        {
        }

        internal FeatureTestRunnerService(IFeatureTestRunnerGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            LoadPersistedState();
            _callbacks = new CallbackForwarder(this);
            _gateway.RegisterCallbacks(_callbacks);
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        public event Action Changed;

        public bool IsRunInProgress
        {
            get { return _activeRun != null; }
        }

        public FeatureTestRunRecord GetRecord(string featureScope)
        {
            if (string.IsNullOrWhiteSpace(featureScope))
            {
                return null;
            }

            if (!_records.TryGetValue(featureScope, out var record))
            {
                record = new FeatureTestRunRecord();
                _records.Add(featureScope, record);
            }

            return record;
        }

        public bool TryRun(FeatureTestDescriptor descriptor, out string errorMessage)
        {
            if (descriptor == null)
            {
                errorMessage = "Descriptor is required.";
                return false;
            }

            return TryStartRun(new[] { descriptor }, out errorMessage);
        }

        public bool TryRunAll(IReadOnlyList<FeatureTestDescriptor> descriptors, out string errorMessage)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                errorMessage = "No test suites are registered.";
                return false;
            }

            return TryStartRun(descriptors, out errorMessage);
        }

        public void Dispose()
        {
            _gateway.UnregisterCallbacks(_callbacks);
            EditorApplication.update -= OnEditorUpdate;
        }

        internal static void ClearPersistedState()
        {
            SessionState.SetString(SessionStateKey, string.Empty);
        }

        private bool TryStartRun(IReadOnlyList<FeatureTestDescriptor> descriptors, out string errorMessage)
        {
            if (IsRunInProgress)
            {
                errorMessage = "A test run is already in progress.";
                return false;
            }

            foreach (var descriptor in descriptors)
            {
                var record = GetRecord(descriptor.FeatureScope);
                record.RunId = string.Empty;
                record.Status = FeatureTestRunStatus.Running;
                record.Message = "テスト実行を要求しました。Unity Test Runner の開始を待っています。";
                record.PassCount = 0;
                record.FailCount = 0;
                record.SkipCount = 0;
                record.InconclusiveCount = 0;
                record.DurationSeconds = 0d;
                record.FinishedAtUtc = null;
            }

            var settings = new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = descriptors.Select(descriptor => descriptor.AssemblyName).ToArray()
            })
            {
                runSynchronously = false
            };

            _activeRun = new ActiveRun(descriptors);

            try
            {
                var runId = _gateway.Execute(settings);
                _activeRun.RunId = runId ?? string.Empty;
                foreach (var descriptor in descriptors)
                {
                    GetRecord(descriptor.FeatureScope).RunId = _activeRun.RunId;
                }

                NotifyChanged();
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                FailActiveRun(exception.Message);
                errorMessage = exception.Message;
                return false;
            }
        }

        private void OnRunFinished(ITestResultAdaptor result)
        {
            if (_activeRun == null)
            {
                return;
            }

            var activeRun = _activeRun;
            _activeRun = null;

            var byAssembly = new Dictionary<string, ITestResultAdaptor>(StringComparer.OrdinalIgnoreCase);
            CollectAssemblyResults(result, byAssembly);

            foreach (var descriptor in activeRun.Descriptors)
            {
                var record = GetRecord(descriptor.FeatureScope);
                if (!byAssembly.TryGetValue(NormalizeAssemblyName(descriptor.AssemblyName), out var assemblyResult))
                {
                    record.Status = FeatureTestRunStatus.Failed;
                    record.Message = activeRun.HasStarted
                        ? "Unity Test Runner は終了しましたが、この suite の assembly 結果を返しませんでした。"
                        : "テスト実行は要求されましたが、この suite の開始通知が Unity Test Runner から返りませんでした。";
                    record.FinishedAtUtc = DateTime.UtcNow;
                    continue;
                }

                record.Status = ToStatus(assemblyResult);
                record.Message = BuildResultMessage(descriptor, assemblyResult);
                record.PassCount = assemblyResult.PassCount;
                record.FailCount = assemblyResult.FailCount;
                record.SkipCount = assemblyResult.SkipCount;
                record.InconclusiveCount = assemblyResult.InconclusiveCount;
                record.DurationSeconds = assemblyResult.Duration;
                record.FinishedAtUtc = assemblyResult.EndTime == default(DateTime)
                    ? DateTime.UtcNow
                    : assemblyResult.EndTime.ToUniversalTime();
            }

            NotifyChanged();
        }

        private void FailActiveRun(string message)
        {
            if (_activeRun == null)
            {
                return;
            }

            foreach (var descriptor in _activeRun.Descriptors)
            {
                var record = GetRecord(descriptor.FeatureScope);
                record.Status = FeatureTestRunStatus.Failed;
                record.Message = message ?? string.Empty;
                record.FinishedAtUtc = DateTime.UtcNow;
            }

            _activeRun = null;
            NotifyChanged();
        }

        private static void CollectAssemblyResults(ITestResultAdaptor result, IDictionary<string, ITestResultAdaptor> byAssembly)
        {
            if (result == null)
            {
                return;
            }

            if (result.Test != null && result.Test.IsTestAssembly)
            {
                byAssembly[NormalizeAssemblyName(result.Test.Name)] = result;
            }

            if (!result.HasChildren || result.Children == null)
            {
                return;
            }

            foreach (var child in result.Children)
            {
                CollectAssemblyResults(child, byAssembly);
            }
        }

        private static FeatureTestRunStatus ToStatus(ITestResultAdaptor result)
        {
            switch (result.TestStatus)
            {
                case TestStatus.Passed:
                    return FeatureTestRunStatus.Passed;
                case TestStatus.Skipped:
                    return FeatureTestRunStatus.Skipped;
                case TestStatus.Inconclusive:
                    return FeatureTestRunStatus.Inconclusive;
                case TestStatus.Failed:
                default:
                    return FeatureTestRunStatus.Failed;
            }
        }

        private static string NormalizeAssemblyName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return string.Empty;
            }

            return assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? assemblyName.Substring(0, assemblyName.Length - 4)
                : assemblyName;
        }

        private static string BuildResultMessage(FeatureTestDescriptor descriptor, ITestResultAdaptor assemblyResult)
        {
            if (!string.IsNullOrWhiteSpace(assemblyResult.Message))
            {
                return assemblyResult.Message;
            }

            var details = BuildRegisteredCaseSummary(descriptor);
            switch (ToStatus(assemblyResult))
            {
                case FeatureTestRunStatus.Passed:
                    return string.IsNullOrWhiteSpace(details)
                        ? "登録されたテストはすべて成功しました。"
                        : "登録されたテストはすべて成功しました。確認した内容:\n" + details;
                case FeatureTestRunStatus.Skipped:
                    return string.IsNullOrWhiteSpace(details)
                        ? "この suite はスキップされました。"
                        : "この suite はスキップされました。対象テスト:\n" + details;
                case FeatureTestRunStatus.Inconclusive:
                    return string.IsNullOrWhiteSpace(details)
                        ? "この suite は未確定の結果になりました。"
                        : "この suite は未確定の結果になりました。対象テスト:\n" + details;
                case FeatureTestRunStatus.Failed:
                default:
                    return string.IsNullOrWhiteSpace(details)
                        ? "この suite で失敗が発生しました。"
                        : "この suite で失敗が発生しました。確認対象:\n" + details;
            }
        }

        private static string BuildRegisteredCaseSummary(FeatureTestDescriptor descriptor)
        {
            if (descriptor == null || descriptor.TestCases == null || descriptor.TestCases.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(
                "\n",
                descriptor.TestCases.Select(testCase =>
                    string.IsNullOrWhiteSpace(testCase.Description)
                        ? "- " + testCase.Title
                        : "- " + testCase.Title + ": " + testCase.Description));
        }

        private void NotifyChanged()
        {
            SaveState();
            Changed?.Invoke();
        }

        private void OnRunStarted()
        {
            if (_activeRun == null)
            {
                return;
            }

            _activeRun.HasStarted = true;
            foreach (var descriptor in _activeRun.Descriptors)
            {
                var record = GetRecord(descriptor.FeatureScope);
                record.Status = FeatureTestRunStatus.Running;
                record.Message = "テストを実行中です。";
            }

            NotifyChanged();
        }

        private void OnTestStarted(ITestAdaptor test)
        {
            if (_activeRun == null || test == null)
            {
                return;
            }

            var testName = string.IsNullOrWhiteSpace(test.FullName) ? test.Name : test.FullName;
            _activeRun.LastTestName = testName ?? string.Empty;
            _activeRun.LastHeartbeatUtc = DateTime.UtcNow;

            foreach (var descriptor in _activeRun.Descriptors)
            {
                var record = GetRecord(descriptor.FeatureScope);
                record.Status = FeatureTestRunStatus.Running;
                record.Message = string.IsNullOrWhiteSpace(_activeRun.LastTestName)
                    ? "テストを実行中です。"
                    : "実行中: " + _activeRun.LastTestName;
            }

            NotifyChanged();
        }

        private void OnTestFinished(ITestResultAdaptor result)
        {
            if (_activeRun == null)
            {
                return;
            }

            _activeRun.LastHeartbeatUtc = DateTime.UtcNow;
        }

        private void OnEditorUpdate()
        {
            if (_activeRun == null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (!_activeRun.HasStarted && now - _activeRun.RequestedAtUtc >= TimeSpan.FromSeconds(5))
            {
                foreach (var descriptor in _activeRun.Descriptors)
                {
                    var record = GetRecord(descriptor.FeatureScope);
                    record.Message = "実行要求は送信済みですが、Unity Test Runner から開始通知がまだ返っていません。";
                }
            }

            if (!_activeRun.HasStarted && now - _activeRun.RequestedAtUtc >= RunStartTimeout)
            {
                FailActiveRun("Unity Test Runner の開始通知待ちがタイムアウトしました。");
                return;
            }

            if (_activeRun.HasStarted && now - _activeRun.LastHeartbeatUtc >= RunHeartbeatTimeout)
            {
                FailActiveRun("Unity Test Runner の進行通知が途切れたため、この実行を失敗として扱いました。");
                return;
            }

            if (now - _activeRun.LastUiNotifyUtc < TimeSpan.FromSeconds(0.5))
            {
                return;
            }

            _activeRun.LastUiNotifyUtc = now;
            NotifyChanged();
        }

        private sealed class ActiveRun
        {
            public ActiveRun(IReadOnlyList<FeatureTestDescriptor> descriptors)
                : this(
                    descriptors,
                    string.Empty,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    false,
                    string.Empty)
            {
            }

            public ActiveRun(
                IReadOnlyList<FeatureTestDescriptor> descriptors,
                string runId,
                DateTime requestedAtUtc,
                DateTime lastHeartbeatUtc,
                DateTime lastUiNotifyUtc,
                bool hasStarted,
                string lastTestName)
            {
                Descriptors = descriptors;
                RunId = runId ?? string.Empty;
                RequestedAtUtc = requestedAtUtc;
                LastHeartbeatUtc = lastHeartbeatUtc;
                LastUiNotifyUtc = lastUiNotifyUtc;
                HasStarted = hasStarted;
                LastTestName = lastTestName ?? string.Empty;
            }

            public IReadOnlyList<FeatureTestDescriptor> Descriptors { get; }

            public string RunId { get; set; }

            public DateTime RequestedAtUtc { get; }

            public DateTime LastHeartbeatUtc { get; set; }

            public DateTime LastUiNotifyUtc { get; set; }

            public bool HasStarted { get; set; }

            public string LastTestName { get; set; }
        }

        [Serializable]
        private sealed class PersistedState
        {
            public PersistedRecord[] records;
            public PersistedActiveRun activeRun;
        }

        [Serializable]
        private sealed class PersistedRecord
        {
            public string featureScope;
            public int status;
            public string runId;
            public string message;
            public int passCount;
            public int failCount;
            public int skipCount;
            public int inconclusiveCount;
            public double durationSeconds;
            public long finishedAtUtcTicks;
        }

        [Serializable]
        private sealed class PersistedActiveRun
        {
            public PersistedDescriptor[] descriptors;
            public string runId;
            public long requestedAtUtcTicks;
            public long lastHeartbeatUtcTicks;
            public long lastUiNotifyUtcTicks;
            public bool hasStarted;
            public string lastTestName;
        }

        [Serializable]
        private sealed class PersistedDescriptor
        {
            public string featureScope;
            public string displayName;
            public string assemblyName;
            public string description;
            public int order;
            public PersistedCase[] testCases;
        }

        [Serializable]
        private sealed class PersistedCase
        {
            public string title;
            public string description;
            public int order;
        }

        private void SaveState()
        {
            var state = new PersistedState
            {
                records = _records.Select(pair => new PersistedRecord
                {
                    featureScope = pair.Key,
                    status = (int)pair.Value.Status,
                    runId = pair.Value.RunId,
                    message = pair.Value.Message,
                    passCount = pair.Value.PassCount,
                    failCount = pair.Value.FailCount,
                    skipCount = pair.Value.SkipCount,
                    inconclusiveCount = pair.Value.InconclusiveCount,
                    durationSeconds = pair.Value.DurationSeconds,
                    finishedAtUtcTicks = pair.Value.FinishedAtUtc.HasValue ? pair.Value.FinishedAtUtc.Value.Ticks : 0L
                }).ToArray(),
                activeRun = _activeRun == null
                    ? null
                    : new PersistedActiveRun
                    {
                        descriptors = _activeRun.Descriptors.Select(ToPersistedDescriptor).ToArray(),
                        runId = _activeRun.RunId,
                        requestedAtUtcTicks = _activeRun.RequestedAtUtc.Ticks,
                        lastHeartbeatUtcTicks = _activeRun.LastHeartbeatUtc.Ticks,
                        lastUiNotifyUtcTicks = _activeRun.LastUiNotifyUtc.Ticks,
                        hasStarted = _activeRun.HasStarted,
                        lastTestName = _activeRun.LastTestName
                    }
            };

            SessionState.SetString(SessionStateKey, JsonUtility.ToJson(state));
        }

        private void LoadPersistedState()
        {
            _records.Clear();
            _activeRun = null;

            var json = SessionState.GetString(SessionStateKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            PersistedState state;
            try
            {
                state = JsonUtility.FromJson<PersistedState>(json);
            }
            catch
            {
                ClearPersistedState();
                return;
            }

            if (state == null)
            {
                return;
            }

            if (state.records != null)
            {
                for (var i = 0; i < state.records.Length; i++)
                {
                    var persisted = state.records[i];
                    if (persisted == null || string.IsNullOrWhiteSpace(persisted.featureScope))
                    {
                        continue;
                    }

                    _records[persisted.featureScope] = new FeatureTestRunRecord
                    {
                        Status = (FeatureTestRunStatus)persisted.status,
                        RunId = persisted.runId ?? string.Empty,
                        Message = persisted.message ?? string.Empty,
                        PassCount = persisted.passCount,
                        FailCount = persisted.failCount,
                        SkipCount = persisted.skipCount,
                        InconclusiveCount = persisted.inconclusiveCount,
                        DurationSeconds = persisted.durationSeconds,
                        FinishedAtUtc = persisted.finishedAtUtcTicks > 0L
                            ? new DateTime(persisted.finishedAtUtcTicks, DateTimeKind.Utc)
                            : (DateTime?)null
                    };
                }
            }

            if (state.activeRun == null || state.activeRun.descriptors == null || state.activeRun.descriptors.Length == 0)
            {
                return;
            }

            var descriptors = state.activeRun.descriptors
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.featureScope) && !string.IsNullOrWhiteSpace(item.displayName) && !string.IsNullOrWhiteSpace(item.assemblyName))
                .Select(ToDescriptor)
                .ToArray();
            if (descriptors.Length == 0)
            {
                return;
            }

            _activeRun = new ActiveRun(
                descriptors,
                state.activeRun.runId,
                FromTicks(state.activeRun.requestedAtUtcTicks),
                FromTicks(state.activeRun.lastHeartbeatUtcTicks),
                FromTicks(state.activeRun.lastUiNotifyUtcTicks),
                state.activeRun.hasStarted,
                state.activeRun.lastTestName);
        }

        private static PersistedDescriptor ToPersistedDescriptor(FeatureTestDescriptor descriptor)
        {
            return new PersistedDescriptor
            {
                featureScope = descriptor.FeatureScope,
                displayName = descriptor.DisplayName,
                assemblyName = descriptor.AssemblyName,
                description = descriptor.Description,
                order = descriptor.Order,
                testCases = descriptor.TestCases == null
                    ? Array.Empty<PersistedCase>()
                    : descriptor.TestCases.Select(testCase => new PersistedCase
                    {
                        title = testCase.Title,
                        description = testCase.Description,
                        order = testCase.Order
                    }).ToArray()
            };
        }

        private static FeatureTestDescriptor ToDescriptor(PersistedDescriptor persisted)
        {
            return new FeatureTestDescriptor(
                persisted.featureScope,
                persisted.displayName,
                persisted.assemblyName,
                persisted.description,
                persisted.order,
                persisted.testCases == null
                    ? Array.Empty<FeatureTestCaseDescriptor>()
                    : persisted.testCases.Select(testCase => new FeatureTestCaseDescriptor(
                        testCase.title,
                        testCase.description,
                        testCase.order)).ToArray());
        }

        private static DateTime FromTicks(long ticks)
        {
            return ticks > 0L
                ? new DateTime(ticks, DateTimeKind.Utc)
                : DateTime.UtcNow;
        }

        private sealed class CallbackForwarder : ICallbacks
        {
            private readonly FeatureTestRunnerService _owner;

            public CallbackForwarder(FeatureTestRunnerService owner)
            {
                _owner = owner;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                _owner.OnRunStarted();
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _owner.OnRunFinished(result);
            }

            public void TestStarted(ITestAdaptor test)
            {
                _owner.OnTestStarted(test);
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                _owner.OnTestFinished(result);
            }
        }
    }
}
