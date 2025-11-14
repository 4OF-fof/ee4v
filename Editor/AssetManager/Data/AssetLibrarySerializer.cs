using System.IO;
using Newtonsoft.Json;
using _4OF.ee4v.Core.Data;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    public static class AssetLibrarySerializer {
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
        
        public static void LoadLibrary() {
            var filePath = Path.Combine(RootDir, "metadata.json");
            if (!File.Exists(filePath)) {
                Initialize();
            }

            var json = File.ReadAllText(filePath);
            var metadata = JsonConvert.DeserializeObject<LibraryMetadata>(json);
            AssetLibrary.Instance.LoadLibrary(metadata);
        }
        
        public static void SaveLibrary() {
            var metadata = AssetLibrary.Instance.Libraries;
            var json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            var filePath = Path.Combine(RootDir, "metadata.json");
            File.WriteAllText(filePath, json);
        }

        public static void AddAsset(string path) {
            var fileInfo = new FileInfo(path);
            var assetMetadata = new AssetMetadata();
            assetMetadata.UpdateName(fileInfo.Name);
            assetMetadata.UpdateSize(fileInfo.Length);
            assetMetadata.UpdateExt(fileInfo.Extension);
            SaveAsset(assetMetadata);

            var assetDir = Path.Combine(RootDir, "Assets", assetMetadata.ID.ToString());
            var destPath = Path.Combine(assetDir, fileInfo.Name);
            File.Copy(path, destPath, true);
            AssetLibrary.Instance.LoadAsset(assetMetadata);
        }
        
        public static void LoadAsset(Ulid assetId) {
            var filePath = Path.Combine(RootDir, "Assets", assetId.ToString() ,"metadata.json");
            if (!File.Exists(filePath)) return;

            var json = File.ReadAllText(filePath);
            var assetMetadata = JsonConvert.DeserializeObject<AssetMetadata>(json);
            AssetLibrary.Instance.LoadAsset(assetMetadata);
        }
        
        public static void LoadAllAssets() {
            var assetRootDir = Path.Combine(RootDir, "Assets");
            if (!Directory.Exists(assetRootDir)) return;

            var assetDirs = Directory.GetDirectories(assetRootDir);
            foreach (var assetDir in assetDirs) {
                var metadataPath = Path.Combine(assetDir, "metadata.json");
                if (!File.Exists(metadataPath)) continue;

                var json = File.ReadAllText(metadataPath);
                var assetMetadata = JsonConvert.DeserializeObject<AssetMetadata>(json);
                AssetLibrary.Instance.LoadAsset(assetMetadata);
            }
        }
        
        public static void SaveAsset(AssetMetadata assetMetadata) {
            var assetDir = Path.Combine(RootDir, "Assets", assetMetadata.ID.ToString());
            Directory.CreateDirectory(assetDir);

            var json = JsonConvert.SerializeObject(assetMetadata, Formatting.Indented);
            var filePath = Path.Combine(assetDir, "metadata.json");
            File.WriteAllText(filePath, json);
        }
    }
}