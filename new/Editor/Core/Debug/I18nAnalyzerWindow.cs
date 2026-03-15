using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ee4v.Core.I18n;
using Ee4v.Core.Internal;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Ee4v.Core.DebugTools
{
    internal sealed class I18nAnalyzerWindow : EditorWindow
    {
        private static readonly Regex TranslationCallRegex =
            new Regex(@"I18N\.(Get|TryGet)\s*\(\s*""([^""]+)""", RegexOptions.Compiled);

        private readonly List<UnresolvedCodeReference> _unresolvedReferences =
            new List<UnresolvedCodeReference>();

        private readonly Dictionary<string, Dictionary<string, List<CodeReference>>> _missingByLocale =
            new Dictionary<string, Dictionary<string, List<CodeReference>>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Dictionary<string, List<LocalizationEntry>>> _unusedByLocale =
            new Dictionary<string, Dictionary<string, List<LocalizationEntry>>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, List<DuplicateTextGroup>> _duplicateTextsByLocale =
            new Dictionary<string, List<DuplicateTextGroup>>(StringComparer.OrdinalIgnoreCase);

        private List<LocalizationDuplicateKey> _duplicateKeys = new List<LocalizationDuplicateKey>();
        private Vector2 _scrollPosition;

        [MenuItem("Debug/I18n Analyzer")]
        private static void ShowWindow()
        {
            GetWindow<I18nAnalyzerWindow>("I18n Analyzer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("I18n Analyzer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Static analysis of literal I18N.Get/TryGet calls. Dynamic keys are not analyzed.",
                MessageType.Info);

            if (GUILayout.Button("Analyze", GUILayout.Height(28f)))
            {
                Analyze();
            }

            EditorGUILayout.Space(6f);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawDuplicateKeys();
            DrawUnresolvedNamespaces();
            DrawMissingKeys();
            DrawUnusedKeys();
            DrawDuplicateTexts();
            EditorGUILayout.EndScrollView();
        }

        private void Analyze()
        {
            _unresolvedReferences.Clear();
            _missingByLocale.Clear();
            _unusedByLocale.Clear();
            _duplicateTextsByLocale.Clear();

            var snapshot = LocalizationCatalogLoader.Load();
            _duplicateKeys = snapshot.DuplicateKeys.ToList();

            var codeReferences = ScanCodeReferences();
            BuildMissing(snapshot, codeReferences);
            BuildUnused(snapshot, codeReferences);
            BuildDuplicateTexts(snapshot);
        }

        private List<CodeReference> ScanCodeReferences()
        {
            var results = new List<CodeReference>();
            var editorRoot = PackagePathUtility.GetEditorRootFullPath();
            if (string.IsNullOrWhiteSpace(editorRoot) || !Directory.Exists(editorRoot))
            {
                return results;
            }

            var scripts = Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in scripts)
            {
                var namespaceName = PackagePathUtility.GetNamespaceForSourceFile(filePath);
                var scope = PackagePathUtility.GetScopeNameForSourceFile(filePath);
                var lines = File.ReadAllLines(filePath);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var matches = TranslationCallRegex.Matches(lines[lineIndex]);
                    foreach (Match match in matches)
                    {
                        if (!match.Success || match.Groups.Count < 3)
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(scope))
                        {
                            _unresolvedReferences.Add(new UnresolvedCodeReference(
                                namespaceName,
                                match.Groups[2].Value,
                                filePath,
                                lineIndex + 1));
                            continue;
                        }

                        results.Add(new CodeReference(
                            scope,
                            match.Groups[2].Value,
                            filePath,
                            lineIndex + 1));
                    }
                }
            }

            return results;
        }

        private void BuildMissing(LocalizationCatalogSnapshot snapshot, IReadOnlyList<CodeReference> references)
        {
            var groupedByScope = references
                .GroupBy(reference => reference.Scope, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var localePair in snapshot.Locales)
            {
                var localeMissing = new Dictionary<string, List<CodeReference>>(StringComparer.OrdinalIgnoreCase);
                foreach (var scopePair in groupedByScope)
                {
                    LocalizationScopeCatalog scopeCatalog = null;
                    if (localePair.Value.Scopes.ContainsKey(scopePair.Key))
                    {
                        scopeCatalog = localePair.Value.Scopes[scopePair.Key];
                    }

                    var missing = scopePair.Value
                        .Where(reference => scopeCatalog == null || !scopeCatalog.Entries.ContainsKey(reference.Key))
                        .OrderBy(reference => reference.Key, StringComparer.Ordinal)
                        .ThenBy(reference => reference.FilePath, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (missing.Count > 0)
                    {
                        localeMissing.Add(scopePair.Key, missing);
                    }
                }

                _missingByLocale[localePair.Key] = localeMissing;
            }
        }

        private void BuildUnused(LocalizationCatalogSnapshot snapshot, IReadOnlyList<CodeReference> references)
        {
            var usedKeysByScope = references
                .GroupBy(reference => reference.Scope, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => new HashSet<string>(group.Select(reference => reference.Key), StringComparer.Ordinal),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var localePair in snapshot.Locales)
            {
                var localeUnused = new Dictionary<string, List<LocalizationEntry>>(StringComparer.OrdinalIgnoreCase);
                foreach (var scopePair in localePair.Value.Scopes)
                {
                    HashSet<string> usedKeys;
                    if (!usedKeysByScope.TryGetValue(scopePair.Key, out usedKeys))
                    {
                        usedKeys = new HashSet<string>(StringComparer.Ordinal);
                    }

                    var unused = scopePair.Value.Entries.Values
                        .Where(entry => !usedKeys.Contains(entry.Key))
                        .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                        .ToList();

                    if (unused.Count > 0)
                    {
                        localeUnused.Add(scopePair.Key, unused);
                    }
                }

                _unusedByLocale[localePair.Key] = localeUnused;
            }
        }

        private void BuildDuplicateTexts(LocalizationCatalogSnapshot snapshot)
        {
            foreach (var localePair in snapshot.Locales)
            {
                var groups = localePair.Value.Scopes.Values
                    .SelectMany(scope => scope.Entries.Values)
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                    .GroupBy(entry => entry.Value, StringComparer.Ordinal)
                    .Select(group => new DuplicateTextGroup(group.Key, group.OrderBy(entry => entry.Scope, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Key, StringComparer.Ordinal).ToList()))
                    .Where(group => group.Entries
                        .Select(entry => entry.Scope)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count() > 1)
                    .OrderBy(group => group.Value, StringComparer.Ordinal)
                    .ToList();

                _duplicateTextsByLocale[localePair.Key] = groups;
            }
        }

        private void DrawDuplicateKeys()
        {
            if (_duplicateKeys.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Duplicate Keys In Same Scope", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (var i = 0; i < _duplicateKeys.Count; i++)
                {
                    var duplicate = _duplicateKeys[i];
                    EditorGUILayout.LabelField(
                        "[" + duplicate.Locale + "][" + duplicate.Scope + "] " + duplicate.Key,
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Original: " + duplicate.OriginalFilePath, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("Duplicate: " + duplicate.DuplicateFilePath, EditorStyles.miniLabel);
                    EditorGUILayout.Space(4f);
                }
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawUnresolvedNamespaces()
        {
            if (_unresolvedReferences.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Unresolved Namespaces", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (var i = 0; i < _unresolvedReferences.Count; i++)
                {
                    var reference = _unresolvedReferences[i];
                    var namespaceLabel = string.IsNullOrWhiteSpace(reference.NamespaceName)
                        ? "(no namespace)"
                        : reference.NamespaceName;

                    EditorGUILayout.LabelField(namespaceLabel + " -> " + reference.Key, EditorStyles.boldLabel);
                    DrawJumpableReference(reference.FilePath, reference.LineNumber, reference.Key);
                    EditorGUILayout.Space(4f);
                }
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawMissingKeys()
        {
            EditorGUILayout.LabelField("Missing Keys", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (var localePair in _missingByLocale.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var scopePair in localePair.Value.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        EditorGUILayout.LabelField(
                            "[" + localePair.Key + "][" + scopePair.Key + "] " + scopePair.Value.Count + " item(s)",
                            EditorStyles.boldLabel);

                        for (var i = 0; i < scopePair.Value.Count; i++)
                        {
                            var reference = scopePair.Value[i];
                            DrawJumpableReference(reference.FilePath, reference.LineNumber, reference.Key);
                        }

                        EditorGUILayout.Space(4f);
                    }
                }
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawUnusedKeys()
        {
            EditorGUILayout.LabelField("Unused Keys", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (var localePair in _unusedByLocale.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var scopePair in localePair.Value.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        EditorGUILayout.LabelField(
                            "[" + localePair.Key + "][" + scopePair.Key + "] " + scopePair.Value.Count + " item(s)",
                            EditorStyles.boldLabel);

                        for (var i = 0; i < scopePair.Value.Count; i++)
                        {
                            var entry = scopePair.Value[i];
                            DrawJumpableReference(entry.FilePath, 1, entry.Key);
                        }

                        EditorGUILayout.Space(4f);
                    }
                }
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawDuplicateTexts()
        {
            EditorGUILayout.LabelField("Duplicate Texts Across Scopes", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (var localePair in _duplicateTextsByLocale.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                {
                    for (var i = 0; i < localePair.Value.Count; i++)
                    {
                        var group = localePair.Value[i];
                        EditorGUILayout.LabelField("[" + localePair.Key + "] " + group.Value, EditorStyles.boldLabel);
                        for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                        {
                            var entry = group.Entries[entryIndex];
                            EditorGUILayout.LabelField(
                                "  [" + entry.Scope + "] " + entry.Key + " (" + entry.FilePath + ")",
                                EditorStyles.miniLabel);
                        }

                        EditorGUILayout.Space(4f);
                    }
                }
            }
        }

        private static void DrawJumpableReference(string filePath, int lineNumber, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Jump", GUILayout.Width(45f)))
                {
                    OpenAtLine(filePath, lineNumber);
                }

                EditorGUILayout.LabelField(label + " [" + Path.GetFileName(filePath) + ":" + lineNumber + "]", EditorStyles.miniLabel);
            }
        }

        private static void OpenAtLine(string filePath, int lineNumber)
        {
            var relativePath = GetRelativePath(filePath);
            var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(relativePath);
            if (scriptAsset != null)
            {
                AssetDatabase.OpenAsset(scriptAsset, lineNumber);
                return;
            }

            InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
        }

        private static string GetRelativePath(string fullPath)
        {
            var packageRoot = PackagePathUtility.GetPackageRootFullPath();
            if (string.IsNullOrWhiteSpace(packageRoot))
            {
                return fullPath;
            }

            var normalizedPackageRoot = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedFullPath = Path.GetFullPath(fullPath);
            if (!normalizedFullPath.StartsWith(normalizedPackageRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            var relativePath = normalizedFullPath.Substring(normalizedPackageRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(PackagePathUtility.GetPackageRootAssetPath(), relativePath).Replace('\\', '/');
        }

        private sealed class CodeReference
        {
            public CodeReference(string scope, string key, string filePath, int lineNumber)
            {
                Scope = scope;
                Key = key;
                FilePath = filePath;
                LineNumber = lineNumber;
            }

            public string Scope { get; }

            public string Key { get; }

            public string FilePath { get; }

            public int LineNumber { get; }
        }

        private sealed class UnresolvedCodeReference
        {
            public UnresolvedCodeReference(string namespaceName, string key, string filePath, int lineNumber)
            {
                NamespaceName = namespaceName;
                Key = key;
                FilePath = filePath;
                LineNumber = lineNumber;
            }

            public string NamespaceName { get; }

            public string Key { get; }

            public string FilePath { get; }

            public int LineNumber { get; }
        }

        private sealed class DuplicateTextGroup
        {
            public DuplicateTextGroup(string value, List<LocalizationEntry> entries)
            {
                Value = value;
                Entries = entries;
            }

            public string Value { get; }

            public List<LocalizationEntry> Entries { get; }
        }
    }
}
