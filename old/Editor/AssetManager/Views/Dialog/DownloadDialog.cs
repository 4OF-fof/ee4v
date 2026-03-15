using System;
using System.IO;
using System.Threading;
using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Dialog {
    public class DownloadDialog {
        private Ulid _assetId;
        private AssetService _assetService;
        private string _downloadUrl;
        private string _expectedFileName;
        private FileSystemWatcher _fileWatcher;
        private bool _isMonitoring;
        private Label _statusLabel;

        public event Action OnDownloadCompleted;

        public VisualElement CreateContent(string downloadUrl, Ulid assetId, string expectedFileName,
            AssetService assetService) {
            _downloadUrl = downloadUrl;
            _assetId = assetId;
            _expectedFileName = expectedFileName;
            _assetService = assetService;

            var content = new VisualElement();

            var title = new Label(I18N.Get("UI.AssetManager.Download.Title")) {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(title);

            var instructionLabel = new Label(I18N.Get("UI.AssetManager.Download.Instruction")) {
                style = {
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 16,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            content.Add(instructionLabel);

            _statusLabel = new Label(I18N.Get("UI.AssetManager.Download.StatusWaiting")) {
                style = {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginBottom = 16,
                    fontSize = 12,
                    color = ColorPreset.InactiveItem
                }
            };
            content.Add(_statusLabel);

            var buttonRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                    marginTop = 16
                }
            };

            var closeBtn = new Button {
                text = I18N.Get("UI.AssetManager.Download.Close"),
                style = {
                    width = 100,
                    height = 30,
                    backgroundColor = ColorPreset.DefaultBackground,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0
                }
            };
            closeBtn.clicked += () =>
            {
                StopMonitoring();
                CloseDialog(content);
                OnDownloadCompleted?.Invoke();
            };

            buttonRow.Add(closeBtn);
            content.Add(buttonRow);

            content.schedule.Execute(StartDownload).ExecuteLater(100);

            return content;
        }

        private void StartDownload() {
            try {
                Application.OpenURL(_downloadUrl);
                _statusLabel.text = I18N.Get("UI.AssetManager.Download.StatusMonitoring");
                StartMonitoring();
            }
            catch (Exception e) {
                _statusLabel.text = I18N.Get("UI.AssetManager.Download.Error.BrowserOpenFailedFmt", e.Message);
            }
        }

        private void StartMonitoring() {
            if (_isMonitoring) return;

            var downloadsPath = GetDownloadsPath();
            if (string.IsNullOrEmpty(downloadsPath) || !Directory.Exists(downloadsPath)) {
                _statusLabel.text = I18N.Get("UI.AssetManager.Download.Error.DownloadFolderNotFound");
                return;
            }

            _isMonitoring = true;

            if (CheckForExistingFile(downloadsPath)) return;

            _fileWatcher = new FileSystemWatcher(downloadsPath) {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Renamed += OnFileRenamed;

            EditorApplication.update += CheckMonitoringStatus;
        }

        private void StopMonitoring() {
            _isMonitoring = false;

            if (_fileWatcher != null) {
                _fileWatcher.Created -= OnFileCreated;
                _fileWatcher.Changed -= OnFileChanged;
                _fileWatcher.Renamed -= OnFileRenamed;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            EditorApplication.update -= CheckMonitoringStatus;
        }

        private void CheckMonitoringStatus() {
            if (!_isMonitoring) EditorApplication.update -= CheckMonitoringStatus;
        }

        private bool CheckForExistingFile(string downloadsPath) {
            var files = Directory.GetFiles(downloadsPath, "*" + Path.GetExtension(_expectedFileName));
            foreach (var file in files) {
                if (!Path.GetFileName(file).Equals(_expectedFileName, StringComparison.OrdinalIgnoreCase)) continue;
                EditorApplication.delayCall += () => ProcessDownloadedFile(file);
                return true;
            }

            return false;
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e) {
            CheckFile(e.FullPath);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e) {
            CheckFile(e.FullPath);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e) {
            CheckFile(e.FullPath);
        }

        private void CheckFile(string filePath) {
            if (!_isMonitoring) return;

            var fileName = Path.GetFileName(filePath);
            if (fileName.Equals(_expectedFileName, StringComparison.OrdinalIgnoreCase))
                EditorApplication.delayCall += () =>
                {
                    if (IsFileReady(filePath)) ProcessDownloadedFile(filePath);
                };
        }

        private static bool IsFileReady(string filePath) {
            try {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return true;
            }
            catch {
                return false;
            }
        }

        private void ProcessDownloadedFile(string filePath) {
            if (!_isMonitoring) return;

            try {
                Thread.Sleep(500);

                if (_assetService == null || !File.Exists(filePath)) return;
                _assetService.AddFileToAsset(_assetId, filePath);

                try {
                    File.Delete(filePath);
                }
                catch (Exception deleteEx) {
                    Debug.LogWarning(I18N.Get("Debug.AssetManager.Download.DeleteFailedFmt", deleteEx.Message));
                }

                EditorApplication.delayCall += () =>
                {
                    _statusLabel.text = I18N.Get("UI.AssetManager.Download.Completed");
                    StopMonitoring();
                };
            }
            catch (Exception e) {
                EditorApplication.delayCall += () =>
                {
                    _statusLabel.text = I18N.Get("UI.AssetManager.Download.Error.RegistrationFailedFmt", e.Message);
                    StopMonitoring();
                };
            }
        }

        private static string GetDownloadsPath() {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloadsPath = Path.Combine(userProfile, "Downloads");

            if (!Directory.Exists(downloadsPath))
                Debug.LogError(I18N.Get("Debug.AssetManager.Download.FolderNotFound"));

            return downloadsPath;
        }

        private static void CloseDialog(VisualElement content) {
            var dialog = content.parent;
            var container = dialog?.parent;
            container?.RemoveFromHierarchy();
        }
    }
}