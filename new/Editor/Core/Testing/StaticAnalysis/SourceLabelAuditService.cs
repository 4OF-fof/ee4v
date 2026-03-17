using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ee4v.Core.Internal;

namespace Ee4v.Core.Testing.StaticAnalysis
{
    internal sealed class SourceLabelAuditViolation
    {
        public SourceLabelAuditViolation(string relativePath)
        {
            RelativePath = relativePath ?? string.Empty;
        }

        public string RelativePath { get; }
    }

    internal sealed class SourceLabelAuditReport
    {
        public SourceLabelAuditReport(IReadOnlyList<SourceLabelAuditViolation> violations)
        {
            Violations = violations ?? Array.Empty<SourceLabelAuditViolation>();
        }

        public IReadOnlyList<SourceLabelAuditViolation> Violations { get; }
    }

    internal static class SourceLabelAuditService
    {
        private static readonly Regex NewLabelPattern = new Regex(@"\bnew\s+Label\s*\(", RegexOptions.Compiled);
        private static readonly Regex InheritLabelPattern = new Regex(@":\s*Label\b", RegexOptions.Compiled);

        public static SourceLabelAuditReport Analyze()
        {
            var editorRoot = PackagePathUtility.GetEditorRootFullPath();
            if (string.IsNullOrWhiteSpace(editorRoot) || !Directory.Exists(editorRoot))
            {
                return new SourceLabelAuditReport(Array.Empty<SourceLabelAuditViolation>());
            }

            var violations = Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new
                {
                    FullPath = path,
                    RelativePath = NormalizeRelativePath(GetRelativePath(editorRoot, path))
                })
                .Where(item => !IsAllowedFile(item.RelativePath))
                .Where(item => HasLabelUsage(item.FullPath))
                .Select(item => new SourceLabelAuditViolation(item.RelativePath))
                .ToArray();

            return new SourceLabelAuditReport(violations);
        }

        private static bool HasLabelUsage(string filePath)
        {
            var source = File.ReadAllText(filePath);
            return NewLabelPattern.IsMatch(source) || InheritLabelPattern.IsMatch(source);
        }

        private static bool IsAllowedFile(string relativePath)
        {
            return string.Equals(
                relativePath,
                NormalizeRelativePath(Path.Combine("UI", "Foundation", "Typography", "UiTextFactory.cs")),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRelativePath(string rootPath, string fullPath)
        {
            var normalizedRoot = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedFullPath = Path.GetFullPath(fullPath);
            if (!normalizedFullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFullPath;
            }

            return normalizedFullPath
                .Substring(normalizedRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
