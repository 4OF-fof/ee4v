using System;
using System.Collections.Generic;
using System.IO;
using Ee4v.AssetManager.Contracts;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetFileIconResolver
    {
        private const string DocumentIcon = "document.png";
        private const string ImageIcon = "image.png";
        private const string VideoIcon = "video.png";
        private const string AudioIcon = "music_note_2.png";
        private const string ArchiveIcon = "folder_zip.png";
        private const string CodeIcon = "code.png";
        private const string ModelIcon = "cube.png";

        private static readonly HashSet<string> ImageExtensions =
            CreateExtensions(
                "png", "jpg", "jpeg", "gif", "bmp", "tga", "tif",
                "tiff", "webp", "svg", "psd", "exr", "hdr");
        private static readonly HashSet<string> VideoExtensions =
            CreateExtensions(
                "mp4", "mov", "avi", "webm", "mkv", "m4v", "wmv");
        private static readonly HashSet<string> AudioExtensions =
            CreateExtensions(
                "mp3", "wav", "ogg", "flac", "m4a", "aac", "aiff");
        private static readonly HashSet<string> ArchiveExtensions =
            CreateExtensions(
                "zip", "7z", "rar", "tar", "gz", "bz2",
                "unitypackage");
        private static readonly HashSet<string> CodeExtensions =
            CreateExtensions(
                "cs", "shader", "hlsl", "cg", "compute", "json",
                "jsonc", "yaml", "yml", "xml", "txt", "md", "asmdef",
                "uss", "uxml");
        private static readonly HashSet<string> ModelExtensions =
            CreateExtensions(
                "fbx", "obj", "blend", "dae", "3ds", "gltf", "glb");
        private static readonly Dictionary<string, Texture2D> Textures =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public static Texture2D Resolve(AssetFile file)
        {
            var iconFileName = GetIconFileName(
                file?.FileName,
                file?.SourcePath);
            if (!Textures.TryGetValue(iconFileName, out var texture))
            {
                texture = AssetManagerControls.LoadFluentIconTexture(
                    iconFileName);
                Textures.Add(iconFileName, texture);
            }

            if (texture != null || iconFileName == DocumentIcon)
            {
                return texture;
            }

            return ResolveDocumentIcon();
        }

        internal static string GetIconFileName(
            string fileName,
            string sourcePath = null)
        {
            var extension = GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = GetExtension(sourcePath);
            }

            if (ImageExtensions.Contains(extension))
            {
                return ImageIcon;
            }
            if (VideoExtensions.Contains(extension))
            {
                return VideoIcon;
            }
            if (AudioExtensions.Contains(extension))
            {
                return AudioIcon;
            }
            if (ArchiveExtensions.Contains(extension))
            {
                return ArchiveIcon;
            }
            if (CodeExtensions.Contains(extension))
            {
                return CodeIcon;
            }
            if (ModelExtensions.Contains(extension))
            {
                return ModelIcon;
            }

            return DocumentIcon;
        }

        private static Texture2D ResolveDocumentIcon()
        {
            if (!Textures.TryGetValue(DocumentIcon, out var texture))
            {
                texture = AssetManagerControls.LoadFluentIconTexture(
                    DocumentIcon);
                Textures.Add(DocumentIcon, texture);
            }
            return texture;
        }

        private static string GetExtension(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : Path.GetExtension(path)
                    .TrimStart('.')
                    .ToLowerInvariant();
        }

        private static HashSet<string> CreateExtensions(
            params string[] extensions)
        {
            return new HashSet<string>(
                extensions,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
