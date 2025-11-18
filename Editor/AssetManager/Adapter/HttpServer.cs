using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Adapter {
    public static class HttpServer {
        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static bool _running;
        private static DateTime _startedAt;
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
                _startedAt = DateTime.UtcNow;
                _listenerThread = new Thread(ListenLoop) { IsBackground = true, Name = "BoothLibraryHttpServer" };
                _listenerThread.Start();
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
                    if (_listenerThread is { IsAlive: true })
                        if (!_listenerThread.Join(500))
                            _listenerThread.Abort();
                }
                catch (Exception ex) {
                    Debug.LogError("Error while stopping listener thread: " + ex);
                }

                _listenerThread = null;
                _listener = null;
                Debug.Log("HttpServer stopped");
            }
        }

        private static void ListenLoop() {
            if (_listener == null) return;
            while (IsRunning)
                try {
                    var context =
                        _listener.GetContext(); // Will block until a request comes in or the listener is closed
                    try {
                        HandleContext(context);
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
                    // Typically thrown when the listener is stopped; break out
                    break;
                }
                catch (InvalidOperationException) {
                    // Listener might have been closed; exit loop
                    break;
                }
                catch (Exception e) {
                    Debug.LogError("Unexpected error in ListenLoop: " + e);
                }
        }

        private static void HandleContext(HttpListenerContext ctx) {
            var req = ctx.Request;
            var resp = ctx.Response;
            try {
                if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                    (req.Url.AbsolutePath == "/health" || req.Url.AbsolutePath == "/health/")) {
                    var uptime = (DateTime.UtcNow - _startedAt).TotalSeconds;
                    var body =
                        $"{{\"status\":\"ok\", \"uptimeSeconds\": {uptime:F1}, \"startedAt\": \"{_startedAt:o}\" }}";
                    var data = Encoding.UTF8.GetBytes(body);
                    resp.StatusCode = 200;
                    resp.ContentType = "application/json; charset=utf-8";
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.Length;
                    resp.OutputStream.Write(data, 0, data.Length);
                }
                else if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                         (req.Url.AbsolutePath == "/contents" || req.Url.AbsolutePath == "/contents/")) {
                    // Expect a JSON array of shops
                    string body;
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding)) {
                        body = reader.ReadToEnd();
                    }

                    if (string.IsNullOrWhiteSpace(body)) {
                        resp.StatusCode = 400;
                        var empty = Encoding.UTF8.GetBytes("Empty body");
                        resp.ContentLength64 = empty.Length;
                        resp.OutputStream.Write(empty, 0, empty.Length);
                    }
                    else {
                        // Convert top-level array to wrapper for Unity JsonUtility
                        var trim = body.Trim();
                        ShopsWrapper wrapper = null;
                        try {
                            if (trim.Length > 0 && trim[0] == '[') {
                                var wrapped = "{\"Shops\": " + body + "}";
                                wrapper = JsonUtility.FromJson<ShopsWrapper>(wrapped);
                            }
                            else {
                                wrapper = JsonUtility.FromJson<ShopsWrapper>(body);
                            }
                        }
                        catch (Exception ex) {
                            Debug.LogWarning("Failed parsing JSON body for /contents: " + ex);
                        }

                        if (wrapper?.Shops == null) {
                            resp.StatusCode = 400;
                            resp.ContentType = "text/plain; charset=utf-8";
                            var err = Encoding.UTF8.GetBytes("Invalid JSON payload");
                            resp.ContentLength64 = err.Length;
                            resp.OutputStream.Write(err, 0, err.Length);
                        }
                        else {
                            BoothLibraryServerState.SetContents(wrapper.Shops);
                            resp.StatusCode = 200;
                            resp.ContentType = "application/json; charset=utf-8";
                            var ok = Encoding.UTF8.GetBytes("{\"ok\":true}");
                            resp.ContentLength64 = ok.Length;
                            resp.OutputStream.Write(ok, 0, ok.Length);
                        }
                    }
                }
                else {
                    resp.StatusCode = 404;
                    resp.ContentType = "text/plain";
                    var notFound = Encoding.UTF8.GetBytes("Not Found");
                    resp.ContentLength64 = notFound.Length;
                    resp.OutputStream.Write(notFound, 0, notFound.Length);
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

    public abstract class BoothLibraryAdapter {
        static BoothLibraryAdapter() {
            // Keep stability hooks but do not auto-start the server; server will be started when window is opened.
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode) HttpServer.Stop();
        }

        private static void OnBeforeAssemblyReload() {
            HttpServer.Stop();
        }

        private static void OnEditorQuitting() {
            HttpServer.Stop();
        }

        [MenuItem("ee4v/Open Window")]
        private static void OpenWindowMenu() {
            BoothLibraryWindow.ShowWindow();
        }
    }
}