using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ee4v.Mcp
{
    internal sealed class Ee4vMcpHttpServer : IDisposable
    {
        private const string ServerName = "ee4v";
        private const string ServerVersion = "0.1.0";
        private const long MaximumRequestBytes = 8L * 1024L * 1024L;
        private static readonly string[] ProtocolVersions = { "2025-11-25", "2025-06-18", "2025-03-26" };
        private readonly ConcurrentDictionary<string, RequestSession> _sessions = new ConcurrentDictionary<string, RequestSession>();
        private readonly int _port;
        private HttpListener _listener;
        private CancellationTokenSource _cancellation;
        private Task _listenTask;

        internal Ee4vMcpHttpServer(int port)
        {
            if (port < 1024 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            _port = port;
        }

        internal bool IsRunning => _listener != null && _listener.IsListening;
        internal string Endpoint => "http://127.0.0.1:" + _port + "/mcp";

        internal void Start()
        {
            if (IsRunning)
            {
                return;
            }

            var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
            try
            {
                listener.Start();
            }
            catch
            {
                listener.Close();
                throw;
            }

            var cancellation = new CancellationTokenSource();
            _listener = listener;
            _cancellation = cancellation;
            _listenTask = Listen(listener, cancellation.Token);
        }

        public void Dispose()
        {
            var listener = _listener;
            var cancellation = _cancellation;
            _listener = null;
            _cancellation = null;
            _listenTask = null;

            cancellation?.Cancel();
            foreach (var session in _sessions.Values) session.Close();
            _sessions.Clear();
            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch
            {
            }

            cancellation?.Dispose();
        }

        private async Task Listen(
            HttpListener listener,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested &&
                   listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    cancellationToken.IsCancellationRequested ||
                    exception is HttpListenerException ||
                    exception is ObjectDisposedException)
                {
                    break;
                }

                _ = Process(context, cancellationToken);
            }
        }

        private async Task Process(
            HttpListenerContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                var remoteEndpoint = context.Request.RemoteEndPoint;
                if (remoteEndpoint == null ||
                    !IPAddress.IsLoopback(remoteEndpoint.Address) ||
                    !IsLoopbackHost(context.Request.Headers["Host"]) ||
                    !IsAllowedOrigin(context.Request.Headers["Origin"]))
                {
                    await WriteStatus(context.Response, 403, "Loopback access only.");
                    return;
                }

                if (context.Request.HttpMethod == "GET" &&
                    string.Equals(context.Request.Url.AbsolutePath, "/health", StringComparison.Ordinal))
                {
                    await WriteJson(
                        context.Response,
                        200,
                        new JObject
                        {
                            ["ok"] = true,
                            ["server"] = ServerName,
                            ["version"] = ServerVersion
                        });
                    return;
                }

                var versionHeader = context.Request.Headers["MCP-Protocol-Version"];
                if (versionHeader != null && !ProtocolVersions.Contains(versionHeader, StringComparer.Ordinal))
                {
                    await WriteStatus(context.Response, 400, "Unsupported MCP-Protocol-Version.");
                    return;
                }

                if (context.Request.HttpMethod == "DELETE" && context.Request.Url.AbsolutePath == "/mcp")
                {
                    var closingId = context.Request.Headers["MCP-Session-Id"];
                    if (closingId == null)
                        await WriteStatus(context.Response, 400, "MCP-Session-Id is required.");
                    else if (!_sessions.TryGetValue(closingId, out var closingSession))
                        await WriteStatus(context.Response, 404, "MCP session has ended.");
                    else if (versionHeader != null && versionHeader != closingSession.ProtocolVersion)
                        await WriteStatus(context.Response, 400, "MCP-Protocol-Version must match the initialized session.");
                    else if (_sessions.TryRemove(closingId, out _))
                    {
                        closingSession.Close();
                        context.Response.StatusCode = 204;
                        context.Response.Close();
                    }
                    else await WriteStatus(context.Response, 404, "MCP session has ended.");
                    return;
                }

                if (context.Request.HttpMethod != "POST" ||
                    !string.Equals(context.Request.Url.AbsolutePath, "/mcp", StringComparison.Ordinal))
                {
                    await WriteStatus(context.Response, 405, "POST /mcp is required.");
                    return;
                }

                if (context.Request.ContentLength64 > MaximumRequestBytes)
                {
                    await WriteStatus(context.Response, 413, "MCP request is too large.");
                    return;
                }

                JObject request;
                request = JObject.Parse(await ReadRequestBody(context.Request, cancellationToken));

                if (request["jsonrpc"]?.Type != JTokenType.String || (string)request["jsonrpc"] != "2.0" || request["method"]?.Type != JTokenType.String ||
                    request["params"] != null && !(request["params"] is JObject) ||
                    request["id"] != null && request["id"].Type != JTokenType.String && request["id"].Type != JTokenType.Integer)
                {
                    await WriteJson(context.Response, 400, Error(null, -32600, "Invalid request", "A JSON-RPC 2.0 method, object params and string/integer request ID are required."));
                    return;
                }

                var method = (string)request["method"];
                var sessionId = context.Request.Headers["MCP-Session-Id"];
                RequestSession session = null;
                if (sessionId != null && !_sessions.TryGetValue(sessionId, out session))
                {
                    await WriteStatus(context.Response, 404, "MCP session has ended. Initialize again.");
                    return;
                }
                if (session != null && versionHeader != null && versionHeader != session.ProtocolVersion)
                {
                    await WriteStatus(context.Response, 400, "MCP-Protocol-Version must match the initialized session.");
                    return;
                }
                if (method != "initialize" && method != "ping" && method != "server/discover" && session == null)
                {
                    await WriteStatus(context.Response, 400, "MCP-Session-Id is required. Initialize first.");
                    return;
                }
                if (request["id"] == null)
                {
                    if (method == "notifications/cancelled") session?.Cancel((request["params"] as JObject)?["requestId"]);
                    context.Response.StatusCode = 202;
                    context.Response.Close();
                    return;
                }

                JObject response;
                if (method == "initialize")
                {
                    var requestedVersion = (request["params"] as JObject)?["protocolVersion"];
                    if (requestedVersion?.Type != JTokenType.String)
                    {
                        await WriteJson(context.Response, 200, Error(request["id"], -32602, "Invalid params", "protocolVersion is required."));
                        return;
                    }
                    var version = ProtocolVersions.Contains((string)requestedVersion, StringComparer.Ordinal)
                        ? (string)requestedVersion : ProtocolVersions[0];
                    sessionId = Guid.NewGuid().ToString("N");
                    _sessions[sessionId] = new RequestSession(version);
                    context.Response.Headers["MCP-Session-Id"] = sessionId;
                    var result = ServerCapabilities();
                    result["protocolVersion"] = version;
                    response = Result(request["id"], result);
                }
                else if (session != null)
                {
                    using (var operation = session.Begin(request["id"], cancellationToken))
                    {
                        try { response = await HandleRequest(request, operation.Token); }
                        finally { session.End(request["id"]); }
                    }
                }
                else response = await HandleRequest(request, cancellationToken);
                await WriteJson(context.Response, 200, response);
            }
            catch (OperationCanceledException)
            {
                context.Response.Abort();
            }
            catch (McpToolException exception)
            {
                await TryWriteJson(context.Response, 400, Error(null, -32600, "Invalid request", exception.Message));
            }
            catch (RequestTooLargeException exception)
            {
                await TryWriteJson(
                    context.Response,
                    413,
                    Error(null, -32600, "Request too large", exception.Message));
            }
            catch (JsonException exception)
            {
                await TryWriteJson(
                    context.Response,
                    400,
                    Error(null, -32700, "Parse error", exception.Message));
            }
            catch (Exception exception)
            {
                await TryWriteJson(
                    context.Response,
                    500,
                    Error(null, -32603, "Internal error", exception.Message));
            }
        }

        private static async Task<string> ReadRequestBody(
            HttpListenerRequest request,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[81920];
            using (var content = new MemoryStream())
            {
                while (true)
                {
                    var read = await request.InputStream.ReadAsync(
                        buffer,
                        0,
                        buffer.Length, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    if (content.Length + read > MaximumRequestBytes)
                    {
                        throw new RequestTooLargeException(
                            "MCP requests are limited to " +
                            MaximumRequestBytes + " bytes.");
                    }

                    content.Write(buffer, 0, read);
                }

                content.Position = 0;
                using (var reader = new StreamReader(
                           content,
                           request.ContentEncoding ?? Encoding.UTF8,
                           true))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static async Task<JObject> HandleRequest(
            JObject request,
            CancellationToken cancellationToken)
        {
            var id = request["id"];
            var method = (string)request["method"];
            var parameters = request["params"] as JObject ?? new JObject();
            switch (method)
            {
                case "server/discover":
                    return Result(id, ServerCapabilities());
                case "ping":
                    return Result(id, new JObject());
                case "tools/list":
                    return Result(id, new JObject { ["tools"] = McpToolRegistry.List() });
                case "tools/call":
                {
                    if (parameters["name"]?.Type != JTokenType.String ||
                        parameters["arguments"] != null && !(parameters["arguments"] is JObject))
                        return Error(id, -32602, "Invalid params", "name must be a string and arguments must be an object.");
                    var name = (string)parameters["name"];
                    var arguments = parameters["arguments"] as JObject ?? new JObject();
                    var toolResult = await UnityMainThreadDispatcher.Run(
                        () => McpToolRegistry.Invoke(name, arguments, cancellationToken), cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    return Result(id, toolResult.ToProtocolValue());
                }
                default:
                    return Error(id, -32601, "Method not found", method ?? string.Empty);
            }
        }

        private static JObject ServerCapabilities()
        {
            return new JObject
            {
                ["serverInfo"] = new JObject
                {
                    ["name"] = ServerName,
                    ["version"] = ServerVersion
                },
                ["capabilities"] = new JObject
                {
                    ["tools"] = new JObject { ["listChanged"] = false }
                },
                ["instructions"] =
                    "Use ee4v tools for VRChat avatar-domain operations. " +
                    "Inspect or plan before writes. Only pass avatarRef values returned by ee4v_find_avatars. " +
                    "Use generic Unity MCP tools for hierarchy and arbitrary component editing."
            };
        }

        private static JObject Result(JToken id, JToken result)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone() ?? JValue.CreateNull(),
                ["result"] = result ?? new JObject()
            };
        }

        private static JObject Error(
            JToken id,
            int code,
            string message,
            string data)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone() ?? JValue.CreateNull(),
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message,
                    ["data"] = data
                }
            };
        }

        private static bool IsLoopbackHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (!Uri.TryCreate(
                    "http://" + host.Trim(),
                    UriKind.Absolute,
                    out var uri) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.AbsolutePath != "/")
            {
                return false;
            }

            return string.Equals(
                       uri.Host,
                       "localhost",
                       StringComparison.OrdinalIgnoreCase) ||
                   IPAddress.TryParse(uri.Host, out var address) &&
                   IPAddress.IsLoopback(address);
        }

        private static bool IsAllowedOrigin(string origin)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                return true;
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IPAddress.TryParse(uri.Host, out var address) &&
                   IPAddress.IsLoopback(address);
        }

        private static async Task WriteStatus(
            HttpListenerResponse response,
            int statusCode,
            string message)
        {
            await WriteJson(
                response,
                statusCode,
                new JObject { ["message"] = message ?? string.Empty });
        }

        private static async Task WriteJson(
            HttpListenerResponse response,
            int statusCode,
            JToken value)
        {
            var bytes = Encoding.UTF8.GetBytes(
                (value ?? new JObject()).ToString(Formatting.None));
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = bytes.Length;
            try
            {
                await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        private static async Task TryWriteJson(
            HttpListenerResponse response,
            int statusCode,
            JToken value)
        {
            try
            {
                await WriteJson(response, statusCode, value);
            }
            catch
            {
                // The peer can disconnect while an error response is written.
            }
        }

        private sealed class RequestTooLargeException : Exception
        {
            internal RequestTooLargeException(string message)
                : base(message)
            {
            }
        }

        private sealed class RequestSession
        {
            private readonly Dictionary<string, CancellationTokenSource> _requests = new Dictionary<string, CancellationTokenSource>();
            private bool _closed;
            internal RequestSession(string version) { ProtocolVersion = version; }
            internal string ProtocolVersion { get; }
            private static string Key(JToken id) => id?.ToString(Formatting.None);
            internal CancellationTokenSource Begin(JToken id, CancellationToken serverToken)
            {
                lock (_requests)
                {
                    serverToken.ThrowIfCancellationRequested();
                    if (_closed) throw new OperationCanceledException();
                    var key = Key(id);
                    if (_requests.ContainsKey(key)) throw new McpToolException("invalid_request", "A request with this ID is already running in this session.");
                    var operation = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
                    _requests.Add(key, operation);
                    return operation;
                }
            }
            internal void End(JToken id) { lock (_requests) _requests.Remove(Key(id)); }
            internal void Cancel(JToken id)
            {
                if (id == null || id.Type != JTokenType.String && id.Type != JTokenType.Integer) return;
                lock (_requests) if (_requests.TryGetValue(Key(id), out var operation)) operation.Cancel();
            }
            internal void Close()
            {
                lock (_requests)
                {
                    _closed = true;
                    foreach (var operation in _requests.Values) operation.Cancel();
                }
            }
        }
    }
}
