using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ee4v.Mcp
{
    internal sealed class Ee4vMcpHttpServer : IDisposable
    {
        private const string ServerName = "ee4v";
        private const string ServerVersion = "0.1.0";
        private const long MaximumRequestBytes = 8L * 1024L * 1024L;
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

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
            _listener.Start();
            _cancellation = new CancellationTokenSource();
            _listenTask = Listen(_cancellation.Token);
        }

        public void Dispose()
        {
            _cancellation?.Cancel();
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
            }

            _listener = null;
            _cancellation?.Dispose();
            _cancellation = null;
            _listenTask = null;
        }

        private async Task Listen(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && IsRunning)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
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

        private static async Task Process(
            HttpListenerContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!IPAddress.IsLoopback(context.Request.RemoteEndPoint.Address) ||
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
                using (var reader = new StreamReader(
                           context.Request.InputStream,
                           context.Request.ContentEncoding ?? Encoding.UTF8))
                {
                    request = JObject.Parse(await reader.ReadToEndAsync());
                }

                var method = (string)request["method"];
                if (method != null && method.StartsWith("notifications/", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = 202;
                    context.Response.Close();
                    return;
                }

                var response = await HandleRequest(request, cancellationToken);
                await WriteJson(context.Response, 200, response);
            }
            catch (JsonException exception)
            {
                await WriteJson(
                    context.Response,
                    400,
                    Error(null, -32700, "Parse error", exception.Message));
            }
            catch (Exception exception)
            {
                try
                {
                    await WriteJson(
                        context.Response,
                        500,
                        Error(null, -32603, "Internal error", exception.Message));
                }
                catch
                {
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
                case "initialize":
                {
                    var protocolVersion = (string)parameters["protocolVersion"] ??
                                          "2025-11-25";
                    var result = ServerCapabilities();
                    result["protocolVersion"] = protocolVersion;
                    return Result(id, result);
                }
                case "ping":
                    return Result(id, new JObject());
                case "tools/list":
                    return Result(id, new JObject { ["tools"] = McpToolRegistry.List() });
                case "tools/call":
                {
                    var name = (string)parameters["name"];
                    var arguments = parameters["arguments"] as JObject ?? new JObject();
                    var toolResult = await UnityMainThreadDispatcher.Run(
                        () => McpToolRegistry.Invoke(name, arguments));
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

            var name = host.Split(':')[0];
            return string.Equals(name, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "[::1]", StringComparison.OrdinalIgnoreCase);
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
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
    }
}
