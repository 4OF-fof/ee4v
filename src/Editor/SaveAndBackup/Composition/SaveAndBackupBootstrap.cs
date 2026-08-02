using System.IO;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using Ee4v.SaveAndBackup.Application;
using Ee4v.SaveAndBackup.Infrastructure;
using Ee4v.SaveAndBackup.Infrastructure.Git;
using Ee4v.SaveAndBackup.Infrastructure.Persistence;
using UnityEditor;

namespace Ee4v.SaveAndBackup.Composition
{
    public static class SaveAndBackupBootstrap
    {
        private static bool _initialized;
        private static SaveAndBackupService _service;
        private static SaveAndBackupCoordinator _coordinator;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            var settings = CoreSettings.Current;
            FeatureBootstrapContract.Initialize(
                "SaveAndBackup",
                typeof(SaveAndBackupDefinitions),
                () => SaveAndBackupDefinitions.RegisterAll(
                    settings),
                () => InitializeModule(settings));
            _initialized = true;
        }

        private static void InitializeModule(
            ISettingsService settings)
        {
            var projectRoot = Path.GetFullPath(
                Path.Combine(
                    UnityEngine.Application.dataPath,
                    ".."));
            var backup = new GitSaveAndBackupGateway(
                projectRoot,
                () => settings.Get(
                    SaveAndBackupDefinitions.BackupRoot),
                PlayerSettings.productGUID.ToString());
            _service = new SaveAndBackupService(
                new SaveAndBackupRecordStore(projectRoot),
                backup,
                new SystemSaveAndBackupEnvironment());
            _coordinator = new SaveAndBackupCoordinator(
                _service,
                backup);
        }

        public static SaveAndBackupService Service => _service;

        internal static SaveAndBackupCoordinator Coordinator =>
            _coordinator;
    }
}
