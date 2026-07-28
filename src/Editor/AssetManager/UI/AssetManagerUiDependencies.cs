using System;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerUiDependencies
    {
        private static IAssetManager _assetManager;
        private static IAssetManagerUiPreferences _preferences;
        private static IAssetArchiveReader _archiveReader;
        private static IAssetFileSystemReader _fileSystemReader;
        private static IAssetManagerUiScheduler _scheduler;
        private static StandaloneAssetManagerViewSession _standaloneViewSession;
        private static Action _requestManualSync;

        internal static IAssetManager AssetManager
        {
            get
            {
                if (_assetManager == null)
                {
                    throw new InvalidOperationException(
                        "AssetManager UI dependencies have not been configured.");
                }

                return _assetManager;
            }
        }

        internal static IAssetManagerUiPreferences Preferences =>
            _preferences ?? throw new InvalidOperationException(
                "AssetManager UI preferences have not been configured.");

        internal static IAssetArchiveReader ArchiveReader =>
            _archiveReader ?? throw new InvalidOperationException(
                "AssetManager archive reader has not been configured.");

        internal static IAssetFileSystemReader FileSystemReader =>
            _fileSystemReader ?? throw new InvalidOperationException(
                "AssetManager filesystem reader has not been configured.");

        internal static IAssetManagerUiScheduler Scheduler =>
            _scheduler ?? throw new InvalidOperationException(
                "AssetManager UI scheduler has not been configured.");

        internal static StandaloneAssetManagerViewSession StandaloneViewSession =>
            _standaloneViewSession ?? throw new InvalidOperationException(
                "AssetManager standalone view session has not been configured.");

        internal static void RequestManualSync()
        {
            if (_requestManualSync == null)
            {
                throw new InvalidOperationException(
                    "AssetManager manual sync has not been configured.");
            }

            _requestManualSync();
        }

        internal static void Configure(
            IAssetManager assetManager,
            IAssetManagerUiPreferences preferences,
            IAssetArchiveReader archiveReader,
            IAssetFileSystemReader fileSystemReader,
            IAssetManagerUiScheduler scheduler,
            StandaloneAssetManagerViewSession standaloneViewSession,
            Action requestManualSync)
        {
            _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            _archiveReader = archiveReader ?? throw new ArgumentNullException(nameof(archiveReader));
            _fileSystemReader = fileSystemReader ?? throw new ArgumentNullException(nameof(fileSystemReader));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _standaloneViewSession = standaloneViewSession ??
                                     throw new ArgumentNullException(
                                         nameof(standaloneViewSession));
            _requestManualSync = requestManualSync ??
                                 throw new ArgumentNullException(
                                     nameof(requestManualSync));
        }
    }
}
