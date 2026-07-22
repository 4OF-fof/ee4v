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

            public Action OnFinished { get; set; }
        }

        private static readonly Queue<PackageImportRequest> PackageQueue = new Queue<PackageImportRequest>();
        private static bool _isImportingPackage;

        public static string AssetsDirectory
        {
            get { return Application.dataPath; }
        }

        public static void ImportPackage(string packagePath, Action onFinished = null)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new ArgumentException("Package path is required.", nameof(packagePath));
            }

            PackageQueue.Enqueue(new PackageImportRequest
            {
                Path = packagePath,
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
            Action finish = () =>
            {
                AssetDatabase.importPackageCompleted -= completed;
                AssetDatabase.importPackageCancelled -= cancelled;
                AssetDatabase.importPackageFailed -= failed;
                _isImportingPackage = false;
                request.OnFinished?.Invoke();
                EditorApplication.delayCall += TryStartNextPackage;
            };

            completed = _ => finish();
            cancelled = _ => finish();
            failed = (_, __) => finish();
            AssetDatabase.importPackageCompleted += completed;
            AssetDatabase.importPackageCancelled += cancelled;
            AssetDatabase.importPackageFailed += failed;

            try
            {
                AssetDatabase.ImportPackage(request.Path, true);
            }
            catch
            {
                finish();
                throw;
            }
        }
    }
}
