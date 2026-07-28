using System;
using System.Text.RegularExpressions;
using Ee4v.Core.Internal;
using UnityEditor;
using UnityEngine;

namespace Ee4v.UI
{
    internal enum UiFluentIcon
    {
        Add,
        AddCircle,
        Subtract,
        SubtractCircle,
        Dismiss,
        DismissCircle,
        Checkmark,
        CheckmarkCircle,
        Search,
        Filter,
        FilterDismiss,
        ArrowLeft,
        ArrowRight,
        ArrowUp,
        ArrowDown,
        ArrowDownload,
        ArrowUpload,
        ArrowImport,
        ArrowExport,
        ChevronLeft,
        ChevronRight,
        ChevronUp,
        ChevronDown,
        Navigation,
        Home,
        MoreHorizontal,
        MoreVertical,
        Settings,
        Options,
        ArrowClockwise,
        ArrowCounterclockwise,
        History,
        Save,
        Edit,
        Delete,
        Copy,
        Clipboard,
        Share,
        Open,
        WindowNew,
        Link,
        LinkDismiss,
        Attach,
        Pin,
        PinOff,
        Star,
        StarOff,
        Heart,
        Eye,
        EyeOff,
        LockClosed,
        LockOpen,
        Key,
        Warning,
        Info,
        Question,
        ErrorCircle,
        Lightbulb,
        Color,
        PaintBrush,
        Image,
        ImageMultiple,
        Camera,
        Video,
        MusicNote2,
        Speaker2,
        Document,
        DocumentText,
        DocumentCode,
        DocumentMultiple,
        Folder,
        FolderBranchFork,
        FolderLayer,
        FolderOpen,
        FolderMultiple,
        FolderZip,
        Archive,
        ArchiveMultiple,
        Box,
        BoxMultiple,
        Cube,
        CubeMultiple,
        Library,
        Apps,
        Grid,
        List,
        Table,
        Tag,
        TagMultiple,
        Database,
        Cloud,
        CloudArrowDown,
        CloudArrowUp,
        Stack,
        Layer,
        Branch,
        BranchCompare,
        BranchFork,
        Collections,
        Group,
        Window,
        Wrench
    }

    internal static class UiFluentIconResolver
    {
        private const string RelativePngDirectory =
            "Editor/ThirdParty/" +
            "FluentUiSystemIcons/Png512";
        private static readonly Regex PascalBoundaryRegex =
            new Regex(
                "([a-z0-9])([A-Z])",
                RegexOptions.Compiled);
        private static readonly Regex NumberBoundaryRegex =
            new Regex(
                "([A-Za-z])([0-9])",
                RegexOptions.Compiled);

        public static bool TryResolve(
            UiFluentIcon icon,
            out Texture2D texture)
        {
            var assetPath = GetAssetPath(icon);
            texture =
                string.IsNullOrWhiteSpace(assetPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<
                        Texture2D>(assetPath);
            return texture != null;
        }

        internal static string GetAssetPath(
            UiFluentIcon icon)
        {
            var root =
                PackagePathUtility
                    .GetPackageRootAssetPath();
            return string.IsNullOrWhiteSpace(root)
                ? null
                : root.TrimEnd('/') +
                  "/" +
                  RelativePngDirectory +
                  "/" +
                  ToSnakeCase(icon.ToString()) +
                  ".png";
        }

        internal static string ToSnakeCase(string value)
        {
            var withWordBoundaries =
                PascalBoundaryRegex.Replace(
                    value ?? string.Empty,
                    "$1_$2");
            return NumberBoundaryRegex.Replace(
                    withWordBoundaries,
                    "$1_$2")
                .ToLowerInvariant();
        }
    }
}
