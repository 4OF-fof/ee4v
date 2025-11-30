using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.Core.i18n {
    public static class I18N {
        private static Dictionary<string, string> _translations;
        private static string _currentLanguage;

        static I18N() {
            _currentLanguage = EditorPrefs.GetString("4OF.ee4v.Language", "ja-JP");
            LoadTranslations();
        }

        private static void LoadTranslations() {
            _translations = new Dictionary<string, string>();
            var directoryPath = GetI18NDirectory();
            if (string.IsNullOrEmpty(directoryPath)) return;

            var languageFilePath = Path.Combine(directoryPath, $"{_currentLanguage}.json");

            if (File.Exists(languageFilePath))
                try {
                    var json = File.ReadAllText(languageFilePath);
                    _translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ??
                        new Dictionary<string, string>();
                }
                catch (Exception e) {
                    Debug.LogError($"[ee4v:i18n] Failed to load language file: {languageFilePath}\n{e.Message}");
                }
            else
                Debug.LogWarning($"[ee4v:i18n] Language file not found: {languageFilePath}");
        }

        private static string GetScriptPath() {
            var guids = AssetDatabase.FindAssets("t:MonoScript i18n");
            return guids.Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => path.EndsWith("i18n.cs") && path.Contains("Core/i18n"));
        }

        private static string GetI18NDirectory() {
            var scriptPath = GetScriptPath();
            if (string.IsNullOrEmpty(scriptPath)) {
                Debug.LogWarning("[ee4v:i18n] Could not find i18n.cs script path");
                return null;
            }

            var directoryPath = Path.GetDirectoryName(scriptPath);
            if (string.IsNullOrEmpty(directoryPath)) {
                Debug.LogWarning("[ee4v:i18n] Could not determine i18n directory from script path");
                return null;
            }

            return directoryPath;
        }

        public static string Get(string key, params object[] args) {
            if (!_translations.TryGetValue(key, out var value)) return key;

            if (args is null || args.Length == 0) return value;

            try {
                return string.Format(value, args);
            }
            catch (FormatException fe) {
                Debug.LogError(
                    $"[ee4v:i18n] FormatException for key='{key}' with format='{value}' and args=[{string.Join(", ", args.Select(a => a?.ToString() ?? "null"))}]: {fe.Message}");
                try {
                    return key + " " + string.Join(" ", args.Select(a => a?.ToString() ?? "null"));
                }
                catch {
                    return key;
                }
            }
            catch (Exception e) {
                Debug.LogError($"[ee4v:i18n] Unexpected exception formatting key='{key}': {e}");
                return key;
            }
        }

        public static void SetLanguage(string languageCode) {
            if (_currentLanguage == languageCode) return;
            _currentLanguage = languageCode;
            EditorPrefs.SetString("4OF.ee4v.Language", languageCode);
            LoadTranslations();
        }

        public static string[] GetAvailableLanguages() {
            var directoryPath = GetI18NDirectory();
            if (string.IsNullOrEmpty(directoryPath)) return Array.Empty<string>();

            return Directory.GetFiles(directoryPath, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .ToArray();
        }

        public static string LangFromIndex(int index) {
            var languages = GetAvailableLanguages();
            if (index < 0 || index >= languages.Length) return "ja-JP";
            return languages[index];
        }
    }
}