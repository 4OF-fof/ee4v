using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.OldData;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Service {
    internal static class FolderService {
        public static void CreateFolder(Ulid parentFolderId, string name, string description = null) {
            if (string.IsNullOrWhiteSpace(name)) {
                Debug.LogError("Folder name cannot be empty");
                return;
            }

            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return;
            }

            var folder = new Folder();
            folder.SetName(name);
            folder.SetDescription(description ?? string.Empty);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(folder);
                AssetLibrarySerializer.SaveLibrary();
                return;
            }

            var parent = libraries.GetFolder(parentFolderId);
            if (parent is BoothItemFolder) {
                Debug.LogError("Cannot create folder under Booth Item Folder.");
                return;
            }

            if (parent is not Folder parentFolder) {
                Debug.LogError($"Parent folder {parentFolderId} not found.");
                return;
            }

            parentFolder.AddChild(folder);

            AssetLibrarySerializer.SaveLibrary();
        }

        public static void MoveFolder(Ulid folderId, Ulid parentFolderId) {
            if (folderId == default) return;
            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return;
            }

            var folderBase = libraries.GetFolder(folderId);
            switch (folderBase) {
                case null:
                    Debug.LogError($"Folder {folderId} not found.");
                    return;
                case BoothItemFolder boothItemFolder when parentFolderId == default || parentFolderId == Ulid.Empty:
                    libraries.RemoveFolder(folderId);
                    libraries.AddFolder(boothItemFolder);
                    AssetLibrarySerializer.SaveLibrary();
                    return;
                case BoothItemFolder boothItemFolder: {
                    var parentBaseForBooth = libraries.GetFolder(parentFolderId);
                    switch (parentBaseForBooth) {
                        case null:
                            Debug.LogError($"Parent folder {parentFolderId} not found.");
                            return;
                        case BoothItemFolder:
                            Debug.LogError("Cannot move under another BoothItemFolder.");
                            return;
                    }

                    if (parentBaseForBooth is not Folder parentFolderForBooth) {
                        Debug.LogError($"Parent folder {parentFolderId} not found.");
                        return;
                    }

                    if (parentFolderId == folderId) {
                        Debug.LogError("Cannot move folder into itself.");
                        return;
                    }

                    libraries.RemoveFolder(folderId);
                    parentFolderForBooth.AddChild(boothItemFolder);
                    AssetLibrarySerializer.SaveLibrary();
                    return;
                }
            }

            if (folderBase is not Folder folder) {
                Debug.LogError("Unknown folder type, cannot move.");
                return;
            }

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
            }
            else {
                var parentBase = libraries.GetFolder(parentFolderId);
                if (parentBase is BoothItemFolder) {
                    Debug.LogError("Cannot move folder under Booth Item Folder.");
                    return;
                }

                if (parentBase is not Folder) {
                    Debug.LogError($"Parent folder {parentFolderId} not found.");
                    return;
                }

                if (parentFolderId == folderId) {
                    Debug.LogError("Cannot move folder into itself.");
                    return;
                }

                var related = GetRelatedFolder(folder);
                if (related.Contains(parentFolderId)) {
                    Debug.LogError("Cannot move a folder into one of its own descendants.");
                    return;
                }
            }

            libraries.RemoveFolder(folderId);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(folder);
            }
            else {
                var parent = libraries.GetFolder(parentFolderId);
                if (parent is Folder parentFolder) parentFolder.AddChild(folder);
            }

            AssetLibrarySerializer.SaveLibrary();
        }

        public static void UpdateFolder(Folder newFolder) {
            if (newFolder == null) return;
            if (!AssetValidationService.IsValidAssetName(newFolder.Name)) return;

            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return;
            }

            var folder = libraries.GetFolder(newFolder.ID);
            switch (folder) {
                case null:
                    Debug.LogError($"Folder {newFolder.ID} not found.");
                    return;
                case BoothItemFolder:
                    Debug.LogError("Cannot update Booth Item Folder via this method.");
                    return;
            }

            if (folder.Name != newFolder.Name) folder.SetName(newFolder.Name);
            if (folder.Description != newFolder.Description) folder.SetDescription(newFolder.Description);
            AssetLibrarySerializer.SaveLibrary();
        }

        public static void UpdateBoothItemFolder(BoothItemFolder newFolder) {
            if (newFolder == null) return;
            if (!AssetValidationService.IsValidAssetName(newFolder.Name)) return;

            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return;
            }

            var folder = libraries.GetFolder(newFolder.ID);
            if (folder == null) {
                Debug.LogError($"Folder {newFolder.ID} not found.");
                return;
            }

            if (folder is not BoothItemFolder) {
                Debug.LogError("Target is not a Booth Item Folder.");
                return;
            }

            if (folder.Name != newFolder.Name) folder.SetName(newFolder.Name);
            if (folder.Description != newFolder.Description) folder.SetDescription(newFolder.Description);
            AssetLibrarySerializer.SaveLibrary();
        }

        public static void SetFolderName(Ulid folderId, string newName) {
            var existing = AssetLibrary.Instance.Libraries?.GetFolder(folderId);
            if (existing == null) return;
            if (!AssetValidationService.IsValidAssetName(newName)) return;
            switch (existing) {
                case Folder existingFolder: {
                    var folder = new Folder(existingFolder);
                    folder.SetName(newName);
                    UpdateFolder(folder);
                    break;
                }
                case BoothItemFolder existingBooth: {
                    var updated = new BoothItemFolder(existingBooth);
                    updated.SetName(newName);
                    UpdateBoothItemFolder(updated);
                    break;
                }
            }
        }

        public static void SetFolderDescription(Ulid folderId, string description) {
            var existing = AssetLibrary.Instance.Libraries?.GetFolder(folderId);
            switch (existing) {
                case null:
                    return;
                case Folder existingFolder: {
                    var folder = new Folder(existingFolder);
                    folder.SetDescription(description);
                    UpdateFolder(folder);
                    break;
                }
                case BoothItemFolder existingBooth: {
                    var updated = new BoothItemFolder(existingBooth);
                    updated.SetDescription(description);
                    UpdateBoothItemFolder(updated);
                    break;
                }
            }
        }

        public static void DeleteFolder(Ulid folderId) {
            if (folderId == default) return;
            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return;
            }

            var folder = libraries.GetFolder(folderId);
            if (folder == null) {
                Debug.LogError($"Folder {folderId} not found.");
                return;
            }

            List<Ulid> folderIds;
            if (folder is Folder folderWithChildren)
                folderIds = GetRelatedFolder(folderWithChildren);
            else
                folderIds = new List<Ulid> { folder.ID };
            foreach (var updatedAsset in from target in folderIds
                     select AssetLibrary.Instance.GetAssetsByFolder(target)
                     into assetsInFolder
                     where assetsInFolder != null && assetsInFolder.Count != 0
                     from asset in assetsInFolder
                     select new AssetMetadata(asset))
                AssetService.RemoveAsset(updatedAsset.ID);

            libraries.RemoveFolder(folderId);
            AssetLibrarySerializer.SaveLibrary();
        }

        private static List<Ulid> GetRelatedFolder(BaseFolder root) {
            var result = new List<Ulid>();
            if (root == null) return result;
            result.Add(root.ID);
            if (root is not Folder f || f.Children == null) return result;
            foreach (var child in f.Children) result.AddRange(GetRelatedFolder(child));
            return result;
        }
    }
}