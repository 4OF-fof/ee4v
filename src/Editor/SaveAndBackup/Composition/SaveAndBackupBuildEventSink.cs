using System;
using Ee4v.Core.I18n;
using Ee4v.SaveAndBackup.Application;
using Ee4v.SaveAndBackup.Infrastructure.Unity;
using UnityEditor;
using UnityEngine;

namespace Ee4v.SaveAndBackup.Composition
{
    public static class SaveAndBackupBuildEventSink
    {
        public static void BuildStarted(GameObject target)
        {
            var coordinator =
                SaveAndBackupBootstrap.Coordinator;
            if (coordinator == null ||
                !UnitySaveTargetGateway.TryGetPrefab(
                    target,
                    out var guid,
                    out var hasUnappliedOverrides))
            {
                return;
            }

            var error = coordinator.Begin(
                guid,
                hasUnappliedOverrides);
            if (error == "unapplied-overrides")
            {
                EditorUtility.DisplayDialog(
                    I18N.Get(
                        "build.unappliedOverrides.title"),
                    I18N.Get(
                        "build.unappliedOverrides.message"),
                    I18N.Get("action.ok"));
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogError(
                    "[SaveAndBackup] " + error);
            }
        }

        public static void BuildSucceeded(string outputPath)
        {
            Report(SaveAndBackupBootstrap.Coordinator?
                .BuildSucceeded(outputPath));
        }

        public static void BuildFailed()
        {
            SaveAndBackupBootstrap.Coordinator?.Failed();
        }

        public static void UploadSucceeded(string externalId)
        {
            Report(SaveAndBackupBootstrap.Coordinator?
                .UploadSucceeded(externalId));
        }

        public static void UploadFailed()
        {
            SaveAndBackupBootstrap.Coordinator?.Failed();
        }

        private static void Report(
            BackupOperationResult result)
        {
            if (result == null ||
                result.Succeeded ||
                result.Skipped)
            {
                return;
            }

            Debug.LogError(
                "[SaveAndBackup] " + result.Error +
                (string.IsNullOrWhiteSpace(
                    result.SnapshotId)
                    ? string.Empty
                    : "\n" + I18N.Get(
                        "build.snapshotRetained",
                        result.SnapshotId)));
        }
    }
}
