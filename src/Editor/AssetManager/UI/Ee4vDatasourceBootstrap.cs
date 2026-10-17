using System;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure.Ee4v;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    [InitializeOnLoad]
    internal static class Ee4vDatasourceBootstrap
    {
        private const string StoppedKey = "ee4v.datasource.manuallyStopped";
        private static Ee4vDatasourceServer _server;
        private static AssetDatasourceKind _datasource;

        static Ee4vDatasourceBootstrap()
        {
            _datasource = AssetManagerSettings.SelectedDatasource;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.delayCall += StartAutomatically;
            EditorApplication.update += DrainMessages;
            AssetManagerSettings.DatasourceChanged += OnDatasourceChanged;
            AssetManagerSettings.DownloadDirectoryChanged += Restart;
            GlobalDataSettings.PathChanged += Restart;
        }

        [MenuItem("ee4v/Datasource/Start Server")]
        private static void StartFromMenu()
        {
            SessionState.SetBool(StoppedKey, false);
            Start();
        }

        [MenuItem("ee4v/Datasource/Start Server", true)]
        private static bool ValidateStart() => AssetManagerSettings.SelectedDatasource == AssetDatasourceKind.Ee4v &&
                                               (_server == null || !_server.IsRunning);

        [MenuItem("ee4v/Datasource/Stop Server")]
        private static void StopFromMenu()
        {
            SessionState.SetBool(StoppedKey, true);
            Stop();
        }

        [MenuItem("ee4v/Datasource/Stop Server", true)]
        private static bool ValidateStop() => _server != null && _server.IsRunning;

        private static void StartAutomatically()
        {
            if (!Application.isBatchMode && !SessionState.GetBool(StoppedKey, false)) Start();
        }

        private static void OnDatasourceChanged()
        {
            var selected = AssetManagerSettings.SelectedDatasource;
            if (_datasource == selected) return;
            _datasource = selected;
            SessionState.SetBool(StoppedKey, false);
            Restart();
        }

        private static void Restart()
        {
            Stop();
            StartAutomatically();
        }

        private static void Start()
        {
            if (AssetManagerSettings.SelectedDatasource != AssetDatasourceKind.Ee4v || _server?.IsRunning == true) return;
            Stop();
            try
            {
                _server = new Ee4vDatasourceServer(AssetManagerSettings.Ee4vLibraryPath, AssetManagerSettings.DownloadDirectoryPath);
                _server.Start();
                Debug.Log("ee4v datasource server listening at " + Ee4vDatasourceServer.Endpoint);
            }
            catch (Exception exception)
            {
                Stop();
                Debug.LogWarning("ee4v datasource server could not start: " + exception.Message);
            }
        }

        private static void DrainMessages()
        {
            if (_server == null) return;
            while (_server.TryDequeueMessage(out var message)) Debug.Log(message);
        }

        private static void Stop()
        {
            var server = _server;
            _server = null;
            if (server == null) return;
            server.Dispose();
            while (server.TryDequeueMessage(out var message)) Debug.Log(message);
        }
    }
}
