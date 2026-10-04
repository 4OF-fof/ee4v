using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetManagerView
    {
        private void BuildFileDetail(VisualElement host, AssetFile file)
        {
            if (host == null || file == null)
            {
                return;
            }

            var extension = GetFileExtension(file);
            var eyebrow = I18N.Get("detail.file.eyebrow") +
                          (string.IsNullOrEmpty(extension)
                              ? string.Empty
                              : " · " + extension.ToUpperInvariant()) +
                          " · " +
                          file.SourceType.ToString().ToUpperInvariant();
            var header = new AssetDetailHeader(
                file.FileName,
                eyebrow,
                file.SourcePath,
                CreateAssetStatusState(file.IsArchived));
            if (SearchableFileTree.CanImport(
                    new FileTreeSelection(file, null)))
            {
                header.AddAction(AssetManagerControls.CreateButton(
                    I18N.Get("action.import"),
                    () => ImportFileTreeEntry(
                        new FileTreeSelection(file, null)),
                    "ee4v-asset-manager__primary-action"));
            }
            header.AddHeaderAction(file.IsArchived
                ? AssetManagerControls.CreateButton(
                    I18N.Get("action.restore"),
                    () => ArchiveFile(file.Id, false))
                : AssetManagerControls.CreateDangerButton(
                    I18N.Get("action.archive"),
                    () => ArchiveFile(file.Id, true)));
            if (file.IsArchived)
            {
                header.AddAction(AssetManagerControls.CreateDangerButton(
                    I18N.Get("action.delete"),
                    () => DeleteFile(file.Id)));
            }
            host.Add(header);

            BuildFileSettings(
                host,
                new[] { file },
                SearchableFileTree.CanImport(
                    new FileTreeSelection(file, null))
                    ? new[] { new AssetFileTarget
                    {
                        FileId = file.Id,
                        TargetPath = string.Empty
                    } }
                    : Array.Empty<AssetFileTarget>(),
                showFileId: true);
        }

        private void BuildFileSettings(
            VisualElement host,
            IReadOnlyList<AssetFile> files,
            IReadOnlyList<AssetFileTarget> dependencySources,
            bool showFileId)
        {
            var fileIds = files.Select(file => file.Id).ToArray();
            var assignedItemId = files[0].ItemId;
            var settings = new AssetDetailSection(
                I18N.Get("detail.settings"));
            var settingList = new AssetDetailSettingList();
            var itemId = AssetManagerControls.CreateTextField(
                string.Empty,
                "ee4v-asset-manager__setting-input");
            itemId.value = assignedItemId ?? string.Empty;
            settingList.AddRow(AssetDetailSettingRow.Editable(
                I18N.Get("field.assignedItem"),
                string.IsNullOrWhiteSpace(assignedItemId)
                    ? I18N.Get("common.none")
                    : assignedItemId,
                itemId,
                AssetManagerControls.CreateButton(
                    I18N.Get("action.moveFile"),
                    () => MoveFiles(fileIds, itemId.value)),
                I18N.Get("action.move")));

            AddDependencySetting(settingList, dependencySources);
            if (showFileId)
            {
                settingList.AddRow(new AssetDetailSettingRow(
                    I18N.Get("field.fileId"),
                    CreateMonoValue(fileIds[0])));
            }
            settings.Add(settingList);
            host.Add(settings);
        }

        private void AddDependencySetting(
            AssetDetailSettingList settingList,
            IReadOnlyList<AssetFileTarget> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                return;
            }

            var dependencyTargets = GetCommonFileDependencies(sources);
            var dependencySummary = dependencyTargets
                .Select(target => FormatTargetName(
                    _manager.GetFile(target.FileId), target))
                .ToArray();
            UiButton editDependencies = null;
            editDependencies = AssetManagerControls.CreateButton(
                I18N.Get("action.edit"),
                () => ShowDependencyEditor(
                    editDependencies,
                    sources,
                    dependencyTargets));
            if (sources.Count > 1)
            {
                editDependencies.tooltip = I18N.Get(
                    "detail.batchDependenciesTooltip");
            }
            var summary = UiTextFactory.Create(
                dependencySummary.Length == 0
                    ? I18N.Get("common.none")
                    : string.Join(" · ", dependencySummary));
            summary.SetWhiteSpace(WhiteSpace.Normal);
            settingList.AddRow(new AssetDetailSettingRow(
                I18N.Get("field.dependencies"),
                summary,
                editDependencies));
        }

        private void SelectFile(AssetFile file)
        {
            _viewState.SelectFile(file?.Id);
        }

        private void BuildFileEntryDetail(
            VisualElement detail,
            AssetFile file,
            AssetFileContentEntry entry)
        {
            var header = new AssetDetailHeader(
                Path.GetFileName(entry.Path),
                I18N.Get("detail.file.entryEyebrow") + " · " +
                I18N.Get(entry.Kind == AssetFileContentEntryKind.Directory
                    ? "detail.file.directory"
                    : "detail.file.file"),
                (file?.FileName ?? I18N.Get("common.none")) + " / " +
                entry.Path);
            detail.Add(header);

            var information = new AssetDetailSection(
                I18N.Get("detail.entryInformation"));
            var settingList = new AssetDetailSettingList();
            settingList.AddRow(new AssetDetailSettingRow(
                I18N.Get("field.file"),
                UiTextFactory.Create(
                    file?.FileName ?? I18N.Get("common.none"))));
            settingList.AddRow(new AssetDetailSettingRow(
                I18N.Get("field.kind"),
                UiTextFactory.Create(I18N.Get(
                    entry.Kind == AssetFileContentEntryKind.Directory
                        ? "detail.file.directory"
                        : "detail.file.file"))));
            settingList.AddRow(new AssetDetailSettingRow(
                I18N.Get("field.size"),
                UiTextFactory.Create(
                    entry.SizeBytes.ToString("N0") + " B")));
            settingList.AddRow(new AssetDetailSettingRow(
                I18N.Get("field.imported"),
                UiTextFactory.Create(I18N.Get(
                    IsEntryImported(file, entry)
                        ? "common.yes"
                        : "common.no"))));
            if (SearchableFileTree.CanImport(
                    new FileTreeSelection(file, entry)))
            {
                AddDependencySetting(settingList, new[]
                {
                    new AssetFileTarget
                    {
                        FileId = file.Id,
                        TargetPath = entry.Path
                    }
                });
            }
            information.Add(settingList);
            if (SearchableFileTree.CanImport(
                    new FileTreeSelection(file, entry)))
            {
                information.Add(AssetManagerControls.CreateButton(
                    I18N.Get("action.importEntry"),
                    () => ImportEntries(file.Id, entry.Path),
                    "ee4v-asset-manager__primary-action"));
            }
            detail.Add(information);
        }

        private void BuildFileTreeSelectionDetail(
            VisualElement detail,
            IReadOnlyList<FileTreeSelection> selections)
        {
            var importable = selections
                .Where(SearchableFileTree.CanImport)
                .ToArray();
            var files = selections
                .Where(selection => selection.File != null)
                .Select(selection => selection.File)
                .GroupBy(file => file.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            if (files.Length == 0)
            {
                detail.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.selectFile")));
                return;
            }

            var firstFile = files[0];
            var extension = GetFileExtension(firstFile);
            var commonExtension = files.All(file => string.Equals(
                GetFileExtension(file),
                extension,
                StringComparison.OrdinalIgnoreCase));
            var commonSource = files.All(file =>
                file.SourceType == firstFile.SourceType);
            var eyebrow = I18N.Get("detail.file.eyebrow") +
                          (commonExtension && !string.IsNullOrEmpty(extension)
                              ? " · " + extension.ToUpperInvariant()
                              : string.Empty) +
                          (commonSource
                              ? " · " + firstFile.SourceType.ToString()
                                  .ToUpperInvariant()
                              : string.Empty);
            var allArchived = files.All(file => file.IsArchived);
            var fileIds = files.Select(file => file.Id).ToArray();
            var header = new AssetDetailHeader(
                files.Length == 1
                    ? firstFile.FileName
                    : string.Format(
                        I18N.Get("detail.fileTreeSelectedCount"),
                        selections.Count),
                eyebrow,
                files.Length == 1 ? firstFile.SourcePath : null,
                CreateAssetStatusState(allArchived));
            if (importable.Length > 0)
            {
                header.AddAction(AssetManagerControls.CreateButton(
                    I18N.Get("action.import"),
                    () => _ = ImportFileTreeEntriesAsync(importable),
                    "ee4v-asset-manager__primary-action"));
            }
            header.AddHeaderAction(allArchived
                ? AssetManagerControls.CreateButton(
                    I18N.Get("action.restore"),
                    () => ArchiveFiles(fileIds, false))
                : AssetManagerControls.CreateDangerButton(
                    I18N.Get("action.archive"),
                    () => ArchiveFiles(fileIds, true)));
            if (allArchived)
            {
                header.AddAction(AssetManagerControls.CreateDangerButton(
                    I18N.Get("action.delete"),
                    () => DeleteFiles(fileIds)));
            }
            detail.Add(header);
            BuildFileSettings(
                detail,
                files,
                importable.Select(selection => new AssetFileTarget
                {
                    FileId = selection.File.Id,
                    TargetPath = selection.Entry?.Path ?? string.Empty
                }).GroupBy(target =>
                        target.FileId + "\n" + target.TargetPath,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToArray(),
                showFileId: false);
        }

        private void OnFileTreeSelectionChanged(
            IReadOnlyList<FileTreeSelection> selection)
        {
            _fileTreeSelections = selection ??
                Array.Empty<FileTreeSelection>();
            _viewState.SelectFile(_fileTreeSelections.Count == 1
                ? _fileTreeSelections[0].File?.Id
                : null);
        }

        private void BuildUnassignedFiles()
        {
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.Flex;
            _sortButton.style.display = DisplayStyle.Flex;
            _gridControls.style.display = DisplayStyle.None;
            var files = AssetManagerItemSort.Apply(
                GetUnassignedFiles()
                    .Where(file => AssetManagerSearch.MatchesFile(
                        file,
                        _search.Value,
                        _viewState.SearchTargets)),
                _viewState.ItemSortField,
                _viewState.IsItemSortReversed);
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("ee4v-asset-manager__file-list");
            if (files.Count == 0)
            {
                list.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.noUnassignedFiles")));
            }
            else
            {
                for (var index = 0; index < files.Count; index++)
                {
                    list.Add(CreateFileButton(files[index]));
                }
            }
            _content.Add(list);
        }

        private UiButton CreateFileButton(AssetFile file)
        {
            var selected = string.Equals(
                file.Id,
                _viewState.SelectedFileId,
                StringComparison.Ordinal);
            var button = AssetManagerControls.CreateButton(
                file.FileName,
                () => SelectFile(file),
                "ee4v-asset-manager__file-button");
            button.EnableInClassList(
                "ee4v-asset-manager__file-button--selected",
                selected);
            button.SetLabelColor(selected
                ? UiColorTokens.TextOnState
                : UiColorTokens.TextPrimary);
            var meta = UiTextFactory.Create(
                GetFileMeta(file),
                UiClassNames.SecondaryText,
                "ee4v-asset-manager__file-meta");
            meta.SetWhiteSpace(WhiteSpace.NoWrap);
            button.Add(meta);
            button.tooltip = file.FileName;
            return button;
        }

        private static UiTextElement CreateMonoValue(string value)
        {
            var text = UiTextFactory.Create(
                value,
                "ee4v-asset-manager__mono");
            text.SetWhiteSpace(WhiteSpace.Normal);
            return text;
        }

        private bool IsEntryImported(
            AssetFile file,
            AssetFileContentEntry entry)
        {
            if (file == null || entry == null)
            {
                return false;
            }

            var importedGuids = _manager.GetFileImportedAssetGuids(file.Id);
            if (string.Equals(
                    Path.GetExtension(entry.Path),
                    ".unitypackage",
                    StringComparison.OrdinalIgnoreCase))
            {
                return importedGuids.Count > 0;
            }

            if (!string.IsNullOrWhiteSpace(entry.AssetGuid))
            {
                return importedGuids.Contains(
                    entry.AssetGuid,
                    StringComparer.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                return false;
            }

            var targetPath = entry.Path
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
            if (targetPath.EndsWith(
                    ".meta",
                    StringComparison.OrdinalIgnoreCase))
            {
                targetPath = targetPath.Substring(
                    0,
                    targetPath.Length - ".meta".Length);
            }

            return importedGuids.Any(guid => IsEntryAssetPath(
                    AssetDatabase.GUIDToAssetPath(guid),
                    targetPath));
        }

        private static bool IsEntryAssetPath(
            string assetPath,
            string targetPath)
        {
            var normalizedAssetPath = (assetPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
            return targetPath.Length > 0 &&
                   (string.Equals(
                        normalizedAssetPath,
                        targetPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    normalizedAssetPath.EndsWith(
                        "/" + targetPath,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static string GetFileMeta(AssetFile file)
        {
            var extension = GetFileExtension(file);
            return string.IsNullOrEmpty(extension)
                ? file.SourceType.ToString()
                : extension.ToUpperInvariant();
        }

        private static string GetFileTypes(IReadOnlyList<AssetFile> files)
        {
            var extensions = (files ?? Array.Empty<AssetFile>())
                .Where(file => file != null)
                .Select(GetFileExtension)
                .Where(extension => extension.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
                .Select(extension => extension.ToUpperInvariant())
                .ToArray();
            return extensions.Length == 0
                ? I18N.Get("common.none")
                : string.Join(", ", extensions);
        }

        private static string GetItemSources(
            AssetItem item,
            IReadOnlyList<AssetFile> files)
        {
            var sources = (files ?? Array.Empty<AssetFile>())
                .Where(file => file != null)
                .Select(file => file.SourceType.ToString())
                .Concat(item != null && item.SourceType.HasValue
                    ? new[] { item.SourceType.Value.ToString() }
                    : Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return sources.Length == 0
                ? I18N.Get("common.none")
                : string.Join(", ", sources);
        }

        private static string GetFileExtension(AssetFile file)
        {
            if (file == null)
            {
                return string.Empty;
            }

            var extension = file.Extension;
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(file.FileName);
            }
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(file.SourcePath);
            }
            return (extension ?? string.Empty).Trim().TrimStart('.');
        }

        private static string FormatTimestamp(DateTime value)
        {
            return value == default
                ? I18N.Get("common.none")
                : value.ToString("g");
        }

    }
}
