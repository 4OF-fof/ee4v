using System.IO;
using Newtonsoft.Json;
using _4OF.ee4v.Core.Data;

namespace _4OF.ee4v.AssetManager.Data {
    public static class AssetController {
        private static readonly string RootDir = Path.Combine(EditorPrefsManager.ContentFolderPath, "AssetManager");
        
        public static void Initialize() {
            Directory.CreateDirectory(RootDir);

            var metadata = new LibraryMetadata();
            var json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            var filePath = Path.Combine(RootDir, "metadata.json");
            File.WriteAllText(filePath, json);
            
            var assetDir = Path.Combine(RootDir, "Assets");
            Directory.CreateDirectory(assetDir);
        }
    }
}