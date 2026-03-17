using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ee4v.Core.I18n;
using Ee4v.Core.Internal;

namespace Ee4v.Core.Testing.StaticAnalysis
{
    internal sealed class LocalizationDuplicateKeyIssue
    {
        public LocalizationDuplicateKeyIssue(
            string locale,
            string scope,
            string key,
            string originalFilePath,
            string duplicateFilePath)
        {
            Locale = locale ?? string.Empty;
            Scope = scope ?? string.Empty;
            Key = key ?? string.Empty;
            OriginalFilePath = originalFilePath ?? string.Empty;
            DuplicateFilePath = duplicateFilePath ?? string.Empty;
        }

        public string Locale { get; }

        public string Scope { get; }

        public string Key { get; }

        public string OriginalFilePath { get; }

        public string DuplicateFilePath { get; }
    }

    internal sealed class LocalizationCodeIssue
    {
        public LocalizationCodeIssue(string locale, string scope, string key, string filePath, int lineNumber)
        {
            Locale = locale ?? string.Empty;
            Scope = scope ?? string.Empty;
            Key = key ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            LineNumber = lineNumber;
        }

        public string Locale { get; }

        public string Scope { get; }

        public string Key { get; }

        public string FilePath { get; }

        public int LineNumber { get; }
    }

    internal sealed class LocalizationUnusedKeyIssue
    {
        public LocalizationUnusedKeyIssue(string locale, string scope, string key, string filePath)
        {
            Locale = locale ?? string.Empty;
            Scope = scope ?? string.Empty;
            Key = key ?? string.Empty;
            FilePath = filePath ?? string.Empty;
        }

        public string Locale { get; }

        public string Scope { get; }

        public string Key { get; }

        public string FilePath { get; }
    }

    internal sealed class LocalizationStaticAuditReport
    {
        public LocalizationStaticAuditReport(
            IReadOnlyList<LocalizationDuplicateKeyIssue> duplicateKeys,
            IReadOnlyList<LocalizationCodeIssue> missingKeys,
            IReadOnlyList<LocalizationUnusedKeyIssue> unusedKeys)
        {
            DuplicateKeys = duplicateKeys ?? Array.Empty<LocalizationDuplicateKeyIssue>();
            MissingKeys = missingKeys ?? Array.Empty<LocalizationCodeIssue>();
            UnusedKeys = unusedKeys ?? Array.Empty<LocalizationUnusedKeyIssue>();
        }

        public IReadOnlyList<LocalizationDuplicateKeyIssue> DuplicateKeys { get; }

        public IReadOnlyList<LocalizationCodeIssue> MissingKeys { get; }

        public IReadOnlyList<LocalizationUnusedKeyIssue> UnusedKeys { get; }
    }

    internal static class LocalizationStaticAuditService
    {
        private static readonly Regex TranslationCallRegex =
            new Regex(@"I18N\.(Get|TryGet)\s*\(\s*""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex SettingDefinitionRegex =
            new Regex(
                @"new\s+SettingDefinition<[^>]+>\s*\(\s*""[^""]+""\s*,\s*[^,]+,\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*""([^""]+)""",
                RegexOptions.Compiled | RegexOptions.Singleline);

        public static LocalizationStaticAuditReport Analyze()
        {
            var snapshot = LocalizationCatalogLoader.Load();
            var codeReferences = ScanCodeReferences();

            return new LocalizationStaticAuditReport(
                snapshot.DuplicateKeys
                    .Select(issue => new LocalizationDuplicateKeyIssue(
                        issue.Locale,
                        issue.Scope,
                        issue.Key,
                        ToPackageRelativePath(issue.OriginalFilePath),
                        ToPackageRelativePath(issue.DuplicateFilePath)))
                    .ToArray(),
                BuildMissing(snapshot, codeReferences),
                BuildUnused(snapshot, codeReferences));
        }

        private static IReadOnlyList<LocalizationCodeIssue> BuildMissing(
            LocalizationCatalogSnapshot snapshot,
            IReadOnlyList<CodeReference> references)
        {
            var issues = new List<LocalizationCodeIssue>();
            var groupedByScope = references
                .GroupBy(reference => reference.Scope, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var localePair in snapshot.Locales.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var scopePair in groupedByScope.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                {
                    LocalizationScopeCatalog scopeCatalog = null;
                    if (localePair.Value.Scopes.ContainsKey(scopePair.Key))
                    {
                        scopeCatalog = localePair.Value.Scopes[scopePair.Key];
                    }

                    issues.AddRange(scopePair.Value
                        .Where(reference => scopeCatalog == null || !scopeCatalog.Entries.ContainsKey(reference.Key))
                        .OrderBy(reference => reference.Key, StringComparer.Ordinal)
                        .ThenBy(reference => reference.FilePath, StringComparer.OrdinalIgnoreCase)
                        .Select(reference => new LocalizationCodeIssue(
                            localePair.Key,
                            scopePair.Key,
                            reference.Key,
                            reference.FilePath,
                            reference.LineNumber)));
                }
            }

            return issues;
        }

        private static IReadOnlyList<LocalizationUnusedKeyIssue> BuildUnused(
            LocalizationCatalogSnapshot snapshot,
            IReadOnlyList<CodeReference> references)
        {
            var issues = new List<LocalizationUnusedKeyIssue>();
            var usedKeysByScope = references
                .GroupBy(reference => reference.Scope, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => new HashSet<string>(group.Select(reference => reference.Key), StringComparer.Ordinal),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var localePair in snapshot.Locales.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var scopePair in localePair.Value.Scopes.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                {
                    if (!usedKeysByScope.TryGetValue(scopePair.Key, out var usedKeys))
                    {
                        usedKeys = new HashSet<string>(StringComparer.Ordinal);
                    }

                    issues.AddRange(scopePair.Value.Entries.Values
                        .Where(entry => !usedKeys.Contains(entry.Key))
                        .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                        .Select(entry => new LocalizationUnusedKeyIssue(
                            localePair.Key,
                            scopePair.Key,
                            entry.Key,
                            ToPackageRelativePath(entry.FilePath))));
                }
            }

            return issues;
        }

        private static IReadOnlyList<CodeReference> ScanCodeReferences()
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
                var namespaceName = PackagePathUtility.GetDeclaredNamespace(filePath);
                var scope = PackagePathUtility.GetScopeNameForNamespace(namespaceName);
                if (string.IsNullOrWhiteSpace(scope))
                {
                    continue;
                }

                var content = File.ReadAllText(filePath);
                var relativePath = ToPackageRelativePath(filePath);
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var matches = TranslationCallRegex.Matches(lines[lineIndex]);
                    foreach (Match match in matches)
                    {
                        if (!match.Success || match.Groups.Count < 3)
                        {
                            continue;
                        }

                        results.Add(new CodeReference(scope, match.Groups[2].Value, relativePath, lineIndex + 1));
                    }
                }

                var definitionMatches = SettingDefinitionRegex.Matches(content);
                foreach (Match match in definitionMatches)
                {
                    if (!match.Success || match.Groups.Count < 4)
                    {
                        continue;
                    }

                    var lineNumber = GetLineNumber(content, match.Index);
                    results.Add(new CodeReference(scope, match.Groups[1].Value, relativePath, lineNumber));
                    results.Add(new CodeReference(scope, match.Groups[2].Value, relativePath, lineNumber));
                    results.Add(new CodeReference(scope, match.Groups[3].Value, relativePath, lineNumber));
                }
            }

            return results;
        }

        private static string ToPackageRelativePath(string fullPath)
        {
            var packageRoot = PackagePathUtility.GetPackageRootFullPath();
            if (string.IsNullOrWhiteSpace(packageRoot) || string.IsNullOrWhiteSpace(fullPath))
            {
                return fullPath ?? string.Empty;
            }

            var normalizedPackageRoot = Path.GetFullPath(packageRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedFullPath = Path.GetFullPath(fullPath);
            if (!normalizedFullPath.StartsWith(normalizedPackageRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFullPath;
            }

            return normalizedFullPath
                .Substring(normalizedPackageRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
        }

        private static int GetLineNumber(string content, int index)
        {
            var lineNumber = 1;
            for (var i = 0; i < index && i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    lineNumber++;
                }
            }

            return lineNumber;
        }

        private sealed class CodeReference
        {
            public CodeReference(string scope, string key, string filePath, int lineNumber)
            {
                Scope = scope ?? string.Empty;
                Key = key ?? string.Empty;
                FilePath = filePath ?? string.Empty;
                LineNumber = lineNumber;
            }

            public string Scope { get; }

            public string Key { get; }

            public string FilePath { get; }

            public int LineNumber { get; }
        }
    }
}
