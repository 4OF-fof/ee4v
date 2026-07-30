using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Internal.EditorAPI
{
    internal static class AssetImport
    {
        private sealed class PackageImportRequest
        {
            public string Path { get; set; }

            public bool Interactive { get; set; }

            public Action<bool> OnFinished { get; set; }
        }

        private static readonly Queue<PackageImportRequest> PackageQueue = new Queue<PackageImportRequest>();
        private static bool _isImportingPackage;

        public static string AssetsDirectory
        {
            get { return Application.dataPath; }
        }

        public static void ImportPackage(
            string packagePath,
            bool interactive,
            Action<bool> onFinished = null)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new ArgumentException("Package path is required.", nameof(packagePath));
            }

            PackageQueue.Enqueue(new PackageImportRequest
            {
                Path = packagePath,
                Interactive = interactive,
                OnFinished = onFinished
            });
            TryStartNextPackage();
        }

        public static void Refresh()
        {
            AssetDatabase.Refresh();
        }

        private static void TryStartNextPackage()
        {
            if (_isImportingPackage || PackageQueue.Count == 0)
            {
                return;
            }

            _isImportingPackage = true;
            var request = PackageQueue.Dequeue();
            AssetDatabase.ImportPackageCallback completed = null;
            AssetDatabase.ImportPackageCallback cancelled = null;
            AssetDatabase.ImportPackageFailedCallback failed = null;
            var didFinish = false;
            Action<bool> finish = succeeded =>
            {
                if (didFinish)
                {
                    return;
                }

                didFinish = true;
                AssetDatabase.importPackageCompleted -= completed;
                AssetDatabase.importPackageCancelled -= cancelled;
                AssetDatabase.importPackageFailed -= failed;
                _isImportingPackage = false;
                EditorApplication.delayCall += TryStartNextPackage;
                request.OnFinished?.Invoke(succeeded);
            };

            completed = _ => finish(true);
            cancelled = _ => finish(false);
            failed = (_, __) => finish(false);
            AssetDatabase.importPackageCompleted += completed;
            AssetDatabase.importPackageCancelled += cancelled;
            AssetDatabase.importPackageFailed += failed;

            try
            {
                AssetDatabase.ImportPackage(request.Path, request.Interactive);
            }
            catch
            {
                finish(false);
                throw;
            }
        }
    }
}
