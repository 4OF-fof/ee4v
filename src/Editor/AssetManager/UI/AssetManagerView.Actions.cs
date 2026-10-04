using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetManagerView
    {
        private void SetItemsArchived(
            IReadOnlyList<string> itemIds,
            bool archived)
        {
            Run(() =>
                _manager.SetItemArchived(itemIds, archived));
        }

        private void DeleteItems(IReadOnlyList<string> itemIds)
        {
            var multiple = itemIds != null && itemIds.Count > 1;
            if (!Confirm(
                    I18N.Get(multiple
                        ? "confirm.deleteItems.title"
                        : "confirm.deleteItem.title"),
                    I18N.Get(
                        multiple
                            ? "confirm.deleteItems.message"
                            : "confirm.deleteItem.message",
                        itemIds?.Count ?? 0)))
            {
                return;
            }

            Run(() =>
            {
                _manager.DeleteItem(itemIds);
                _viewState.SelectItem(null);
            });
        }

        private void RegisterFile(
            string itemId,
            string filePath,
            string fileName)
        {
            Run(() => _manager.RegisterFile(
                itemId,
                new RegisterFileRequest
                {
                    LibraryPath = AssetManagerSettings.Ee4vLibraryPath,
                    FilePath = filePath,
                    FileName = fileName
                }));
        }

        private void RegisterCurrentItemFile()
        {
            var itemId = _viewState.DetailItemId;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            var filePath = EditorUtility.OpenFilePanel(
                I18N.Get("detail.file.register"),
                string.Empty,
                string.Empty);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            RegisterFile(
                itemId,
                filePath,
                Path.GetFileName(filePath));
        }

        private void MoveFiles(
            IReadOnlyList<string> fileIds,
            string itemId)
        {
            Run(() =>
                _manager.SetFileItem(
                    fileIds,
                    string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim()));
        }

        private void ArchiveFile(string id, bool archived)
        {
            ArchiveFiles(new[] { id }, archived);
        }

        private void ArchiveFiles(
            IReadOnlyList<string> fileIds,
            bool archived)
        {
            Run(() =>
                _manager.SetFileArchived(fileIds, archived));
        }

        private void DeleteFile(string id)
        {
            DeleteFiles(new[] { id });
        }

        private void DeleteFiles(IReadOnlyList<string> fileIds)
        {
            if (!Confirm(
                    I18N.Get(fileIds.Count == 1
                        ? "confirm.deleteFile.title"
                        : "confirm.deleteFiles.title"),
                    fileIds.Count == 1
                        ? I18N.Get("confirm.deleteFile.message")
                        : string.Format(
                            I18N.Get("confirm.deleteFiles.message"),
                            fileIds.Count)))
            {
                return;
            }

            Run(() =>
            {
                _manager.DeleteFile(fileIds);
                _fileTreeSelections = Array.Empty<FileTreeSelection>();
                _viewState.SelectFile(null);
            });
        }

        private void SaveDependencies(
            IReadOnlyList<AssetFileTarget> sources,
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            Run(() =>
                _manager.SetFileDependencies(
                    sources,
                    dependencyTargets));
        }

        private void ImportEntries(string fileId, string paths)
        {
            _ = RunImport(
                () => _manager.ImportFileEntries(fileId, SplitLines(paths)));
        }

        private void ImportFileTreeEntry(FileTreeSelection selection)
        {
            if (!SearchableFileTree.CanImport(selection))
            {
                return;
            }

            _ = ImportFileTreeEntriesAsync(new[] { selection });
        }

        private async Task ImportFileTreeEntriesAsync(
            IReadOnlyList<FileTreeSelection> selections)
        {
            var groups = selections
                .Where(SearchableFileTree.CanImport)
                .GroupBy(selection => selection.File.Id, StringComparer.Ordinal)
                .ToArray();
            foreach (var group in groups)
            {
                var paths = group
                    .Select(selection => selection.Entry?.Path ?? string.Empty)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (paths.Contains(string.Empty))
                {
                    paths = new[] { string.Empty };
                }
                try
                {
                    var result = await _manager.ImportFileEntries(
                        group.Key,
                        paths);
                    if (!result.Succeeded)
                    {
                        Debug.LogError(
                            "Asset import failed: " + result.State + " · " +
                            result.ErrorMessage);
                        return;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return;
                }
            }
        }

        private void ShowItemTargetEditor(
            VisualElement anchor,
            string itemId,
            IReadOnlyList<AssetFile> files)
        {
            TargetEditorPopup.Show(
                anchor,
                _manager,
                itemId,
                files,
                _manager.GetItemTargets(itemId),
                I18N.Get("detail.itemTargetPickerTitle"),
                I18N.Get("action.saveTargets"),
                targets => Run(() =>
                    _manager.SetItemTargets(itemId, targets)),
                _fileTree?.GetCachedAnalyses());
        }

        private IReadOnlyList<AssetFileTarget> GetCommonFileDependencies(
            IReadOnlyList<AssetFileTarget> sources)
        {
            var common = _manager.GetFileDependencies(sources[0].FileId)
                .Where(dependency => string.Equals(
                    dependency.DependentTargetPath,
                    sources[0].TargetPath,
                    StringComparison.OrdinalIgnoreCase))
                .Select(dependency => new AssetFileTarget
                {
                    FileId = dependency.DependencyFileId,
                    TargetPath = dependency.TargetPath
                })
                .ToArray();
            for (var index = 1; index < sources.Count; index++)
            {
                var source = sources[index];
                var dependencies = _manager.GetFileDependencies(source.FileId)
                    .Where(dependency => string.Equals(
                        dependency.DependentTargetPath,
                        source.TargetPath,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                common = common.Where(target => dependencies.Any(dependency =>
                    string.Equals(
                        dependency.DependencyFileId,
                        target.FileId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        dependency.TargetPath,
                        target.TargetPath,
                        StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
            }
            return common;
        }

        private void ShowDependencyEditor(
            VisualElement anchor,
            IReadOnlyList<AssetFileTarget> sources,
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            var sourceFile = _manager.GetFile(sources[0].FileId);
            var sourceItemId = sourceFile?.ItemId ?? string.Empty;
            var excludedFileIds = new HashSet<string>(
                sources.Where(source => string.IsNullOrEmpty(
                        source.TargetPath))
                    .Select(source => source.FileId),
                StringComparer.Ordinal);
            var groupedFiles = (_manager.SearchItems(new AssetItemQuery
                {
                    IncludeArchived = true
                }).Items ?? Array.Empty<AssetItem>())
                .Where(item => item != null)
                .Select(item => new
                {
                    Item = item,
                    Files = (item.Files ?? Array.Empty<AssetFile>())
                        .Where(file => file != null &&
                                       !excludedFileIds.Contains(file.Id))
                        .OrderBy(
                            file => file.FileName,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(file => file.Id, StringComparer.Ordinal)
                        .ToArray()
                })
                .Where(group => group.Files.Length > 0)
                .OrderBy(group => !string.Equals(
                    group.Item.Id,
                    sourceItemId,
                    StringComparison.Ordinal))
                .ThenBy(
                    group => group.Item.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Item.Id, StringComparer.Ordinal)
                .ToArray();
            var hasSourceGroup = groupedFiles.Any(group => string.Equals(
                group.Item.Id,
                sourceItemId,
                StringComparison.Ordinal));
            var startedOtherItems = false;
            var groups = new List<FileTreeGroup>(groupedFiles.Length);
            foreach (var group in groupedFiles)
            {
                var isSourceGroup = string.Equals(
                    group.Item.Id,
                    sourceItemId,
                    StringComparison.Ordinal);
                var startsNewSection = hasSourceGroup &&
                                       !isSourceGroup &&
                                       !startedOtherItems;
                startedOtherItems |= !isSourceGroup;
                groups.Add(new FileTreeGroup(
                    group.Item.Id,
                    string.IsNullOrWhiteSpace(group.Item.Name)
                        ? group.Item.Id
                        : group.Item.Name,
                    group.Files,
                    startsNewSection));
            }
            var files = groups
                .SelectMany(group => group.Files)
                .ToArray();
            TargetEditorPopup.Show(
                anchor,
                _manager,
                null,
                files,
                dependencyTargets,
                I18N.Get("detail.dependencyTargetPickerTitle"),
                I18N.Get("action.saveDependencies"),
                targets => SaveDependencies(sources, targets),
                _fileTree?.GetCachedAnalyses(),
                groups,
                sources);
        }

        private void ShowItemTargetImport(
            VisualElement anchor,
            string itemId,
            IReadOnlyList<AssetFile> files)
        {
            var filesById = (files ?? Array.Empty<AssetFile>())
                .ToDictionary(file => file.Id, StringComparer.Ordinal);
            var groups = _manager.GetItemTargets(itemId)
                .Where(target =>
                    filesById.ContainsKey(target.FileId) &&
                    !string.IsNullOrWhiteSpace(target.GroupName))
                .Select(target => new ItemTargetEntry
                {
                    File = filesById[target.FileId],
                    Target = target
                })
                .GroupBy(
                    entry => entry.Target.GroupName,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new TargetImportGroup
                {
                    Name = group.Key,
                    Choices = group.Select(entry => new TargetImportChoice
                    {
                        Label = FormatTargetName(entry.File, entry.Target),
                        Target = entry.Target
                    }).ToArray()
                })
                .ToArray();
            if (groups.Length == 0)
            {
                ImportItemTargets(
                    itemId,
                    Array.Empty<AssetFileTarget>());
                return;
            }

            TargetImportPopup.Show(
                anchor,
                groups,
                selections => ImportItemTargets(itemId, selections));
        }

        private bool HasItemTargets(string itemId)
        {
            return _manager.GetItemTargets(itemId).Count > 0;
        }

        private void ImportItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> selections)
        {
            _ = RunImport(() => _manager.ImportItemTargets(
                itemId,
                selections));
        }

    }
}
