using System.IO;
using UnityEngine;

namespace _4OF.ee4v.AssetManager {
    public static class AssetValidationService {
        public static bool IsValidAssetName(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                Debug.LogError("Name cannot be empty or whitespace.");
                return false;
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalidChars) < 0) return true;
            Debug.LogError($"Name '{name}' contains invalid characters.");
            return false;
        }
    }
}