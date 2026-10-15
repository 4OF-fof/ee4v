using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Mcp
{
    [InitializeOnLoad]
    internal static class Ee4vMcpBootstrap
    {
        private const string PortKey = "ee4v.mcp.port";
        private const int DefaultPort = 48884;
        private static Ee4vMcpHttpServer _server;
        private static bool _toolsRegistered;

        static Ee4vMcpBootstrap()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting -= Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.delayCall -= StartAutomatically;
            EditorApplication.delayCall += StartAutomatically;
        }

        [MenuItem("ee4v/MCP/Start Server")]
        private static void StartFromMenu()
        {
            Start();
        }

        [MenuItem("ee4v/MCP/Start Server", true)]
        private static bool ValidateStart()
        {
            return _server == null || !_server.IsRunning;
        }

        [MenuItem("ee4v/MCP/Stop Server")]
        private static void StopFromMenu()
        {
            Stop();
        }

        [MenuItem("ee4v/MCP/Stop Server", true)]
        private static bool ValidateStop()
        {
            return _server != null && _server.IsRunning;
        }

        [MenuItem("ee4v/MCP/Log Connection Information")]
        private static void LogConnectionInformation()
        {
            var port = EditorPrefs.GetInt(PortKey, DefaultPort);
            var endpoint = _server != null && _server.IsRunning ? _server.Endpoint : "http://127.0.0.1:" + port + "/mcp";
            Debug.Log(
                "ee4v MCP endpoint: " + endpoint + "\n" +
                "ee4v CLI: ee4v-cli projects (select with --project)\n" +
                "Codex config.toml:\n[mcp_servers.ee4v]\nurl = \"" + endpoint + "\"");
        }

        private static void StartAutomatically()
        {
            EditorApplication.delayCall -= StartAutomatically;
            if (!Application.isBatchMode)
            {
                Start();
            }
        }

        private static void Start()
        {
            EnsureToolsRegistered();
            if (_server != null && _server.IsRunning)
            {
                return;
            }

            try
            {
                var preferredPort = EditorPrefs.GetInt(PortKey, DefaultPort);
                for (var attempt = 0; attempt < 32; attempt++)
                {
                    var cli = new Ee4vCliBridge(Path.GetFullPath("."), Application.productName, Application.unityVersion);
                    _server = new Ee4vMcpHttpServer(1024 + (preferredPort - 1024 + attempt) % (65536 - 1024), cli);
                    try
                    {
                        _server.Start();
                        cli.Publish(_server.Endpoint.Substring(0, _server.Endpoint.Length - "/mcp".Length));
                        break;
                    }
                    catch (System.Net.HttpListenerException) when (attempt < 31)
                    { _server.Dispose(); _server = null; }
                }
                Debug.Log("ee4v MCP server listening at " + _server.Endpoint);
            }
            catch (Exception exception)
            {
                _server?.Dispose();
                _server = null;
                Debug.LogWarning("ee4v MCP server could not start: " + exception.Message);
            }
        }

        private static void Stop()
        {
            _server?.Dispose();
            _server = null;
        }

        private static void EnsureToolsRegistered()
        {
            if (_toolsRegistered)
            {
                return;
            }

            _toolsRegistered = true;
            McpStatusTools.Register();
            AvatarMcpTools.Register();
            AvatarAuthoringMcpTools.Register();
            FaceExpressionMcpTools.Register();
            AssetManagerMcpTools.Register();
            AvatarSimulationMcpTools.Register();
            ExpressionMenuMcpTools.Register();
        }
    }
}
