using System.IO;
using System.Linq;
using Ee4v.Core.Settings;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal readonly struct FaceExpressionGenerationPaths
    {
        private FaceExpressionGenerationPaths(
            string rootName,
            string prefabPath,
            string assetsFolder,
            string controllerPath,
            string menuPath)
        {
            RootName = rootName;
            PrefabPath = prefabPath;
            AssetsFolder = assetsFolder;
            ControllerPath = controllerPath;
            MenuPath = menuPath;
        }

        internal string RootName { get; }
        internal string PrefabPath { get; }
        internal string AssetsFolder { get; }
        internal string ControllerPath { get; }
        internal string MenuPath { get; }

        internal static FaceExpressionGenerationPaths Create(
            GameObject avatar,
            bool ensureFolders = false)
        {
            var safeName = SanitizeFileName(avatar == null ? null : avatar.name);
            var relativeFolder = "Animation/FacialSet/" + safeName;
            var folder = ensureFolders
                ? ProjectAssetSettings.EnsureAssetFolder(relativeFolder)
                : ProjectAssetSettings.GetAssetFolder(relativeFolder);
            var assets = ensureFolders
                ? ProjectAssetSettings.EnsureAssetFolder(relativeFolder + "/Assets")
                : ProjectAssetSettings.GetAssetFolder(relativeFolder + "/Assets");
            return new FaceExpressionGenerationPaths(
                safeName + "_FacialSet",
                folder + "/" + safeName + "_FacialSet.prefab",
                assets,
                assets + "/" + safeName + " Face Expressions.controller",
                assets + "/" + safeName + " FacialSet Menu.asset");
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string((value ?? "Avatar")
                .Select(character => invalid.Contains(character) ? '_' : character)
                .ToArray());
        }
    }
}
