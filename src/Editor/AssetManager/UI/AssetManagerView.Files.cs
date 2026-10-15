using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
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
            var header = CreateFileDetailHeader(host,
                file.FileName,
                eyebrow,
                file.SourcePath,
                AssetFileTreeBuilder.CanAnalyze(file) ? "folder_zip.png" : "document.png",
                CreateAssetStatusState(file.IsArchived));
            if (SearchableFileTree.CanImport(
                    new FileTreeSelection(file, null)))
            {
                header.AddHeaderAction(AssetManagerControls.CreateButton(
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
            AddFileDetailFacts(host,
                new ValueDisplay(I18N.Get("field.size"), FormatTotalFileSize(new[] { file }), ValueDisplayLayout.Stacked),
                new ValueDisplay(I18N.Get("field.fileTypes"), string.IsNullOrEmpty(extension)
                    ? I18N.Get("common.none") : extension.ToUpperInvariant(), ValueDisplayLayout.Stacked),
                new ValueDisplay(I18N.Get("field.imported"), I18N.Get(
                    _manager.GetFileImportedAssetGuids(file.Id).Count > 0 ? "common.yes" : "common.no"),
                    ValueDisplayLayout.Stacked));
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
                    : Array.Empty<AssetFileTarget>());
            var information = new DetailSection(I18N.Get("detail.information"));
            information.Add(new ValueDisplay(I18N.Get("field.createdAt"), FormatTimestamp(file.CreatedAt)));
            information.Add(new ValueDisplay(I18N.Get("field.updatedAt"), FormatTimestamp(file.UpdatedAt)));
            information.Add(new ValueDisplay(I18N.Get("field.fileId"), CreateMonoValue(file.Id)));
            host.Add(information);
        }

        private static DetailHeader CreateFileDetailHeader(VisualElement host, string title,
            string eyebrow, string path, string iconName, BadgeState status = null)
        {
            var hero = new VisualElement();
            hero.AddToClassList("ee4v-asset-manager__file-detail-hero");
            var icon = new Icon(AssetManagerControls.LoadFluentIconState(iconName,
                UiSizeTokens.Size24, tintColor: UiColorTokens.TextMuted));
            icon.AddToClassList("ee4v-asset-manager__file-detail-icon");
            hero.Add(icon);
            var header = new DetailHeader(title, eyebrow, path, status);
            header.AddToClassList("ee4v-asset-manager__file-detail-header");
            header.TitleText.SetWhiteSpace(WhiteSpace.Normal);
            hero.Add(header);
            hero.RegisterCallback<GeometryChangedEvent>(evt =>
                hero.EnableInClassList("ee4v-asset-manager__file-detail-hero--compact", evt.newRect.width < 520f));
            host.Add(hero);
            return header;
        }

        private static void AddFileDetailFacts(VisualElement host, params ValueDisplay[] values)
        {
            var facts = new VisualElement();
            facts.AddToClassList("ee4v-asset-manager__file-detail-facts");
            foreach (var value in values) { facts.Add(value); }
            host.Add(facts);
        }

        private void BuildFileSettings(
            VisualElement host,
            IReadOnlyList<AssetFile> files,
            IReadOnlyList<AssetFileTarget> dependencySources)
        {
            AddFileDependencySettings(host, dependencySources);
            var fileIds = files.Select(file => file.Id).ToArray();
            var assignedItemId = files[0].ItemId;
            var assignedItem = string.IsNullOrEmpty(assignedItemId) ? null : FindVariantItem(assignedItemId);
            var settings = new DetailSection(
                I18N.Get("detail.settings"));
            var settingList = new PropertyList();
            var itemId = AssetManagerControls.CreateTextField(
                string.Empty,
                "ee4v-asset-manager__setting-input");
            itemId.Input.Value = assignedItemId ?? string.Empty;
            var assignment = PropertyRow.Editable(
                I18N.Get("field.assignedItem"),
                string.IsNullOrWhiteSpace(assignedItemId)
                    ? I18N.Get("common.none")
                    : assignedItem?.Name ?? assignedItemId,
                itemId,
                AssetManagerControls.CreateButton(
                    I18N.Get("action.moveFile"),
                    () => MoveFiles(fileIds, itemId.Input.Value)),
                I18N.Get("action.move"));
            assignment.tooltip = assignedItemId ?? string.Empty;
            settingList.AddRow(assignment);
            settings.Add(settingList);
            host.Add(settings);
        }

        private void AddFileDependencySettings(VisualElement host, IReadOnlyList<AssetFileTarget> sources)
        {
            if (sources == null || sources.Count == 0) { return; }
            var section = new DetailSection(I18N.Get("detail.importSettings"));
            var list = new PropertyList();
            AddDependencySetting(list, sources);
            section.Add(list);
            host.Add(section);
        }

        private void AddDependencySetting(
            PropertyList settingList,
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
                    dependencyTargets),
                "ee4v-ui-details__inline-action");
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
            settingList.AddRow(new PropertyRow(
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
            var header = CreateFileDetailHeader(detail,
                Path.GetFileName(entry.Path),
                I18N.Get("detail.file.entryEyebrow") + " · " +
                I18N.Get(entry.Kind == AssetFileContentEntryKind.Directory
                    ? "detail.file.directory"
                    : "detail.file.file"),
                (file?.FileName ?? I18N.Get("common.none")) + " / " +
                entry.Path,
                entry.Kind == AssetFileContentEntryKind.Directory ? "folder.png" : "document.png");
            AddFileDetailFacts(detail,
                new ValueDisplay(I18N.Get("field.size"), AssetFileTreeBuilder.FormatSize(entry.SizeBytes),
                    ValueDisplayLayout.Stacked),
                new ValueDisplay(I18N.Get("field.kind"), I18N.Get(
                    entry.Kind == AssetFileContentEntryKind.Directory
                        ? "detail.file.directory"
                        : "detail.file.file"), ValueDisplayLayout.Stacked),
                new ValueDisplay(I18N.Get("field.imported"), I18N.Get(
                    IsEntryImported(file, entry)
                        ? "common.yes"
                        : "common.no"), ValueDisplayLayout.Stacked));
            if (SearchableFileTree.CanImport(
                    new FileTreeSelection(file, entry)))
            {
                AddFileDependencySettings(detail, new[]
                {
                    new AssetFileTarget
                    {
                        FileId = file.Id,
                        TargetPath = entry.Path
                    }
                });
                header.AddHeaderAction(AssetManagerControls.CreateButton(
                    I18N.Get("action.importEntry"),
                    () => ImportEntries(file.Id, entry.Path),
                    "ee4v-asset-manager__primary-action"));
            }
            var information = new DetailSection(I18N.Get("detail.entryInformation"));
            information.Add(new ValueDisplay(I18N.Get("field.file"), file?.FileName ?? I18N.Get("common.none")));
            information.Add(new ValueDisplay(I18N.Get("field.path"), entry.Path));
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
            var header = CreateFileDetailHeader(detail,
                string.Format(
                        I18N.Get("detail.fileTreeSelectedCount"),
                        selections.Count),
                eyebrow,
                files.Length == 1 ? firstFile.SourcePath : null, "document.png",
                CreateAssetStatusState(allArchived));
            if (importable.Length > 0)
            {
                header.AddHeaderAction(AssetManagerControls.CreateButton(
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
            AddFileDetailFacts(detail,
                new ValueDisplay(I18N.Get("field.fileCount"), files.Length.ToString(), ValueDisplayLayout.Stacked));
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
                    .ToArray());
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
            if (files.Count == 0)
            {
                _content.Add(AssetManagerControls.CreateEmptyState(
                    I18N.Get("notice.noItems")));
                return;
            }
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("ee4v-asset-manager__file-list");
            for (var index = 0; index < files.Count; index++)
            {
                list.Add(CreateFileButton(files[index]));
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
