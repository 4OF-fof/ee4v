using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetInfoController
    {
        private readonly IAssetManager _assetManager;
        private readonly IAssetManagerUiScheduler _scheduler;
        private readonly AssetItemCommands _commands;
        private SelectionTargetKind _targetKind;
        private string _targetId = string.Empty;
        private string _parentItemId = string.Empty;
        private bool _active;
        private bool _mutating;

        internal AssetInfoController(
            IAssetManager assetManager = null,
            IAssetManagerUiScheduler scheduler = null,
            AssetItemCommands commands = null)
        {
            _assetManager = assetManager ??
                AssetManagerUiDependencies.AssetManager;
            _scheduler = scheduler ??
                AssetManagerUiDependencies.Scheduler;
            _commands = commands ?? new AssetItemCommands(
                _assetManager,
                AssetManagerUiDependencies.FilePicker);
        }

        internal event Action<AssetInfoState> StateChanged;
        internal event Action<string> ErrorChanged;
        internal event Action<string> NoticeChanged;

        internal void Activate()
        {
            if (_active)
            {
                return;
            }

            _active = true;
            _assetManager.Changed += OnAssetManagerChanged;
        }

        internal void Deactivate()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            _assetManager.Changed -= OnAssetManagerChanged;
        }

        internal void SetSelection(
            ItemCardState item,
            AssetSelectionContentKind contentKind)
        {
            if (item == null)
            {
                Clear();
                return;
            }

            switch (contentKind)
            {
                case AssetSelectionContentKind.AssetItem:
                    _targetKind = SelectionTargetKind.Item;
                    _targetId = item.ItemId;
                    _parentItemId = item.ItemId;
                    break;
                case AssetSelectionContentKind.AssetVariantGroup:
                    _targetKind = SelectionTargetKind.VariantGroup;
                    _targetId = item.ItemId;
                    _parentItemId = item.ParentItemId;
                    break;
                case AssetSelectionContentKind.AssetVersionGroup:
                    _targetKind = SelectionTargetKind.VersionGroup;
                    _targetId = item.ItemId;
                    _parentItemId = item.ParentItemId;
                    break;
                default:
                    Clear();
                    return;
            }

            if (string.IsNullOrWhiteSpace(_targetId) ||
                string.IsNullOrWhiteSpace(_parentItemId))
            {
                Clear();
                return;
            }

            Reload();
        }

        internal void Clear()
        {
            _targetId = string.Empty;
            _parentItemId = string.Empty;
            StateChanged?.Invoke(null);
        }

        internal void Save(AssetInfoEditRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(_targetId))
            {
                return;
            }

            ExecuteMutation(() =>
            {
                switch (_targetKind)
                {
                    case SelectionTargetKind.Item:
                        _commands.UpdateItem(
                            _targetId,
                            request.Name,
                            request.Description,
                            request.TagNames);
                        break;
                    case SelectionTargetKind.VariantGroup:
                        _assetManager.UpdateVariantGroup(
                            _targetId,
                            new UpdateVariantGroupRequest
                            {
                                Name = request.Name,
                                Description = request.Description
                            });
                        break;
                    case SelectionTargetKind.VersionGroup:
                        _assetManager.UpdateVersionGroup(
                            _targetId,
                            new UpdateVersionGroupRequest
                            {
                                Name = request.Name,
                                Description = request.Description
                            });
                        break;
                }
                Reload();
                NoticeChanged?.Invoke(I18N.Get(
                    "assetManager.assetInfo.saved"));
            });
        }

        internal void AddFile()
        {
            if (_targetKind != SelectionTargetKind.Item ||
                string.IsNullOrWhiteSpace(_targetId))
            {
                return;
            }

            ExecuteMutation(() =>
            {
                if (!_commands.AddFile(_targetId))
                {
                    return;
                }

                Reload();
                NoticeChanged?.Invoke(I18N.Get(
                    "assetManager.assetInfo.fileAdded"));
            });
        }

        private void Reload()
        {
            if (string.IsNullOrWhiteSpace(_targetId))
            {
                StateChanged?.Invoke(null);
                return;
            }

            try
            {
                switch (_targetKind)
                {
                    case SelectionTargetKind.Item:
                        ReloadItem();
                        break;
                    case SelectionTargetKind.VariantGroup:
                        ReloadVariantGroup();
                        break;
                    case SelectionTargetKind.VersionGroup:
                        ReloadVersionGroup();
                        break;
                }
            }
            catch (Exception exception)
            {
                ErrorChanged?.Invoke(
                    AssetManagerUiErrorMessage.Format(exception));
            }
        }

        private void ExecuteMutation(Action operation)
        {
            try
            {
                _mutating = true;
                operation();
            }
            catch (Exception exception)
            {
                ErrorChanged?.Invoke(
                    AssetManagerUiErrorMessage.Format(exception));
            }
            finally
            {
                _mutating = false;
            }
        }

        private void OnAssetManagerChanged(AssetManagerChange change)
        {
            if (_mutating ||
                string.IsNullOrWhiteSpace(_targetId) ||
                change == null ||
                change.Kind != AssetManagerChangeKind.Catalog)
            {
                return;
            }

            _scheduler.RunOnMainThread(() =>
            {
                if (_active)
                {
                    Reload();
                }
            });
        }

        private void ReloadItem()
        {
            var item = _assetManager.GetItem(_targetId);
            if (item == null)
            {
                Clear();
                return;
            }

            StateChanged?.Invoke(CreateState(
                item.Id,
                item.Name,
                item.Description,
                (item.Tags ?? Array.Empty<AssetTag>())
                    .Where(tag => tag != null)
                    .Select(tag => tag.Name)
                    .ToArray(),
                _assetManager.GetFiles(item.Id),
                item.CreatedAt,
                item.UpdatedAt,
                showTags: true,
                canAddFile: true,
                availableTagNames: (_assetManager.GetTags() ??
                        Array.Empty<AssetTag>())
                    .Where(tag =>
                        tag != null &&
                        !string.IsNullOrWhiteSpace(tag.Name))
                    .Select(tag => tag.Name)
                    .ToArray()));
        }

        private void ReloadVariantGroup()
        {
            var group = _assetManager
                .GetVariantGroups(_parentItemId)
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(
                        candidate.Id,
                        _targetId,
                        StringComparison.Ordinal));
            if (group == null)
            {
                Clear();
                return;
            }

            var versionIds = new HashSet<string>(
                _assetManager
                    .GetVersionGroups(_parentItemId)
                    .Where(version =>
                        version != null &&
                        string.Equals(
                            version.VariantGroupId,
                            group.Id,
                            StringComparison.Ordinal))
                    .Select(version => version.Id),
                StringComparer.Ordinal);
            var files = _assetManager
                .GetFiles(_parentItemId)
                .Where(file =>
                    file != null &&
                    (string.Equals(
                         file.VariantGroupId,
                         group.Id,
                         StringComparison.Ordinal) ||
                     versionIds.Contains(file.VersionGroupId)))
                .ToArray();
            StateChanged?.Invoke(CreateState(
                group.Id,
                group.Name,
                group.Description,
                Array.Empty<string>(),
                files,
                group.CreatedAt,
                group.UpdatedAt,
                showTags: false,
                canAddFile: false));
        }

        private void ReloadVersionGroup()
        {
            var group = _assetManager
                .GetVersionGroups(_parentItemId)
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(
                        candidate.Id,
                        _targetId,
                        StringComparison.Ordinal));
            if (group == null)
            {
                Clear();
                return;
            }

            var files = _assetManager
                .GetFiles(_parentItemId)
                .Where(file =>
                    file != null &&
                    string.Equals(
                        file.VersionGroupId,
                        group.Id,
                        StringComparison.Ordinal))
                .ToArray();
            StateChanged?.Invoke(CreateState(
                group.Id,
                group.Name,
                group.Description,
                Array.Empty<string>(),
                files,
                group.CreatedAt,
                group.UpdatedAt,
                showTags: false,
                canAddFile: false));
        }

        internal static AssetInfoState CreateState(
            string targetId,
            string name,
            string description,
            IReadOnlyList<string> tagNames,
            IReadOnlyList<AssetFile> files,
            DateTime createdAt,
            DateTime updatedAt,
            bool showTags,
            bool canAddFile,
            IReadOnlyList<string> availableTagNames = null)
        {
            var safeFiles = files ?? Array.Empty<AssetFile>();
            var sources = safeFiles
                .Where(file => file != null && file.Origins != null)
                .SelectMany(file => file.Origins)
                .Where(origin => origin != null)
                .Select(origin => FormatSource(origin.SourceType))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var fileTypes = safeFiles
                .Where(file => file != null)
                .Select(file => GetFileType(file))
                .Where(extension => extension.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
                .Select(extension => extension.ToUpperInvariant())
                .ToArray();
            return new AssetInfoState(
                targetId,
                name,
                description,
                tagNames,
                safeFiles.Count,
                FormatTotalFileSize(safeFiles),
                fileTypes.Length == 0
                    ? I18N.Get("assetManager.assetInfo.noFileTypes")
                    : string.Join(", ", fileTypes),
                sources.Length == 0
                    ? I18N.Get("assetManager.assetInfo.noSources")
                    : string.Join(", ", sources),
                createdAt.ToString("g"),
                updatedAt.ToString("g"),
                showTags,
                canAddFile,
                availableTagNames);
        }

        private static string GetFileType(AssetFile file)
        {
            if (!string.IsNullOrWhiteSpace(file.Extension))
            {
                return FileExtensionUtility.Normalize(file.Extension);
            }

            var fileName = file.FileName ?? string.Empty;
            return fileName.LastIndexOf('.') < 0
                ? string.Empty
                : FileExtensionUtility.Normalize(fileName);
        }

        private static string FormatTotalFileSize(
            IReadOnlyList<AssetFile> files)
        {
            if (files.Any(file => file == null || !file.SizeBytes.HasValue))
            {
                return I18N.Get("assetManager.assetInfo.unknownFileSize");
            }

            var totalBytes = files.Sum(file =>
                Math.Max(0L, file.SizeBytes.Value));
            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            var value = (double)totalBytes;
            var unitIndex = 0;
            while (value >= 1024d && unitIndex < units.Length - 1)
            {
                value /= 1024d;
                unitIndex++;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "{0:0.##} {1}",
                value,
                units[unitIndex]);
        }

        private static string FormatSource(AssetSourceType source)
        {
            switch (source)
            {
                case AssetSourceType.Blm:
                    return "BLM";
                case AssetSourceType.Eagle:
                    return "Eagle";
                default:
                    return "ee4v";
            }
        }

        private enum SelectionTargetKind
        {
            Item,
            VariantGroup,
            VersionGroup
        }
    }
}
