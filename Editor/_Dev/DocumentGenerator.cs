using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v._Dev {
    public static class DocumentGenerator {
        private const string OutputFolderPath = "Assets/4OF/ee4v/Document";

        [MenuItem("Debug/Generate Document", false, 200)]
        public static void GenerateDocument() {
            var originalLanguage = SettingSingleton.I.language;

            var allTranslatedData = CollectAllTranslatedData();

            if (allTranslatedData.Count == 0) {
                Debug.LogError(
                    "Implementations not found.");
                return;
            }

            if (!Directory.Exists(OutputFolderPath)) Directory.CreateDirectory(OutputFolderPath);

            foreach (var (langCode, dataList) in allTranslatedData) {
                var markdown = GenerateMarkdown(dataList, langCode);
                var outputPath = Path.Combine(OutputFolderPath, $"COMPONENT_DOC_{langCode}.md");
                File.WriteAllText(outputPath, markdown);
                Debug.Log($"Generated document for {langCode}: {outputPath}");
            }

            I18N.SetLanguage(originalLanguage);
            SettingSingleton.I.language = originalLanguage;
            AssetDatabase.Refresh();
        }

        private static Dictionary<string, List<ResolvedComponentData>> CollectAllTranslatedData() {
            var languageCodes = new List<string> { "ja-JP", "en-US", "ko-KR" };
            var allTranslatedData = new Dictionary<string, List<ResolvedComponentData>>();

            foreach (var langCode in languageCodes) {
                I18N.SetLanguage(langCode);
                var dataList = CollectTranslatedStrings();
                allTranslatedData.Add(langCode, dataList);
            }

            return allTranslatedData;
        }

        private static List<ResolvedComponentData> CollectTranslatedStrings() {
            var allData = new List<ResolvedComponentData>();
            var componentTypes = new List<Type> {
                typeof(IHierarchyExtensionComponent),
                typeof(IProjectExtensionComponent),
                typeof(IProjectToolbarComponent),
                typeof(IEditorService),
                typeof(IEditorUtility),
                typeof(IAssetManagerComponent)
            };

            foreach (var interfaceType in componentTypes) {
                var implementingTypes = TypeCache.GetTypesDerivedFrom(interfaceType)
                    .Where(t => !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) != null);

                foreach (var type in implementingTypes)
                    try {
                        var instance = Activator.CreateInstance(type);

                        var name = type.GetProperty("Name")?.GetValue(instance) as string;
                        var description = type.GetProperty("Description")?.GetValue(instance) as string;
                        var trigger = type.GetProperty("Trigger")?.GetValue(instance) as string;

                        allData.Add(new ResolvedComponentData {
                            ComponentType = interfaceType.Name.Replace("I", "").Replace("Component", ""),
                            Name = name ?? "-",
                            Description = description ?? "-",
                            Trigger = trigger ?? "-"
                        });
                    }
                    catch (Exception e) {
                        Debug.LogError($"Failed to process component {type.FullName}: {e.Message}");
                    }
            }

            allData.Sort((a, b) => string.Compare(a.ComponentType, b.ComponentType, StringComparison.Ordinal));

            return allData;
        }

        private static string GenerateMarkdown(List<ResolvedComponentData> data, string langCode) {
            var sb = new StringBuilder();

            var title = GetMarkdownTitle(langCode);
            var hierarchyHeader = GetMarkdownHierarchyHeader(langCode);
            var projectHeader = GetMarkdownProjectHeader(langCode);
            var toolbarHeader = GetMarkdownToolbarHeader(langCode);
            var editorServiceHeader = GetMarkdownEditorServiceHeader(langCode);
            var editorUtilityHeader = GetMarkdownEditorUtilityHeader(langCode);
            var assetManagerHeader = GetMarkdownAssetManagerHeader(langCode);

            var nameCol = GetMarkdownNameCol(langCode);
            var descriptionCol = GetMarkdownDescriptionCol(langCode);
            var triggerCol = GetMarkdownTriggerCol(langCode);

            sb.AppendLine(title);
            sb.AppendLine();

            // Hierarchy Extension
            sb.AppendLine(hierarchyHeader);
            sb.AppendLine();
            sb.AppendLine($"| {nameCol} | {descriptionCol} | {triggerCol} |");
            sb.AppendLine("| :--- | :--- | :--- |");

            var hierarchyComponents = data.Where(d => d.ComponentType.Contains("HierarchyExtension")).ToList();
            foreach (var comp in hierarchyComponents)
                sb.AppendLine($"| **{comp.Name.Trim()}** | {comp.Description.Trim()} | {comp.Trigger.Trim()} |");

            sb.AppendLine();

            // Project Extension
            sb.AppendLine(projectHeader);
            sb.AppendLine();
            sb.AppendLine($"| {nameCol} | {descriptionCol} | {triggerCol} |");
            sb.AppendLine("| :--- | :--- | :--- |");

            var projectComponents = data.Where(d => d.ComponentType.Contains("ProjectExtension")).ToList();
            foreach (var comp in projectComponents)
                sb.AppendLine($"| **{comp.Name.Trim()}** | {comp.Description.Trim()} | {comp.Trigger.Trim()} |");

            sb.AppendLine();

            // Project Toolbar
            sb.AppendLine(toolbarHeader);
            sb.AppendLine();
            sb.AppendLine($"| {nameCol} | {descriptionCol} | {triggerCol} |");
            sb.AppendLine("| :--- | :--- | :--- |");

            var toolbarComponents = data.Where(d => d.ComponentType.Contains("ProjectToolbar")).ToList();
            foreach (var comp in toolbarComponents)
                sb.AppendLine($"| **{comp.Name.Trim()}** | {comp.Description.Trim()} | {comp.Trigger.Trim()} |");

            sb.AppendLine();

            // Asset Manager
            sb.AppendLine(assetManagerHeader);
            sb.AppendLine();
            sb.AppendLine($"| {nameCol} | {descriptionCol} |");
            sb.AppendLine("| :--- | :--- |");

            var assetManagerComponents = data.Where(d => d.ComponentType.Contains("AssetManager")).ToList();
            foreach (var comp in assetManagerComponents)
                sb.AppendLine($"| **{comp.Name.Trim()}** | {comp.Description.Trim()} |");

            sb.AppendLine();

            // Editor Service
            sb.AppendLine(editorServiceHeader);
            sb.AppendLine();
            sb.AppendLine($"| {nameCol} | {descriptionCol} | {triggerCol} |");
            sb.AppendLine("| :--- | :--- | :--- |");

            var editorService = data.Where(d => d.ComponentType.Contains("EditorService")).ToList();
            foreach (var comp in editorService)
                sb.AppendLine($"| **{comp.Name.Trim()}** | {comp.Description.Trim()} | {comp.Trigger.Trim()} |");

            sb.AppendLine();

            // Editor Utility
            sb.AppendLine(editorUtilityHeader);
            sb.AppendLine();
            sb.AppendLine($"| {nameCol} | {descriptionCol} | {triggerCol} |");
            sb.AppendLine("| :--- | :--- | :--- |");

            var editorUtility = data.Where(d => d.ComponentType.Contains("EditorUtility")).ToList();
            foreach (var comp in editorUtility)
                sb.AppendLine($"| **{comp.Name.Trim()}** | {comp.Description.Trim()} | {comp.Trigger.Trim()} |");

            return sb.ToString();
        }

        private static string GetMarkdownTitle(string langCode) {
            return langCode switch {
                "ja-JP" => "# Unity Editor 拡張機能コンポーネント一覧",
                "en-US" => "# Unity Editor Extension Component List",
                "ko-KR" => "# Unity Editor 확장 기능 컴포넌트 목록",
                _       => "# Unity Editor Extension Component List"
            };
        }

        private static string GetMarkdownHierarchyHeader(string langCode) {
            return langCode switch {
                "ja-JP" => "## Hierarchy 拡張機能",
                "en-US" => "## Hierarchy Extensions",
                "ko-KR" => "## Hierarchy 확장 기능",
                _       => "## Hierarchy Extensions"
            };
        }

        private static string GetMarkdownProjectHeader(string langCode) {
            return langCode switch {
                "ja-JP" => "## Project 拡張機能",
                "en-US" => "## Project Extensions",
                "ko-KR" => "## Project 확장 기능",
                _       => "## Project Extensions"
            };
        }

        private static string GetMarkdownToolbarHeader(string langCode) {
            return langCode switch {
                "ja-JP" => "## Project Toolbar 拡張機能",
                "en-US" => "## Project Toolbar Extensions",
                "ko-KR" => "## Project Toolbar 확장 기능",
                _       => "## Project Toolbar Extensions"
            };
        }

        private static string GetMarkdownAssetManagerHeader(string langCode) {
            return langCode switch {
                "ja-JP" => "## Asset Manager コンポーネント",
                "en-US" => "## Asset Manager Components",
                "ko-KR" => "## Asset Manager 컴포넌트",
                _       => "## Asset Manager Components"
            };
        }

        private static string GetMarkdownEditorServiceHeader(string langCode) {
            return langCode switch {
                "ja-JP" => "## Editor Service 拡張機能",
                "en-US" => "## Editor Service Extensions",
                "ko-KR" => "## Editor Service 확장 기능",
                _       => "## Editor Service Extensions"
            };
        }

        public static string GetMarkdownEditorUtilityHeader(string langCode) {
            return langCode switch {
                "ja-JP" => "## Editor Utility 拡張機能",
                "en-US" => "## Editor Utility Extensions",
                "ko-KR" => "## Editor Utility 확장 기능",
                _       => "## Editor Utility Extensions"
            };
        }

        private static string GetMarkdownNameCol(string langCode) {
            return langCode switch {
                "ja-JP" => "名称 (Name)",
                "en-US" => "Name",
                "ko-KR" => "이름 (Name)",
                _       => "Name"
            };
        }

        private static string GetMarkdownDescriptionCol(string langCode) {
            return langCode switch {
                "ja-JP" => "機能 (Description)",
                "en-US" => "Description",
                "ko-KR" => "기능 (Description)",
                _       => "機能 (Description)"
            };
        }

        private static string GetMarkdownTriggerCol(string langCode) {
            return langCode switch {
                "ja-JP" => "起動条件 (Trigger)",
                "en-US" => "Trigger",
                "ko-KR" => "작동 조건 (Trigger)",
                _       => "Trigger"
            };
        }

        private struct ResolvedComponentData {
            public string ComponentType;
            public string Name;
            public string Description;
            public string Trigger;
        }
    }
}