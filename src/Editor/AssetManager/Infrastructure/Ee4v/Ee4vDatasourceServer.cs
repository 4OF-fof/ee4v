using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ee4v.AssetManager.Infrastructure.Ee4v
{
    /// <summary>Loopback bridge for the BOOTH userscript. Owns folder files, never the database.</summary>
    public sealed partial class Ee4vDatasourceServer : IDisposable
    {
        public const int Port = 48197;
        public const string Endpoint = "http://127.0.0.1:48197";
        private const int MaximumRequestBytes = 1024 * 1024;
        private readonly string _library;
        private readonly string _downloads;
        private readonly string _itemsRoot;
        private readonly object _gate = new object();
        private readonly Dictionary<string, ImportJob> _jobs = new Dictionary<string, ImportJob>();
        private readonly ConcurrentQueue<string> _messages = new ConcurrentQueue<string>();
        private HttpListener _listener;
        private CancellationTokenSource _cancellation;
        private Task _worker;
        private string _token;

        public Ee4vDatasourceServer(string library, string downloads)
        {
            _library = ResolveDirectory(library);
            _downloads = ResolveDirectory(downloads);
            _itemsRoot = Path.Combine(_library, "Items");
        }

        public bool IsRunning => _listener != null && _listener.IsListening && _worker?.IsCompleted != true;

        public bool TryDequeueMessage(out string message) => _messages.TryDequeue(out message);

        public void Start()
        {
            if (IsRunning) return;
            if (_listener != null) throw new InvalidOperationException("Dispose the previous server before restarting.");
            if (!Directory.Exists(_downloads)) throw new DirectoryNotFoundException("Download directory was not found: " + _downloads);
            RejectLinks(_downloads);
            RejectLinks(_library);
            RejectLinks(_itemsRoot);
            Directory.CreateDirectory(_itemsRoot);
            var listener = new HttpListener();
            listener.Prefixes.Add(Endpoint + "/");
            try { listener.Start(); }
            catch { listener.Close(); throw; }
            using (var random = RandomNumberGenerator.Create())
            {
                var bytes = new byte[32];
                random.GetBytes(bytes);
                _token = Hex(bytes);
            }
            _cancellation = new CancellationTokenSource();
            _listener = listener;
            var cancellation = _cancellation;
            _worker = Task.Run(() => Run(listener, cancellation));
        }

        public void Dispose()
        {
            _cancellation?.Cancel();
            _listener?.Close();
            // The worker never calls Unity APIs. Drain it before a reload or path change.
            _worker?.GetAwaiter().GetResult();
            _cancellation?.Dispose();
            _listener = null;
            _cancellation = null;
            _worker = null;
            _jobs.Clear();
        }

        private async Task Run(HttpListener listener, CancellationTokenSource lifetime)
        {
            var cancellation = lifetime.Token;
            // Keep health/status requests responsive while copying large archives.
            var polling = Task.Run(async () =>
            {
                try
                {
                    while (!cancellation.IsCancellationRequested)
                    {
                        await Task.Delay(1500, cancellation).ConfigureAwait(false);
                        await Poll(cancellation).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    if (!cancellation.IsCancellationRequested)
                        _messages.Enqueue("ee4v datasource watcher stopped: " + exception.Message);
                    listener.Close();
                }
            });
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync().ConfigureAwait(false);
                    await Process(context, cancellation).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                if (!cancellation.IsCancellationRequested)
                    _messages.Enqueue("ee4v datasource server stopped: " + exception.Message);
            }
            finally
            {
                lifetime.Cancel();
                listener.Close();
                await polling.ConfigureAwait(false);
            }
        }

        private async Task Process(HttpListenerContext context, CancellationToken cancellation)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                using (timeout.Token.Register(() =>
                {
                    try { context.Response.Abort(); context.Request.InputStream.Close(); }
                    catch (ObjectDisposedException) { }
                    catch (HttpListenerException) { }
                }))
                try
                {
                    var request = context.Request;
                    var host = request.Headers["Host"];
                    var origin = request.Headers["Origin"];
                    if (request.RemoteEndPoint == null || !IPAddress.IsLoopback(request.RemoteEndPoint.Address) ||
                        (host != "127.0.0.1:" + Port && host != "localhost:" + Port) ||
                        (!string.IsNullOrEmpty(origin) &&
                         !Regex.IsMatch(origin, @"\Ahttps://(?:[a-z0-9-]+\.)?booth\.pm\z", RegexOptions.IgnoreCase)))
                    {
                        await Send(context.Response, 403, new { ok = false, error = "Invalid host or origin" }, timeout.Token).ConfigureAwait(false);
                        return;
                    }
                    var route = request.RawUrl;
                    if (request.HttpMethod == "GET" && route == "/health")
                    {
                        await Send(context.Response, 200, new { ok = true, rootFolderAvailable = true, token = _token }, timeout.Token).ConfigureAwait(false);
                        return;
                    }
                    if (request.HttpMethod != "POST" || request.Headers["X-EE4V-Bridge-Token"] != _token)
                    {
                        await Send(context.Response, 401, new { ok = false, error = "Invalid session token" }, timeout.Token).ConfigureAwait(false);
                        return;
                    }
                    var body = await ReadBody(request, timeout.Token).ConfigureAwait(false);
                    object result;
                    if (route == "/v1/status") { lock (_gate) result = Status(body); }
                    else if (route == "/v1/import") { lock (_gate) result = Import(body); }
                    else
                    {
                        await Send(context.Response, 404, new { ok = false, error = "Not found" }, timeout.Token).ConfigureAwait(false);
                        return;
                    }
                    await Send(context.Response, 200, result, timeout.Token).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    if (!timeout.IsCancellationRequested)
                    {
                        try { await Send(context.Response, 400, new { ok = false, error = exception.Message }, timeout.Token).ConfigureAwait(false); }
                        catch (Exception) { /* The browser may have disconnected. */ }
                    }
                }
                finally
                {
                    try { context.Response.Close(); }
                    catch (ObjectDisposedException) { }
                    catch (HttpListenerException) { }
                }
            }
        }

        private object Status(JObject payload)
        {
            var items = new JArray();
            foreach (var product in payload["items"] as JArray ?? new JArray())
            {
                var id = ProductId(product);
                var metadata = ReadMetadata(id);
                var results = new JArray();
                foreach (var input in product["downloads"] as JArray ?? new JArray())
                {
                    var download = NormalizeDownload(input);
                    _jobs.TryGetValue(id + "/" + download.Id, out var job);
                    results.Add(new JObject
                    {
                        ["id"] = download.Id, ["filename"] = download.Filename, ["downloadUrl"] = download.Url,
                        ["imported"] = Imported(metadata, download), ["pending"] = job != null && job.Watching,
                        ["error"] = job?.Error ?? string.Empty
                    });
                }
                items.Add(new JObject
                {
                    ["boothItemId"] = long.Parse(id, System.Globalization.CultureInfo.InvariantCulture),
                    ["itemUrl"] = product["itemUrl"]?.DeepClone(),
                    ["hasBoothMeta"] = ((JArray)metadata["files"]).Count > 0, ["downloads"] = results
                });
            }
            return new { ok = true, items };
        }

        private object Import(JObject payload)
        {
            var id = ProductId(payload["product"]);
            var download = NormalizeDownload(payload["download"]);
            var jobId = id + "/" + download.Id;
            if (Imported(ReadMetadata(id), download)) return new { ok = true, jobId, alreadyImported = true };
            if (_jobs.TryGetValue(jobId, out var previous) && previous.Watching)
                return new { ok = true, jobId, alreadyPending = true };
            if (_jobs.Values.Any(job => job.Watching && string.Equals(job.Download.Filename, download.Filename, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("Another pending import uses this filename; finish it first");
            _jobs[jobId] = new ImportJob
            {
                Id = id, Download = download, Product = (JObject)payload["product"].DeepClone(), Started = DateTime.UtcNow
            };
            return new { ok = true, jobId };
        }

        private static async Task<JObject> ReadBody(HttpListenerRequest request, CancellationToken cancellation)
        {
            if (request.ContentLength64 > MaximumRequestBytes) throw new IOException("Request body exceeds 1 MiB");
            using (var body = new MemoryStream())
            {
                var buffer = new byte[8192];
                int count;
                while ((count = await request.InputStream.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) > 0)
                {
                    if (body.Length + count > MaximumRequestBytes) throw new IOException("Request body exceeds 1 MiB");
                    body.Write(buffer, 0, count);
                }
                return JObject.Parse(Encoding.UTF8.GetString(body.ToArray()));
            }
        }

        private static async Task Send(HttpListenerResponse response, int status, object payload, CancellationToken cancellation)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
            response.StatusCode = status;
            response.ContentType = "application/json; charset=utf-8";
            response.Headers["Cache-Control"] = "no-store";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, cancellation).ConfigureAwait(false);
        }

        private static string ProductId(JToken product)
        {
            var id = (string)product?["boothItemId"] ?? string.Empty;
            if (!Regex.IsMatch(id, @"\A[1-9][0-9]{0,15}\z")) throw new IOException("Invalid BOOTH item id");
            return id;
        }

        private static Download NormalizeDownload(JToken input)
        {
            if (!Uri.TryCreate((string)input?["downloadUrl"], UriKind.Absolute, out var url) ||
                url.Scheme != "https" || !(url.Host == "booth.pm" || url.Host.EndsWith(".booth.pm", StringComparison.Ordinal)) ||
                !Regex.IsMatch(url.AbsolutePath, @"\A/downloadables/[0-9]+/?\z"))
                throw new IOException("Invalid BOOTH download URL");
            var filename = ((string)input?["filename"] ?? string.Empty).Trim();
            ValidateFilename(filename);
            using (var hash = SHA256.Create())
                return new Download { Url = url.AbsoluteUri, Filename = filename,
                    Id = Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(url.AbsolutePath + "\n" + filename))) };
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

        private static void ValidateFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename) || filename == "." || filename == ".." ||
                filename != Path.GetFileName(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                Regex.IsMatch(filename, @"[\\/:\x00-\x1f]")) throw new IOException("Invalid download filename");
        }

        private static string ResolveDirectory(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new IOException("An explicit directory is required");
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
        }

        private static void RejectLinks(string value)
        {
            for (var current = Path.GetFullPath(value); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Symbolic links and junctions are not supported: " + current);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        private sealed class Download
        {
            internal string Id;
            internal string Filename;
            internal string Url;
        }

        private sealed class ImportJob
        {
            internal string Id;
            internal Download Download;
            internal JObject Product;
            internal DateTime Started;
            internal bool Watching = true;
            internal string Error;
            internal string Candidate;
            internal long Size;
            internal DateTime Modified;
        }
    }
}
