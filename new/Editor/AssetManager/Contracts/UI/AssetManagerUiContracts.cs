using System;
using System.Collections.Generic;
using System.Threading;

namespace Ee4v.AssetManager.Contracts
{
    public enum AssetManagerUiPreference
    {
        ItemsPerRow,
        HistoryOverlayMaximumItems,
        ShowFileTreeImageTooltip
    }

    public interface IAssetManagerUiPreferences
    {
        event Action<AssetManagerUiPreference> Changed;

        int ItemsPerRow { get; set; }
        int MinimumItemsPerRow { get; }
        int MaximumItemsPerRow { get; }
        int HistoryOverlayMaximumItems { get; }
        bool ShowFileTreeImageTooltip { get; }

        void Preload();
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

    public interface IAssetArchiveReader
    {
        string CacheDirectory { get; }

        IReadOnlyList<AssetArchiveEntry> ReadZipEntries(
            string zipPath,
            CancellationToken cancellationToken);
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
        void RunInBackground<T>(
            Func<CancellationToken, T> operation,
            CancellationToken cancellationToken,
            Action<AssetManagerBackgroundResult<T>> completed);
    }
}
