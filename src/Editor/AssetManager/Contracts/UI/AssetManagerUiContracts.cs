using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Ee4v.AssetManager.Contracts
{
    public sealed class AssetItemContextActionRequest
    {
        public AssetItemContextActionRequest(
            string itemId,
            float screenX,
            float screenY)
        {
            ItemId = itemId ?? string.Empty;
            ScreenX = screenX;
            ScreenY = screenY;
        }

        public string ItemId { get; }
        public float ScreenX { get; }
        public float ScreenY { get; }
    }

    public sealed class AssetItemContextAction
    {
        public AssetItemContextAction(
            string id,
            string label,
            Action execute,
            bool enabled = true)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Execute = execute;
            Enabled = enabled;
        }

        public string Id { get; }
        public string Label { get; }
        public Action Execute { get; }
        public bool Enabled { get; }
    }

    public interface IAssetItemContextActionProvider
    {
        bool TryCreate(
            AssetItemContextActionRequest request,
            out AssetItemContextAction action);
    }

    public interface IAssetItemContextActionRegistry
    {
        IDisposable Register(IAssetItemContextActionProvider provider);

        IReadOnlyList<AssetItemContextAction> CreateActions(
            AssetItemContextActionRequest request);
    }

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

    public sealed class AssetManagerFileSelection
    {
        public AssetManagerFileSelection(
            string path,
            string fileName,
            long? sizeBytes)
        {
            Path = path ?? string.Empty;
            FileName = fileName ?? string.Empty;
            SizeBytes = sizeBytes;
        }

        public string Path { get; }
        public string FileName { get; }
        public long? SizeBytes { get; }
    }

    public interface IAssetManagerFilePicker
    {
        AssetManagerFileSelection SelectFile(string title);
        AssetManagerFileSelection ReadFile(string path);
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

    public interface IAssetManagerProtectionActions :
        IAssetManagerAssetDerivationService
    {
        event Action Changed;

        string GetProtectionRootGuid(string assetGuid);
        void SetProtected(string assetGuid, bool isProtected);
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
