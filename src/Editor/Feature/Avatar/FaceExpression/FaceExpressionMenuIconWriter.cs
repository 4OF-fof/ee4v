using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal static class FaceExpressionMenuIconWriter
    {
        internal static IReadOnlyList<Texture2D> Write(
            GameObject avatar,
            IReadOnlyList<GestureMatrixControllerWriter.EffectiveMenuEntry> entries,
            FaceExpressionGenerationPaths paths)
        {
            var result = new Texture2D[entries.Count];
            var usedPaths = new HashSet<string>(StringComparer.Ordinal);
            if (FaceExpressionSettings.GetMenuIconsDisabled() || entries.Count == 0)
            {
                DeleteUnused(paths, usedPaths);
                return result;
            }

            var rendererPaths = FaceExpressionClipEditor.GetRendererPaths(avatar);
            var iconsByClip = new Dictionary<AnimationClip, Texture2D>();
            using (var preview = new FaceExpressionPreview(null))
            {
                preview.SetAvatar(avatar);
                for (var index = 0; index < entries.Count; index++)
                {
                    var clip = entries[index].Assignment.Clip;
                    if (iconsByClip.TryGetValue(clip, out var cached))
                    {
                        result[index] = cached;
                        continue;
                    }

                    var icon = RenderAndSave(
                        avatar,
                        preview,
                        rendererPaths,
                        clip,
                        paths,
                        usedPaths);
                    iconsByClip.Add(clip, icon);
                    result[index] = icon;
                }
            }

            DeleteUnused(paths, usedPaths);
            return result;
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

                var clipPath = AssetDatabase.GetAssetPath(clip);
                var identity = string.IsNullOrEmpty(clipPath)
                    ? clip.GetInstanceID().ToString()
                    : AssetDatabase.AssetPathToGUID(clipPath);
                var assetPath = paths.AssetsFolder + "/" + paths.RootName +
                                " Icon " + identity + ".png";
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
            var safeAvatarName = paths.RootName.EndsWith(
                "_FacialSet",
                StringComparison.Ordinal)
                ? paths.RootName.Substring(
                    0,
                    paths.RootName.Length - "_FacialSet".Length)
                : paths.RootName;
            var legacyPrefix = safeAvatarName + " FacialSet Icon ";
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(
                        Path.GetDirectoryName(path)?.Replace('\\', '/'),
                        folder,
                        StringComparison.Ordinal) ||
                    usedPaths.Contains(path) ||
                    (!name.StartsWith(currentPrefix, StringComparison.Ordinal) &&
                     !name.StartsWith(legacyPrefix, StringComparison.Ordinal)))
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(path);
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
