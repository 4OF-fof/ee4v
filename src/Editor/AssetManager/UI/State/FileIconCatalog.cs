using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;

namespace Ee4v.AssetManager.UI
{
    internal enum FileEntryKind
    {
        File,
        Directory,
        VariantGroup,
        VersionGroup
    }

    internal sealed class FileIconDefinition
    {
        public const float StandardIconSize = 88f;

        public FileIconDefinition(
            string id,
            FileEntryKind kind,
            UiFluentIcon icon,
            IEnumerable<string> extensions = null)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            Icon = icon;
            Extensions = (extensions ?? Array.Empty<string>())
                .Select(FileExtensionUtility.Normalize)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string Id { get; }

        public FileEntryKind Kind { get; }

        public UiFluentIcon Icon { get; }

        public float ArtworkIconSize
        {
            get { return StandardIconSize; }
        }

        public IReadOnlyList<string> Extensions { get; }

        public IconState CreateArtworkIconState()
        {
            return IconState.FromFluentIcon(
                Icon,
                ArtworkIconSize);
        }

    }

    internal static class FileIconCatalog
    {
        private static readonly FileIconDefinition[] RegisteredDefinitions =
        {
            Kind(
                "directory",
                FileEntryKind.Directory,
                UiFluentIcon.Folder),
            Kind(
                "variant-group",
                FileEntryKind.VariantGroup,
                UiFluentIcon.FolderBranchFork),
            Kind(
                "version-group",
                FileEntryKind.VersionGroup,
                UiFluentIcon.FolderLayer),
            File(
                "package-archive",
                UiFluentIcon.FolderZip,
                "zip",
                "unitypackage"),
            File(
                "archive",
                UiFluentIcon.Archive,
                "rar",
                "7z",
                "tar",
                "gz"),
            File(
                "image",
                UiFluentIcon.Image,
                "png",
                "jpg",
                "jpeg",
                "gif",
                "webp",
                "psd",
                "clip"),
            File(
                "text",
                UiFluentIcon.DocumentText,
                "txt",
                "md",
                "json",
                "jsonc",
                "xml",
                "yaml",
                "yml"),
            File(
                "unity-asset",
                UiFluentIcon.Apps,
                "unity",
                "asset",
                "prefab",
                "mat",
                "controller",
                "anim"),
            File(
                "model",
                UiFluentIcon.Cube,
                "fbx",
                "obj",
                "blend",
                "vrm",
                "glb",
                "gltf"),
            File(
                "audio",
                UiFluentIcon.MusicNote2,
                "wav",
                "mp3",
                "ogg",
                "aiff"),
            File(
                "code",
                UiFluentIcon.DocumentCode,
                "cs",
                "js",
                "ts",
                "shader",
                "cginc"),
            File(
                "file",
                UiFluentIcon.Document)
        };

        private static readonly IReadOnlyList<FileIconDefinition>
            ReadOnlyDefinitions = Array.AsReadOnly(RegisteredDefinitions);

        public static IReadOnlyList<FileIconDefinition> Definitions
        {
            get { return ReadOnlyDefinitions; }
        }

        public static FileIconDefinition Resolve(
            FileEntryKind kind,
            string extension)
        {
            if (kind != FileEntryKind.File)
            {
                for (var i = 0; i < RegisteredDefinitions.Length; i++)
                {
                    if (RegisteredDefinitions[i].Kind == kind)
                    {
                        return RegisteredDefinitions[i];
                    }
                }
            }

            var normalizedExtension =
                FileExtensionUtility.Normalize(extension);
            FileIconDefinition fallback = null;
            for (var i = 0; i < RegisteredDefinitions.Length; i++)
            {
                var definition = RegisteredDefinitions[i];
                if (definition.Kind != FileEntryKind.File)
                {
                    continue;
                }

                if (definition.Extensions.Count == 0)
                {
                    fallback = definition;
                    continue;
                }

                if (definition.Extensions.Contains(
                        normalizedExtension,
                        StringComparer.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }

            return fallback;
        }

        private static FileIconDefinition Kind(
            string id,
            FileEntryKind kind,
            UiFluentIcon icon)
        {
            return new FileIconDefinition(
                id,
                kind,
                icon);
        }

        private static FileIconDefinition File(
            string id,
            UiFluentIcon icon,
            params string[] extensions)
        {
            return new FileIconDefinition(
                id,
                FileEntryKind.File,
                icon,
                extensions);
        }
    }
}
