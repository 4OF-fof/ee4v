using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ee4v.Mcp
{
    // CLI transport shares the MCP registry; feature modules do not depend on either protocol.
    internal sealed class Ee4vCliBridge : IDisposable
    {
        internal const int ApiVersion = 1;
        private readonly ConcurrentDictionary<string, Job> _jobs = new ConcurrentDictionary<string, Job>();
        private readonly string _session = Guid.NewGuid().ToString("N");
        private readonly string _token = CreateToken();
        private readonly string _projectPath;
        private readonly string _projectName;
        private readonly string _unityVersion;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly object _admission = new object();
        private string _descriptorPath;
        private bool _disposed;

        internal Ee4vCliBridge(string projectPath, string projectName, string unityVersion)
        {
            _projectPath = Path.GetFullPath(projectPath);
            _projectName = projectName;
            _unityVersion = unityVersion;
        }

        internal static string StateRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ee4v", "cli");

        internal void Publish(string endpoint)
        {
            var directory = Path.Combine(StateRoot, "instances");
            Directory.CreateDirectory(directory);
            _descriptorPath = Path.Combine(directory, _session + ".json");
            var value = new JObject
            {
                ["apiVersion"] = ApiVersion, ["session"] = _session,
                ["projectPath"] = _projectPath, ["projectName"] = _projectName,
                ["unityVersion"] = _unityVersion, ["endpoint"] = endpoint,
                ["pid"] = Process.GetCurrentProcess().Id, ["token"] = _token
            };
            var temporary = _descriptorPath + ".tmp";
            File.WriteAllText(temporary, value.ToString(Formatting.None), new UTF8Encoding(false));
            File.Move(temporary, _descriptorPath);
        }

        internal bool Authorize(string header)
        {
            var expected = "Bearer " + _token;
            if (header == null || header.Length != expected.Length) return false;
            var difference = 0;
            for (var index = 0; index < expected.Length; index++) difference |= expected[index] ^ header[index];
            return difference == 0;
        }

        internal JObject Health() => new JObject
        {
            ["ok"] = true, ["apiVersion"] = ApiVersion, ["session"] = _session,
            ["projectPath"] = _projectPath, ["projectName"] = _projectName,
            ["unityVersion"] = _unityVersion,
            ["runningJobs"] = _jobs.Values.Count(job => !job.Completed)
        };

        internal JObject Submit(JObject request)
        {
            if (request["operation"]?.Type != JTokenType.String ||
                request["arguments"] != null && !(request["arguments"] is JObject) ||
                request.Properties().Any(property => property.Name != "operation" && property.Name != "arguments"))
                throw new McpToolException("invalid_request", "operation must be a string and arguments an object.");

            Job job;
            lock (_admission)
            {
                if (_disposed) throw new McpToolException("server_stopped", "The Editor connection has stopped.");
                // Bound retained results, including potentially large preview images.
                foreach (var pair in _jobs.Where(pair => pair.Value.Completed &&
                    DateTime.UtcNow - pair.Value.FinishedAt > TimeSpan.FromMinutes(10)).ToArray())
                    if (_jobs.TryRemove(pair.Key, out var expired)) expired.Dispose();
                if (_jobs.Count >= 64)
                    throw new McpToolException("job_limit", "Retrieve and release completed jobs before submitting more operations.");
                job = new Job(_session + ":" + Guid.NewGuid().ToString("N"), (string)request["operation"], _lifetime.Token);
                _jobs[job.Id] = job;
                _ = Execute(job, (JObject)(request["arguments"]?.DeepClone() ?? new JObject()));
            }
            return job.Snapshot(false);
        }

        private static async Task Execute(Job job, JObject arguments)
        {
            try
            {
                var result = await UnityMainThreadDispatcher.Run(() =>
                {
                    job.MarkStarted();
                    return McpToolRegistry.Invoke(job.Operation, arguments, job.Token);
                }, job.Token).ConfigureAwait(false);
                job.Finish(result.ToProtocolValue(), result.IsError ? "failed" : "completed");
            }
            catch (OperationCanceledException)
            {
                job.Finish(McpToolResult.Error("cancelled", "The operation was cancelled; started operations may have applied changes.").ToProtocolValue(), "cancelled");
            }
            catch (Exception exception)
            {
                job.Finish(McpToolResult.Error("tool_failed", exception.Message).ToProtocolValue(), "failed");
            }
        }

        internal JObject ListJobs() => new JObject
        {
            ["jobs"] = new JArray(_jobs.Values.OrderBy(job => job.Id).Select(job => job.Snapshot(false)))
        };

        internal JObject GetJob(string id, string action)
        {
            if (!_jobs.TryGetValue(id, out var job))
                throw new McpToolException("job_not_found", "Job expired, was released, or belongs to a previous Editor domain. Do not repeat a write whose outcome is unknown.");
            if (action == "cancel") return job.Cancel();
            if (action == "release")
            {
                if (!job.Completed) throw new McpToolException("job_running", "Only completed jobs can be released.");
                if (_jobs.TryRemove(id, out var released)) released.Dispose();
                return new JObject { ["ok"] = true, ["released"] = id };
            }
            return job.Snapshot(true);
        }

        public void Dispose()
        {
            lock (_admission)
            {
                if (_disposed) return;
                _disposed = true;
            }
            _lifetime.Cancel();
            // The jobs dispose their sources once asynchronous invocations have unwound.
            foreach (var job in _jobs.Values) job.DisposeWhenFinished();
            _lifetime.Dispose();
            try { if (_descriptorPath != null && File.Exists(_descriptorPath)) File.Delete(_descriptorPath); }
            catch (IOException) { /* A stale descriptor is rejected by authenticated session discovery. */ }
            catch (UnauthorizedAccessException) { /* Listener shutdown must still complete. */ }
        }

        private static string CreateToken()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private sealed class Job : IDisposable
        {
            private readonly object _gate = new object();
            private readonly CancellationTokenSource _cancellation;
            private string _state = "queued";
            private bool _started;
            private bool _disposeWhenFinished;
            private JObject _result;
            internal Job(string id, string operation, CancellationToken lifetime)
            {
                Id = id; Operation = operation;
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            }
            internal string Id { get; }
            internal string Operation { get; }
            internal CancellationToken Token => _cancellation.Token;
            internal DateTime FinishedAt { get; private set; }
            internal bool Completed { get { lock (_gate) return _result != null; } }
            internal void MarkStarted()
            {
                lock (_gate) { Token.ThrowIfCancellationRequested(); _started = true; _state = "running"; }
            }
            internal void Finish(JObject result, string state)
            {
                lock (_gate)
                {
                    _result = result; _state = state; FinishedAt = DateTime.UtcNow;
                    if (_disposeWhenFinished) _cancellation.Dispose();
                }
            }
            internal JObject Snapshot(bool includeResult)
            {
                lock (_gate)
                {
                    var value = new JObject
                    {
                        ["id"] = Id, ["operation"] = Operation, ["state"] = _state,
                        ["started"] = _started, ["finishedAt"] = _result == null ? null : FinishedAt.ToString("O")
                    };
                    if (includeResult && _result != null) value["result"] = _result.DeepClone();
                    return value;
                }
            }
            internal JObject Cancel()
            {
                lock (_gate)
                {
                    if (_result == null) _cancellation.Cancel();
                    var value = Snapshot(false);
                    value["outcome"] = _result != null ? "already_finished" : _started ? "cancellation_requested_after_start" : "cancelled_before_start";
                    return value;
                }
            }
            internal void DisposeWhenFinished()
            {
                lock (_gate) { _disposeWhenFinished = true; if (_result != null) _cancellation.Dispose(); }
            }
            public void Dispose() { lock (_gate) _cancellation.Dispose(); }
        }
    }
}
