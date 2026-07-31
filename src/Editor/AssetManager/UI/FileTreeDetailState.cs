using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.UI
{
    internal sealed class FileTreeDetailState
    {
        public FileTreeDetailState(
            string id,
            string name,
            string parentName = null,
            string extension = null,
            AssetArchiveContent archiveContent = null,
            string sourceArchivePath = null,
            string sourceArchiveEntryPath = null)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            ParentName = parentName ?? string.Empty;
            Extension =
                FileExtensionUtility.Normalize(
                    extension == null
                        ? Name
                        : extension);
            ArchiveContent = archiveContent;
            SourceArchivePath =
                sourceArchivePath ?? string.Empty;
            SourceArchiveEntryPath =
                sourceArchiveEntryPath ?? string.Empty;
        }

        public string Id { get; }

        public string Name { get; }

        public string ParentName { get; }

        public string Extension { get; }

        public AssetArchiveContent ArchiveContent { get; }

        public string SourceArchivePath { get; }

        public string SourceArchiveEntryPath { get; }

        public bool HasArchiveEntrySource =>
            !string.IsNullOrWhiteSpace(
                SourceArchivePath) &&
            !string.IsNullOrWhiteSpace(
                SourceArchiveEntryPath);

        public string AssetFileId
        {
            get
            {
                const string prefix = "asset-file|";
                return Id.StartsWith(
                    prefix,
                    System.StringComparison.Ordinal)
                    ? Id.Substring(prefix.Length)
                    : string.Empty;
            }
        }

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
