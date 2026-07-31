using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Ee4v.AssetManager.Contracts
{
    public enum AssetManagerUiPreference
    {
        HistoryOverlayMaximumItems,
        ShowFileTreeImageTooltip
    }

    public interface IAssetManagerUiPreferences
    {
        event Action<AssetManagerUiPreference> Changed;

        int DefaultItemsPerRow { get; }
        int MinimumItemsPerRow { get; }
        int MaximumItemsPerRow { get; }
        int HistoryOverlayMaximumItems { get; }
        bool ShowFileTreeImageTooltip { get; }

        void Preload();
    }

    public interface IAssetManagerProjectActions
    {
        bool CanHighlightItem(string itemId);
        bool CanHighlightFile(string fileId);
        bool IsItemHighlighted(string itemId);
        bool IsFileHighlighted(string fileId);
        bool IsGuidHighlighted(string guid);
        void HighlightItem(string itemId);
        void HighlightFile(string fileId);
        void ClearHighlights();
    }

    public interface IAssetManagerProtectionActions
    {
        event Action Changed;

        bool IsManaged(string assetGuid);
        bool IsProtected(string assetGuid);
        string GetProtectionRootGuid(string assetGuid);
        bool CanCreateMaterialVariant(string assetGuid);
        bool CanCreatePrefabVariant(string assetGuid);
        void SetProtected(string assetGuid, bool isProtected);
        bool CreateEditableCopy(
            string assetGuid,
            string destinationAssetPath);
        bool CreateMaterialVariant(
            string assetGuid,
            string destinationAssetPath);
        bool CreatePrefabVariant(
            string assetGuid,
            string destinationAssetPath);
    }

    public sealed class AssetArchiveEntry
    {
        public AssetArchiveEntry(string fullName, long length, string archiveFullName = null)
        {
            FullName = fullName ?? string.Empty;
            Length = length;
            ArchiveFullName = string.IsNullOrWhiteSpace(archiveFullName)
                ? FullName
                : archiveFullName;
        }

        public string FullName { get; }
        public long Length { get; }
        public string ArchiveFullName { get; }
    }

    public enum AssetArchiveContentKind
    {
        Zip,
        UnityPackage
    }

    public enum AssetArchiveContentEntryKind
    {
        File,
        Directory
    }

    public sealed class AssetArchiveContentEntry
    {
        public AssetArchiveContentEntry(
            string path,
            AssetArchiveContentEntryKind kind,
            long sizeBytes = 0L,
            string sourcePath = null)
        {
            Path = path ?? string.Empty;
            Kind = kind;
            SizeBytes = Math.Max(0L, sizeBytes);
            SourcePath = sourcePath ?? string.Empty;
        }

        public string Path { get; }
        public AssetArchiveContentEntryKind Kind { get; }
        public long SizeBytes { get; }
        public string SourcePath { get; }
    }

    public sealed class AssetArchiveContent
    {
        public AssetArchiveContent(
            AssetArchiveContentKind kind,
            long sizeBytes,
            IReadOnlyList<AssetArchiveContentEntry> entries)
        {
            Kind = kind;
            SizeBytes = Math.Max(0L, sizeBytes);
            Entries = entries ?? Array.Empty<AssetArchiveContentEntry>();
        }

        public AssetArchiveContentKind Kind { get; }
        public long SizeBytes { get; }
        public IReadOnlyList<AssetArchiveContentEntry> Entries { get; }
    }

    public interface IAssetArchiveReader
    {
        string CacheDirectory { get; }

        IReadOnlyList<AssetArchiveEntry> ReadZipEntries(
            string zipPath,
            CancellationToken cancellationToken);

        AssetArchiveContent ReadZipContent(
            string zipPath,
            CancellationToken cancellationToken);

        AssetArchiveContent ReadUnityPackageContent(
            string packagePath,
            CancellationToken cancellationToken);

        AssetArchiveContent
            ReadUnityPackageContentFromZip(
                string zipPath,
                string entryPath,
                CancellationToken cancellationToken);

        byte[] ReadEntryBytes(
            AssetArchiveContentKind kind,
            string archivePath,
            string packageEntryPath,
            string contentEntryPath,
            long maximumBytes,
            CancellationToken cancellationToken);
    }

    public enum AssetFileSystemEntryKind
    {
        File,
        Directory
    }

    public sealed class AssetFileSystemEntry
    {
        public AssetFileSystemEntry(
            string fullPath,
            string name,
            AssetFileSystemEntryKind kind)
        {
            FullPath = fullPath ?? string.Empty;
            Name = name ?? string.Empty;
            Kind = kind;
        }

        public string FullPath { get; }
        public string Name { get; }
        public AssetFileSystemEntryKind Kind { get; }
    }

    public interface IAssetFileSystemReader
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);

        IReadOnlyList<AssetFileSystemEntry> GetDirectoryEntries(
            string path,
            CancellationToken cancellationToken);

        Stream OpenFile(string path, long maximumBytes);

        Stream OpenZipEntry(
            string archivePath,
            string entryPath,
            long maximumBytes);
    }

    public sealed class AssetManagerBackgroundResult<T>
    {
        public AssetManagerBackgroundResult(T value, Exception error, bool canceled)
        {
            Value = value;
            Error = error;
            Canceled = canceled;
        }

        public T Value { get; }
        public Exception Error { get; }
        public bool Canceled { get; }
        public bool Succeeded => !Canceled && Error == null;
    }

    public interface IAssetManagerUiScheduler
    {
        void RunOnMainThread(Action operation);

        void RunInBackground<T>(
            Func<CancellationToken, T> operation,
            CancellationToken cancellationToken,
            Action<AssetManagerBackgroundResult<T>> completed);
    }
}
