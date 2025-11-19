using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.OldData;

namespace _4OF.ee4v.AssetManager.Service {
    internal static class TagService {
        public static void RenameTag(string oldTag, string newTag) {
            if (string.IsNullOrEmpty(oldTag) || string.IsNullOrEmpty(newTag) || oldTag == newTag) return;
            AssetLibrary.Instance.RenameTag(oldTag, newTag);
            AssetLibrarySerializer.SaveLibrary();
        }
    }
}