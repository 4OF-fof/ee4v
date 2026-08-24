using System.IO;
using System.Linq;
using Ee4v.Core.Settings;
using UnityEngine;

namespace Ee4v.PhysBoneCollider
{
    internal readonly struct PhysBoneColliderGenerationPaths
    {
        private PhysBoneColliderGenerationPaths(
            string rootName,
            string prefabPath)
        {
            RootName = rootName;
            PrefabPath = prefabPath;
        }

        internal string RootName { get; }
        internal string PrefabPath { get; }

        internal static PhysBoneColliderGenerationPaths Create(
            GameObject avatar,
            bool ensureFolders = false)
        {
            var safeName = SanitizeFileName(avatar == null ? null : avatar.name);
            var relativeFolder = "Avatar/PhysBoneCollider/" + safeName;
            var folder = ensureFolders
                ? ProjectAssetSettings.EnsureAssetFolder(relativeFolder)
                : ProjectAssetSettings.GetAssetFolder(relativeFolder);
            var rootName = safeName + "_PhysBoneColliders";
            return new PhysBoneColliderGenerationPaths(
                rootName,
                folder + "/" + rootName + ".prefab");
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
