namespace Ee4v.AssetManager.UI
{
    internal sealed class FileTreeDetailState
    {
        public FileTreeDetailState(
            string id,
            string name,
            string parentName = null,
            string extension = null)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            ParentName = parentName ?? string.Empty;
            Extension =
                FileExtensionUtility.Normalize(
                    extension == null
                        ? Name
                        : extension);
        }

        public string Id { get; }

        public string Name { get; }

        public string ParentName { get; }

        public string Extension { get; }

        public static FileTreeDetailState FromAssetFile(
            string fileId,
            string name,
            string extension = null)
        {
            return new FileTreeDetailState(
                "asset-file|" + (fileId ?? string.Empty),
                name,
                extension: extension);
        }
    }
}
