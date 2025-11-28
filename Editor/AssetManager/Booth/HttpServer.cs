using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using _4OF.ee4v.Core.i18n;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Booth {
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
                Debug.Log(I18N.Get("Debug.AssetManager.HttpServer.StartedFmt", prefix));
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
                    Debug.LogError(I18N.Get("Debug.AssetManager.HttpServer.StopFailedFmt", ex.Message));
                }

                try {
                    _listener?.Close();
                }
                catch (Exception ex) {
                    Debug.LogError(I18N.Get("Debug.AssetManager.HttpServer.CloseFailedFmt", ex.Message));
                }

                try {
                    _cts?.Cancel();
                    if (_listenerTask != null)
                        if (!_listenerTask.Wait(500))
                            Debug.LogWarning(I18N.Get("Debug.AssetManager.HttpServer.ListenerTimeout"));
                }
                catch (Exception ex) {
                    Debug.LogError(I18N.Get("Debug.AssetManager.HttpServer.StopListenerFailedFmt", ex.Message));
                }

                _listenerTask = null;
                _cts?.Dispose();
                _cts = null;
                _listener = null;
                Debug.Log(I18N.Get("Debug.AssetManager.HttpServer.Stopped"));
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
                        Debug.LogError(I18N.Get("Debug.AssetManager.HttpServer.HandleContextFailedFmt", e.Message));
                        try {
                            context.Response.StatusCode = 500;
                            context.Response.Close();
                        }
                        catch (Exception ex) {
                            Debug.LogWarning(I18N.Get("Debug.AssetManager.HttpServer.HandleContextFailedFmt",
                                ex.Message));
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
                    Debug.LogError(I18N.Get("Debug.AssetManager.HttpServer.ListenLoopFailedFmt", e.Message));
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
                        List<ShopDto> shopList = null;
                        try {
                            if (trim.Length > 0 && trim[0] == '[') {
                                shopList = JsonConvert.DeserializeObject<List<ShopDto>>(body);
                            }
                            else {
                                var wrapper = JsonConvert.DeserializeObject<ShopListDtp>(body);
                                shopList = wrapper?.shopList;
                            }
                        }
                        catch (Exception ex) {
                            Debug.LogWarning(I18N.Get("Debug.AssetManager.HttpServer.JsonParseFailedFmt", ex.Message));
                        }

                        if (shopList == null) {
                            resp.StatusCode = 400;
                            resp.ContentType = "text/plain; charset=utf-8";
                            var err = Encoding.UTF8.GetBytes("Invalid JSON payload");
                            resp.ContentLength64 = err.Length;
                            await resp.OutputStream.WriteAsync(err, 0, err.Length);
                        }
                        else {
                            var finalShopList = shopList;
                            EditorApplication.delayCall += () =>
                            {
                                try {
                                    BoothLibraryImporter.Import(finalShopList);

                                    BoothLibraryServerState.SetContents(finalShopList);
                                }
                                catch (Exception ex) {
                                    Debug.LogError(
                                        I18N.Get("Debug.AssetManager.HttpServer.ImportFailedFmt", ex.Message));
                                }
                            };

                            resp.StatusCode = 200;
                            resp.ContentType = "application/json; charset=utf-8";
                            var okStr = "{\"ok\":true, \"message\":\"Request accepted\"}";
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