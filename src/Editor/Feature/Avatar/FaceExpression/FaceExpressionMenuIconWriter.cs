using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal static class FaceExpressionMenuIconWriter
    {
        private static readonly Dictionary<string, Hash128> Signatures = new Dictionary<string, Hash128>();
        private static readonly HashSet<string> FailedIcons = new HashSet<string>();

        internal static IReadOnlyList<Texture2D> Write(
            GameObject avatar,
            IReadOnlyList<GestureMatrixControllerWriter.EffectiveMenuEntry> entries,
            FaceExpressionGenerationPaths paths,
            bool render = true)
        {
            return Write(avatar, entries, paths, render, int.MaxValue, out _);
        }

        internal static IReadOnlyList<Texture2D> Write(
            GameObject avatar,
            IReadOnlyList<GestureMatrixControllerWriter.EffectiveMenuEntry> entries,
            FaceExpressionGenerationPaths paths,
            bool render,
            int renderLimit,
            out bool pending,
            FaceExpressionPreview sharedPreview = null)
        {
            pending = false;
            var renderedCount = 0;
            var result = new Texture2D[entries.Count];
            if (!render)
            {
                if (!FaceExpressionSettings.GetMenuIconsDisabled())
                    for (var index = 0; index < entries.Count; index++)
                        result[index] = AssetDatabase.LoadAssetAtPath<Texture2D>(GetAssetPath(entries[index].Assignment.Clip, paths));
                return result;
            }
            var usedPaths = new HashSet<string>(StringComparer.Ordinal);
            if (FaceExpressionSettings.GetMenuIconsDisabled() || entries.Count == 0)
            {
                DeleteUnused(paths, usedPaths);
                return result;
            }

            var rendererPaths = FaceExpressionClipEditor.GetRendererPaths(avatar);
            var iconsByClip = new Dictionary<AnimationClip, Texture2D>();
            var avatarSignature = FaceExpressionPreview.GetThumbnailSourceSignature(avatar);
            var preview = sharedPreview;
            try
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    var clip = entries[index].Assignment.Clip;
                    if (iconsByClip.TryGetValue(clip, out var cached))
                    {
                        result[index] = cached;
                        continue;
                    }

                    var assetPath = GetAssetPath(clip, paths);
                    usedPaths.Add(assetPath);
                    var signature = Hash128.Compute(avatarSignature.ToString() + EditorJsonUtility.ToJson(clip));
                    var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (Signatures.TryGetValue(assetPath, out var saved) && saved == signature &&
                        (icon != null || FailedIcons.Contains(assetPath) && renderLimit != int.MaxValue))
                    {
                        iconsByClip.Add(clip, icon);
                        result[index] = icon;
                        continue;
                    }
                    if (renderedCount >= renderLimit)
                    {
                        pending = true;
                        iconsByClip.Add(clip, icon);
                        result[index] = icon;
                        continue;
                    }
                    renderedCount++;
                    if (preview == null)
                    {
                        preview = new FaceExpressionPreview(null);
                        preview.SetAvatar(avatar);
                    }
                    preview.PrepareThumbnailSource(avatarSignature);
                    icon = RenderAndSave(
                        avatar,
                        preview,
                        rendererPaths,
                        clip,
                        paths,
                        usedPaths);
                    Signatures[assetPath] = signature;
                    if (icon == null) FailedIcons.Add(assetPath);
                    else FailedIcons.Remove(assetPath);
                    iconsByClip.Add(clip, icon);
                    result[index] = icon;
                }
            }
            finally { if (sharedPreview == null) preview?.Dispose(); }

            DeleteUnused(paths, usedPaths);
            return result;
        }

        private static string GetAssetPath(AnimationClip clip, FaceExpressionGenerationPaths paths)
        {
            var identity = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long localId)
                ? guid + "-" + localId
                : clip.GetInstanceID().ToString();
            return paths.AssetsFolder + "/" + paths.RootName + " Icon " + identity + ".png";
        }

        private static Texture2D RenderAndSave(
            GameObject avatar,
            FaceExpressionPreview preview,
            IReadOnlyList<string> rendererPaths,
            AnimationClip clip,
            FaceExpressionGenerationPaths paths,
            ISet<string> usedPaths)
        {
            Texture2D rendered = null;
            try
            {
                var channels = FaceExpressionClipEditor.Read(
                    avatar,
                    clip,
                    Array.Empty<string>(),
                    rendererPaths);
                rendered = preview.RenderThumbnail(channels, 256, 256);
                if (rendered == null)
                {
                    return null;
                }

                var assetPath = GetAssetPath(clip, paths);
                var fullPath = Path.Combine(
                    Application.dataPath,
                    assetPath.Substring("Assets/".Length)
                        .Replace('/', Path.DirectorySeparatorChar));
                File.WriteAllBytes(fullPath, rendered.EncodeToPNG());
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ConfigureImporter(assetPath);
                usedPaths.Add(assetPath);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "A FacialSet menu icon could not be generated: " +
                    exception.Message);
                return null;
            }
            finally
            {
                if (rendered != null)
                {
                    UnityEngine.Object.DestroyImmediate(rendered);
                }
            }
        }

        private static void DeleteUnused(
            FaceExpressionGenerationPaths paths,
            ISet<string> usedPaths)
        {
            var folder = paths.AssetsFolder.TrimEnd('/');
            var currentPrefix = paths.RootName + " Icon ";
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(
                        Path.GetDirectoryName(path)?.Replace('\\', '/'),
                        folder,
                        StringComparison.Ordinal) ||
                    usedPaths.Contains(path) ||
                    !name.StartsWith(currentPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(path);
                Signatures.Remove(path);
                FailedIcons.Remove(path);
            }
        }

        private static void ConfigureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }
}
