using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Adapter {
    public static class HttpServer {
        private static HttpListener _listener;
        private static Task _listenerTask;
        private static CancellationTokenSource _cts;
        private static bool _running;
        private static readonly object Lock = new();

        public static bool IsRunning {
            get {
                lock (Lock) {
                    return _running;
                }
            }
        }

        public static void Start(int port) {
            lock (Lock) {
                if (_running) return;
                _listener = new HttpListener();
                var prefix = $"http://localhost:{port}/";
                _listener.Prefixes.Add(prefix);
                try {
                    _listener.Start();
                }
                catch (HttpListenerException ex) {
                    Debug.LogError("HttpListener failed to start: " + ex);
                    _listener.Close();
                    _listener = null;
                    throw;
                }

                _running = true;
                _cts = new CancellationTokenSource();
                _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
                Debug.Log($"HttpServer started and listening on {prefix}");
            }
        }

        public static void Stop() {
            lock (Lock) {
                if (!_running) return;
                _running = false;
                try {
                    _listener?.Stop();
                }
                catch (Exception ex) {
                    Debug.LogError("Error while stopping HttpListener: " + ex);
                }

                try {
                    _listener?.Close();
                }
                catch (Exception ex) {
                    Debug.LogError("Error while closing HttpListener: " + ex);
                }

                try {
                    _cts?.Cancel();
                    if (_listenerTask != null)
                        if (!_listenerTask.Wait(500))
                            Debug.LogWarning("HttpServer listener task did not stop within timeout");
                }
                catch (Exception ex) {
                    Debug.LogError("Error while stopping listener thread: " + ex);
                }

                _listenerTask = null;
                _cts?.Dispose();
                _cts = null;
                _listener = null;
                Debug.Log("HttpServer stopped");
            }
        }

        private static async Task ListenLoopAsync(CancellationToken token) {
            if (_listener == null) return;
            while (IsRunning && !token.IsCancellationRequested)
                try {
                    var getContextTask = _listener.GetContextAsync();
                    var completed = await Task.WhenAny(getContextTask, Task.Delay(Timeout.Infinite, token));
                    if (completed != getContextTask) break;
                    var context = await getContextTask;
                    try {
                        _ = Task.Run(async () => await HandleContextAsync(context), token);
                    }
                    catch (Exception e) {
                        Debug.LogError("Error handling request: " + e);
                        try {
                            context.Response.StatusCode = 500;
                            context.Response.Close();
                        }
                        catch (Exception ex) {
                            Debug.LogWarning("Failed closing response after exception: " + ex);
                        }
                    }
                }
                catch (HttpListenerException) {
                    break;
                }
                catch (InvalidOperationException) {
                    break;
                }
                catch (Exception e) {
                    Debug.LogError("Unexpected error in ListenLoop: " + e);
                }
        }

        private static async Task HandleContextAsync(HttpListenerContext ctx) {
            var req = ctx.Request;
            var resp = ctx.Response;
            try {
                if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                    req.Url.AbsolutePath is "/" or "") {
                    var status = BoothLibraryServerState.Status ?? "waiting";
                    var body = $"{{\"status\":\"{status}\"}}";
                    var data = Encoding.UTF8.GetBytes(body);
                    resp.StatusCode = 200;
                    resp.ContentType = "application/json; charset=utf-8";
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.Length;
                    await resp.OutputStream.WriteAsync(data, 0, data.Length);
                }
                else if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                         req.Url.AbsolutePath is "/" or "") {
                    string body;
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding)) {
                        body = await reader.ReadToEndAsync();
                    }

                    if (string.IsNullOrWhiteSpace(body)) {
                        resp.StatusCode = 400;
                        var empty = Encoding.UTF8.GetBytes("Empty body");
                        resp.ContentLength64 = empty.Length;
                        await resp.OutputStream.WriteAsync(empty, 0, empty.Length);
                    }
                    else {
                        var trim = body.Trim();
                        ShopListDtp wrapper = null;
                        try {
                            if (trim.Length > 0 && trim[0] == '[') {
                                var wrapped = "{\"shopList\": " + body + "}";
                                wrapper = JsonUtility.FromJson<ShopListDtp>(wrapped);
                            }
                            else {
                                wrapper = JsonUtility.FromJson<ShopListDtp>(body);
                            }
                        }
                        catch (Exception ex) {
                            Debug.LogWarning("Failed parsing JSON body for POST /: " + ex);
                        }

                        if (wrapper?.shopList == null) {
                            resp.StatusCode = 400;
                            resp.ContentType = "text/plain; charset=utf-8";
                            var err = Encoding.UTF8.GetBytes("Invalid JSON payload");
                            resp.ContentLength64 = err.Length;
                            await resp.OutputStream.WriteAsync(err, 0, err.Length);
                        }
                        else {
                            int created;
                            try {
                                var tcs = new TaskCompletionSource<int>();
                                EditorApplication.delayCall += () =>
                                {
                                    try {
                                        var res = BoothLibraryImporter.Import(wrapper.shopList);
                                        tcs.TrySetResult(res);
                                    }
                                    catch (Exception ex) {
                                        tcs.TrySetException(ex);
                                    }
                                };

                                created = await tcs.Task;
                            }
                            catch (Exception ex) {
                                Debug.LogError("Error importing shops on POST: " + ex);
                                resp.StatusCode = 500;
                                resp.ContentType = "application/json; charset=utf-8";
                                var errStr =
                                    $"{{\"ok\":false, \"error\": \"Import failed: {ex.Message.Replace("\"", "\\\"")}\"}}";
                                var err = Encoding.UTF8.GetBytes(errStr);
                                resp.ContentLength64 = err.LongLength;
                                await resp.OutputStream.WriteAsync(err, 0, err.Length);
                                return;
                            }

                            var tcsSet = new TaskCompletionSource<bool>();
                            var contents = wrapper.shopList;
                            EditorApplication.delayCall += () =>
                            {
                                try {
                                    BoothLibraryServerState.SetContents(contents);
                                    tcsSet.TrySetResult(true);
                                }
                                catch (Exception ex) {
                                    tcsSet.TrySetException(ex);
                                }
                            };
                            await tcsSet.Task;

                            resp.StatusCode = 200;
                            resp.ContentType = "application/json; charset=utf-8";
                            var okStr = $"{{\"ok\":true, \"created\":{created}}}";
                            var ok = Encoding.UTF8.GetBytes(okStr);
                            resp.ContentLength64 = ok.LongLength;
                            await resp.OutputStream.WriteAsync(ok, 0, ok.Length);
                        }
                    }
                }
                else {
                    resp.StatusCode = 404;
                    resp.ContentType = "text/plain";
                    var notFound = Encoding.UTF8.GetBytes("Not Found");
                    resp.ContentLength64 = notFound.Length;
                    await resp.OutputStream.WriteAsync(notFound, 0, notFound.Length);
                }
            }
            finally {
                try {
                    resp.OutputStream.Close();
                }
                catch (Exception) {
                    // ignored
                }

                try {
                    resp.Close();
                }
                catch (Exception) {
                    // ignored
                }
            }
        }
    }
}