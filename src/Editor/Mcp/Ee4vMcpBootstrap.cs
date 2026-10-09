using System;
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
            Debug.Log(
                "ee4v MCP endpoint: http://127.0.0.1:" + port + "/mcp\n" +
                "Codex config.toml:\n[mcp_servers.ee4v]\nurl = \"http://127.0.0.1:" +
                port + "/mcp\"");
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
                _server = new Ee4vMcpHttpServer(
                    EditorPrefs.GetInt(PortKey, DefaultPort));
                _server.Start();
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
            FaceExpressionMcpTools.Register();
            AssetManagerMcpTools.Register();
            AvatarSimulationMcpTools.Register();
            ExpressionMenuMcpTools.Register();
        }
    }
}
