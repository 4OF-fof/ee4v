using System.IO;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Service {
    internal static class AssetValidationService {
        public static bool IsValidAssetName(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                Debug.LogError("Asset name cannot be empty or whitespace.");
                return false;
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var found = name.IndexOfAny(invalidChars);
            if (found < 0) return true;
            Debug.LogError($"Asset name '{name}' contains invalid filename character '{name[found]}'.");
            return false;
        }
    }
}