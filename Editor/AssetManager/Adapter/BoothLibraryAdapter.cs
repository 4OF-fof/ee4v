using UnityEditor;

namespace _4OF.ee4v.AssetManager.Adapter {
    public abstract class BoothLibraryAdapter {
        static BoothLibraryAdapter() {
            // Keep stability hooks but do not auto-start the server; server will be started when window is opened.
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode) {
                HttpServer.Stop();
            }
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