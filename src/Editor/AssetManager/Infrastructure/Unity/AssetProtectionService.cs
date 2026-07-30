using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Unity
{
    internal sealed class AssetProtectionService :
        IAssetManagerProtectionActions,
        IDisposable
    {
        private const string ManagedPathsRelativePath =
            "Library/ee4v/asset-protection-paths.json";

        private readonly HashSet<string> _suspendedFileIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<AssetProtectionPathScope>
            _scopes =
                new List<AssetProtectionPathScope>();
        private readonly HashSet<string> _managedReadOnlyPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IAssetManager _assetManager;
        private string _projectRoot;
        private string _managedPathsFile;
        private bool _disposed;

        public event Action Changed;

        internal void Initialize(IAssetManager assetManager)
        {
            if (_assetManager != null)
            {
                return;
            }

            _assetManager = assetManager ??
                throw new ArgumentNullException(nameof(assetManager));
            _projectRoot = Path.GetFullPath(
                    Path.Combine(
                        UnityEngine.Application.dataPath,
                        ".."))
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            _managedPathsFile = Path.Combine(
                _projectRoot,
                ManagedPathsRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            LoadManagedPaths();
            _assetManager.Changed += OnAssetManagerChanged;
            ReloadAndReconcile();
        }

        internal void BeginImport(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId) ||
                !_suspendedFileIds.Add(fileId))
            {
                return;
            }

            ReconcileFileAttributes();
            Changed?.Invoke();
        }

        internal void EndImport(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId) ||
                !_suspendedFileIds.Remove(fileId))
            {
                return;
            }

            ReloadAndReconcile();
        }

        internal bool IsPathProtected(string assetOrMetaPath)
        {
            var path = NormalizeAssetPath(assetOrMetaPath);
            return AssetProtectionPathPolicy.IsProtected(
                _scopes,
                _suspendedFileIds,
                path);
        }

        internal bool WouldMutateProtectedAsset(
            string assetOrMetaPath)
        {
            return AssetProtectionPathPolicy
                .WouldMutateProtectedAsset(
                    _scopes,
                    _suspendedFileIds,
                    NormalizeAssetPath(
                        assetOrMetaPath));
        }

        public bool IsManaged(string assetGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            return FindControllingScope(path) != null;
        }

        public bool IsProtected(string assetGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            return IsPathProtected(path);
        }

        public string GetProtectionRootGuid(string assetGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            var scope = FindControllingScope(path);
            return scope?.AssetGuid ?? string.Empty;
        }

        public bool CanCreateMaterialVariant(string assetGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            return !string.IsNullOrWhiteSpace(path) &&
                   AssetDatabase.LoadAssetAtPath<Material>(path) !=
                   null;
        }

        public bool CanCreatePrefabVariant(string assetGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            var prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null &&
                   PrefabUtility.GetPrefabAssetType(prefab) !=
                   PrefabAssetType.NotAPrefab;
        }

        public void SetProtected(
            string assetGuid,
            bool isProtected)
        {
            if (_assetManager == null)
            {
                return;
            }

            var rootGuid = GetProtectionRootGuid(assetGuid);
            if (string.IsNullOrWhiteSpace(rootGuid))
            {
                rootGuid = assetGuid;
            }

            _assetManager.SetImportedAssetProtection(
                rootGuid,
                isProtected);
        }

        public bool CreateEditableCopy(
            string assetGuid,
            string destinationAssetPath)
        {
            var sourcePath =
                AssetDatabase.GUIDToAssetPath(assetGuid);
            var destination =
                NormalizeAssetPath(destinationAssetPath);
            if (string.IsNullOrWhiteSpace(sourcePath) ||
                string.IsNullOrWhiteSpace(destination) ||
                IsPathProtected(destination) ||
                WouldMutateProtectedAsset(destination))
            {
                return false;
            }

            return AssetDatabase.CopyAsset(
                sourcePath,
                destination);
        }

        public bool CreateMaterialVariant(
            string assetGuid,
            string destinationAssetPath)
        {
            var sourcePath =
                AssetDatabase.GUIDToAssetPath(assetGuid);
            var destination =
                NormalizeAssetPath(destinationAssetPath);
            var parent =
                AssetDatabase.LoadAssetAtPath<Material>(
                    sourcePath);
            if (parent == null ||
                string.IsNullOrWhiteSpace(destination) ||
                IsPathProtected(destination) ||
                WouldMutateProtectedAsset(destination))
            {
                return false;
            }

            var variant = new Material(parent)
            {
                name = Path.GetFileNameWithoutExtension(
                    destination),
                parent = parent
            };
            try
            {
                AssetDatabase.CreateAsset(
                    variant,
                    destination);
                return true;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(variant);
                throw;
            }
        }

        public bool CreatePrefabVariant(
            string assetGuid,
            string destinationAssetPath)
        {
            var sourcePath =
                AssetDatabase.GUIDToAssetPath(assetGuid);
            var destination =
                NormalizeAssetPath(destinationAssetPath);
            var source =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    sourcePath);
            if (source == null ||
                PrefabUtility.GetPrefabAssetType(source) ==
                PrefabAssetType.NotAPrefab ||
                string.IsNullOrWhiteSpace(destination) ||
                IsPathProtected(destination) ||
                WouldMutateProtectedAsset(destination))
            {
                return false;
            }

            var previewScene =
                EditorSceneManager.NewPreviewScene();
            try
            {
                var instance =
                    PrefabUtility.InstantiatePrefab(
                        source,
                        previewScene) as GameObject;
                if (instance == null)
                {
                    return false;
                }

                var created =
                    PrefabUtility.SaveAsPrefabAsset(
                        instance,
                        destination,
                        out var succeeded);
                return succeeded && created != null;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(
                    previewScene);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_assetManager != null)
            {
                _assetManager.Changed -= OnAssetManagerChanged;
            }
        }

        private void OnAssetManagerChanged(
            AssetManagerChange change)
        {
            if (change == null ||
                change.Kind !=
                    AssetManagerChangeKind.ImportedAssetGuids &&
                change.Kind !=
                    AssetManagerChangeKind
                        .ImportedAssetProtection &&
                change.Kind != AssetManagerChangeKind.Catalog)
            {
                return;
            }

            ReloadAndReconcile();
        }

        private void ReloadAndReconcile()
        {
            if (_assetManager == null || _disposed)
            {
                return;
            }

            try
            {
                var associations =
                    _assetManager
                        .GetImportedAssetAssociations() ??
                    Array.Empty<
                        AssetImportedAssetAssociation>();
                _scopes.Clear();
                for (var i = 0;
                     i < associations.Count;
                     i++)
                {
                    var association = associations[i];
                    if (association == null ||
                        string.IsNullOrWhiteSpace(
                            association.AssetGuid) ||
                        string.IsNullOrWhiteSpace(
                            association.FileId))
                    {
                        continue;
                    }

                    var assetPath =
                        NormalizeAssetPath(
                            AssetDatabase.GUIDToAssetPath(
                                association.AssetGuid));
                    if (string.IsNullOrWhiteSpace(assetPath))
                    {
                        continue;
                    }

                    _scopes.Add(
                        new AssetProtectionPathScope(
                        association.AssetGuid,
                        association.FileId,
                        assetPath,
                        association.IsProtected));
                }

                ReconcileFileAttributes();
                Changed?.Invoke();
            }
            catch (Exception exception)
            {
                ClearManagedReadOnlyPaths();
                Debug.LogException(exception);
            }
        }

        private AssetProtectionPathScope FindControllingScope(
            string assetOrMetaPath)
        {
            return AssetProtectionPathPolicy.FindControllingScope(
                _scopes,
                NormalizeAssetPath(assetOrMetaPath));
        }

        private void ReconcileFileAttributes()
        {
            if (string.IsNullOrWhiteSpace(_projectRoot))
            {
                return;
            }

            var desired =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < _scopes.Count; i++)
            {
                var scope = _scopes[i];
                if (!scope.IsProtected ||
                    _suspendedFileIds.Contains(
                        scope.FileId) ||
                    !IsPathProtected(scope.AssetPath))
                {
                    continue;
                }

                AddScopeFiles(scope.AssetPath, desired);
            }

            foreach (var relativePath in
                     _managedReadOnlyPaths.ToArray())
            {
                if (desired.Contains(relativePath))
                {
                    SetReadOnly(relativePath, true);
                    continue;
                }

                SetReadOnly(relativePath, false);
                _managedReadOnlyPaths.Remove(relativePath);
            }

            foreach (var relativePath in desired)
            {
                var fullPath = ToFullPath(relativePath);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                var attributes =
                    File.GetAttributes(fullPath);
                if ((attributes &
                     FileAttributes.ReadOnly) == 0)
                {
                    File.SetAttributes(
                        fullPath,
                        attributes |
                        FileAttributes.ReadOnly);
                    _managedReadOnlyPaths.Add(
                        relativePath);
                }
                else if (_managedReadOnlyPaths.Contains(
                             relativePath))
                {
                    SetReadOnly(relativePath, true);
                }
            }

            SaveManagedPaths();
        }

        private void AddScopeFiles(
            string assetPath,
            ISet<string> destination)
        {
            var fullPath = ToFullPath(assetPath);
            if (Directory.Exists(fullPath))
            {
                AddDesiredPath(assetPath + ".meta", destination);
                string[] files;
                try
                {
                    files = Directory.GetFiles(
                        fullPath,
                        "*",
                        SearchOption.AllDirectories);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return;
                }

                for (var i = 0; i < files.Length; i++)
                {
                    var relative = ToProjectRelative(files[i]);
                    AddDesiredPath(relative, destination);
                }

                return;
            }

            AddDesiredPath(assetPath, destination);
            AddDesiredPath(assetPath + ".meta", destination);
        }

        private void AddDesiredPath(
            string relativePath,
            ISet<string> destination)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                !File.Exists(ToFullPath(relativePath)) ||
                !IsPathProtected(relativePath))
            {
                return;
            }

            destination.Add(
                NormalizeProjectRelativePath(relativePath));
        }

        private void ClearManagedReadOnlyPaths()
        {
            foreach (var path in
                     _managedReadOnlyPaths.ToArray())
            {
                SetReadOnly(path, false);
            }

            _managedReadOnlyPaths.Clear();
            SaveManagedPaths();
        }

        private void SetReadOnly(
            string relativePath,
            bool readOnly)
        {
            try
            {
                var fullPath = ToFullPath(relativePath);
                if (!File.Exists(fullPath))
                {
                    return;
                }

                var attributes =
                    File.GetAttributes(fullPath);
                attributes = readOnly
                    ? attributes | FileAttributes.ReadOnly
                    : attributes & ~FileAttributes.ReadOnly;
                File.SetAttributes(fullPath, attributes);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void LoadManagedPaths()
        {
            _managedReadOnlyPaths.Clear();
            if (!File.Exists(_managedPathsFile))
            {
                return;
            }

            try
            {
                var state =
                    JsonUtility.FromJson<ManagedPathState>(
                        File.ReadAllText(
                            _managedPathsFile));
                var paths = state?.paths ??
                            new List<string>();
                for (var i = 0; i < paths.Count; i++)
                {
                    var path =
                        NormalizeProjectRelativePath(
                            paths[i]);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        _managedReadOnlyPaths.Add(path);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void SaveManagedPaths()
        {
            if (string.IsNullOrWhiteSpace(_managedPathsFile))
            {
                return;
            }

            try
            {
                var directory =
                    Path.GetDirectoryName(
                        _managedPathsFile);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(
                    _managedPathsFile,
                    JsonUtility.ToJson(
                        new ManagedPathState
                        {
                            paths =
                                _managedReadOnlyPaths
                                    .OrderBy(
                                        path => path,
                                        StringComparer
                                            .OrdinalIgnoreCase)
                                    .ToList()
                        },
                        true));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private string ToFullPath(string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(
                _projectRoot,
                NormalizeProjectRelativePath(
                        projectRelativePath)
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private string ToProjectRelative(string fullPath)
        {
            var path = Path.GetFullPath(fullPath);
            var prefix = _projectRoot +
                         Path.DirectorySeparatorChar;
            if (!path.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return NormalizeProjectRelativePath(
                path.Substring(prefix.Length));
        }

        private static string NormalizeAssetPath(
            string path)
        {
            var normalized =
                NormalizeProjectRelativePath(path);
            if (normalized.EndsWith(
                    ".meta",
                    StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(
                    0,
                    normalized.Length - ".meta".Length);
            }

            return normalized;
        }

        private static string NormalizeProjectRelativePath(
            string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        [Serializable]
        private sealed class ManagedPathState
        {
            public List<string> paths =
                new List<string>();
        }
    }

    internal sealed class AssetProtectionPathScope
    {
        internal AssetProtectionPathScope(
            string assetGuid,
            string fileId,
            string assetPath,
            bool isProtected)
        {
            AssetGuid = assetGuid ?? string.Empty;
            FileId = fileId ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            IsProtected = isProtected;
        }

        internal string AssetGuid { get; }
        internal string FileId { get; }
        internal string AssetPath { get; }
        internal bool IsProtected { get; }
    }

    internal static class AssetProtectionPathPolicy
    {
        internal static AssetProtectionPathScope
            FindControllingScope(
                IReadOnlyList<AssetProtectionPathScope> scopes,
                string assetPath)
        {
            AssetProtectionPathScope selected = null;
            if (scopes == null ||
                string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            for (var i = 0; i < scopes.Count; i++)
            {
                var scope = scopes[i];
                if (scope == null ||
                    !IsSameOrDescendant(
                        assetPath,
                        scope.AssetPath))
                {
                    continue;
                }

                if (selected == null ||
                    scope.AssetPath.Length >
                    selected.AssetPath.Length ||
                    scope.AssetPath.Length ==
                    selected.AssetPath.Length &&
                    !scope.IsProtected)
                {
                    selected = scope;
                }
            }

            return selected;
        }

        internal static bool IsProtected(
            IReadOnlyList<AssetProtectionPathScope> scopes,
            ISet<string> suspendedFileIds,
            string assetPath)
        {
            var scope = FindControllingScope(
                scopes,
                assetPath);
            return scope != null &&
                   scope.IsProtected &&
                   (suspendedFileIds == null ||
                    !suspendedFileIds.Contains(
                        scope.FileId));
        }

        internal static bool WouldMutateProtectedAsset(
            IReadOnlyList<AssetProtectionPathScope> scopes,
            ISet<string> suspendedFileIds,
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                scopes == null)
            {
                return false;
            }

            if (IsProtected(
                    scopes,
                    suspendedFileIds,
                    assetPath))
            {
                return true;
            }

            for (var i = 0; i < scopes.Count; i++)
            {
                var scope = scopes[i];
                if (scope == null ||
                    suspendedFileIds != null &&
                    suspendedFileIds.Contains(
                        scope.FileId) ||
                    !scope.IsProtected ||
                    !IsProtected(
                        scopes,
                        suspendedFileIds,
                        scope.AssetPath))
                {
                    continue;
                }

                if (IsSameOrDescendant(
                        scope.AssetPath,
                        assetPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameOrDescendant(
            string path,
            string root)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   !string.IsNullOrWhiteSpace(root) &&
                   (string.Equals(
                        path,
                        root,
                        StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(
                        root + "/",
                        StringComparison.OrdinalIgnoreCase));
        }
    }
}
