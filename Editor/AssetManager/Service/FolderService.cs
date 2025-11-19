using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Service {
    public class FolderService {
        private readonly IAssetRepository _repository;
        private readonly AssetService _assetService;

        // AssetServiceを依存注入して、アセット削除ロジックを再利用する
        public FolderService(IAssetRepository repository, AssetService assetService) {
            _repository = repository;
            _assetService = assetService;
        }

        public void CreateFolder(Ulid parentFolderId, string name, string description = null) {
            if (string.IsNullOrWhiteSpace(name)) {
                Debug.LogError("Folder name cannot be empty");
                return;
            }

            var libraries = _repository.GetLibraryMetadata();
            if (libraries == null) return;

            var folder = new Folder();
            folder.SetName(name);
            folder.SetDescription(description ?? string.Empty);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(folder);
            }
            else {
                var parent = libraries.GetFolder(parentFolderId);
                if (parent is not Folder parentFolder) {
                    Debug.LogError("Cannot create folder: Parent is not a standard folder.");
                    return;
                }
                parentFolder.AddChild(folder);
            }

            _repository.SaveLibraryMetadata(libraries);
        }

        public void MoveFolder(Ulid folderId, Ulid parentFolderId) {
            if (folderId == default) return;
            var libraries = _repository.GetLibraryMetadata();
            if (libraries == null) return;

            var folderBase = libraries.GetFolder(folderId);
            if (folderBase == null) return;

            // ルートへの移動
            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                if (folderBase is BoothItemFolder boothItem) {
                    libraries.RemoveFolder(folderId);
                    libraries.AddFolder(boothItem);
                }
                else if (folderBase is Folder f) {
                    libraries.RemoveFolder(folderId);
                    libraries.AddFolder(f);
                }
                _repository.SaveLibraryMetadata(libraries);
                return;
            }

            // 他のフォルダへの移動
            var newParentBase = libraries.GetFolder(parentFolderId);
            if (newParentBase is not Folder newParentFolder) {
                Debug.LogError("Cannot move: Target parent is not a valid folder.");
                return;
            }

            // 親子関係チェック（自分の子孫には移動できない）
            if (folderBase is Folder movingFolder && IsDescendant(movingFolder, parentFolderId)) {
                Debug.LogError("Cannot move a folder into its own descendant.");
                return;
            }

            libraries.RemoveFolder(folderId);
            newParentFolder.AddChild(folderBase);
            _repository.SaveLibraryMetadata(libraries);
        }

        public void UpdateFolder(Folder newFolder) {
            if (newFolder == null || !AssetValidationService.IsValidAssetName(newFolder.Name)) return;
            
            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(newFolder.ID) as Folder;
            if (folder == null) return;

            folder.SetName(newFolder.Name);
            folder.SetDescription(newFolder.Description);
            _repository.SaveLibraryMetadata(libraries);
        }
        
        public void UpdateBoothItemFolder(BoothItemFolder newFolder) {
            if (newFolder == null || !AssetValidationService.IsValidAssetName(newFolder.Name)) return;

            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(newFolder.ID) as BoothItemFolder;
            if (folder == null) return;

            folder.SetName(newFolder.Name);
            folder.SetDescription(newFolder.Description);
            folder.SetShopName(newFolder.ShopName);
            
            _repository.SaveLibraryMetadata(libraries);
        }

        public void SetFolderName(Ulid folderId, string newName) {
            if (!AssetValidationService.IsValidAssetName(newName)) return;
            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(folderId);
            
            if (folder != null) {
                folder.SetName(newName);
                _repository.SaveLibraryMetadata(libraries);
            }
        }

        public void SetFolderDescription(Ulid folderId, string description) {
            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(folderId);
            
            if (folder != null) {
                folder.SetDescription(description);
                _repository.SaveLibraryMetadata(libraries);
            }
        }

        public void DeleteFolder(Ulid folderId) {
            var libraries = _repository.GetLibraryMetadata();
            var targetFolder = libraries?.GetFolder(folderId);
            if (targetFolder == null) return;

            // フォルダ内（およびサブフォルダ内）の全アセットを論理削除
            var allDescendantIds = GetSelfAndDescendants(targetFolder);
            var allAssets = _repository.GetAllAssets(); // キャッシュから全取得

            foreach (var asset in allAssets) {
                if (allDescendantIds.Contains(asset.Folder)) {
                    _assetService.RemoveAsset(asset.ID);
                }
            }

            libraries.RemoveFolder(folderId);
            _repository.SaveLibraryMetadata(libraries);
        }

        private bool IsDescendant(Folder folder, Ulid targetId) {
            if (folder.ID == targetId) return true;
            foreach (var child in folder.Children) {
                if (child.ID == targetId) return true;
                if (child is Folder childFolder && IsDescendant(childFolder, targetId)) return true;
            }
            return false;
        }

        private HashSet<Ulid> GetSelfAndDescendants(BaseFolder root) {
            var set = new HashSet<Ulid> { root.ID };
            if (root is Folder f) {
                foreach (var child in f.Children) {
                    set.UnionWith(GetSelfAndDescendants(child));
                }
            }
            return set;
        }
    }
}