using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetItemCommands
    {
        private readonly IAssetManager _assetManager;
        private readonly IAssetManagerFilePicker _filePicker;

        internal AssetItemCommands(
            IAssetManager assetManager = null,
            IAssetManagerFilePicker filePicker = null)
        {
            _assetManager = assetManager ??
                AssetManagerUiDependencies.AssetManager;
            _filePicker = filePicker ??
                AssetManagerUiDependencies.FilePicker;
        }

        internal AssetManagerFileSelection SelectFile()
        {
            return _filePicker.SelectFile(I18N.Get(
                "assetManager.assetInfo.selectFileTitle"));
        }

        internal bool CanRegisterDroppedFiles(
            IReadOnlyList<string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < paths.Count; i++)
            {
                if (_filePicker.ReadFile(paths[i]) == null)
                {
                    return false;
                }
            }

            return true;
        }

        internal int RegisterDroppedFiles(
            IReadOnlyList<string> paths)
        {
            var files = ReadDistinctFiles(paths);
            for (var i = 0; i < files.Count; i++)
            {
                _assetManager.RegisterFile(
                    null,
                    CreateRegisterFileRequest(files[i]));
            }

            return files.Count;
        }

        internal AssetItem UpdateItem(
            string itemId,
            string name,
            string description,
            IReadOnlyList<string> tagNames)
        {
            var tagIds = ResolveTagIds(tagNames);
            var item = _assetManager.UpdateItem(
                itemId,
                new UpdateAssetItemRequest
                {
                    Name = name,
                    Description = description
                });
            if (!HaveSameIds(
                    (item.Tags ?? Array.Empty<AssetTag>())
                        .Where(tag => tag != null)
                        .Select(tag => tag.Id),
                    tagIds))
            {
                _assetManager.SetItemTags(itemId, tagIds);
                item = _assetManager.GetItem(itemId) ?? item;
            }

            return item;
        }

        internal bool AddFile(
            string itemId,
            string versionGroupId,
            string variantGroupId)
        {
            var file = SelectFile();
            if (file == null)
            {
                return false;
            }

            _assetManager.RegisterFile(
                itemId,
                CreateRegisterFileRequest(
                    file,
                    versionGroupId,
                    variantGroupId));
            return true;
        }

        private IReadOnlyList<AssetManagerFileSelection>
            ReadDistinctFiles(IReadOnlyList<string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return Array.Empty<AssetManagerFileSelection>();
            }

            var result =
                new List<AssetManagerFileSelection>(paths.Count);
            var seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < paths.Count; i++)
            {
                var file = _filePicker.ReadFile(paths[i]);
                if (file == null || !seen.Add(file.Path))
                {
                    continue;
                }

                result.Add(file);
            }

            return result;
        }

        private IReadOnlyList<string> ResolveTagIds(
            IReadOnlyList<string> tagNames)
        {
            var names = NormalizeTagNames(tagNames);
            if (names.Count == 0)
            {
                return Array.Empty<string>();
            }

            var existing = _assetManager.GetTags();
            var tagsByName = new Dictionary<string, AssetTag>(
                StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < existing.Count; i++)
            {
                var tag = existing[i];
                if (tag != null &&
                    !string.IsNullOrWhiteSpace(tag.Name) &&
                    !tagsByName.ContainsKey(tag.Name))
                {
                    tagsByName.Add(tag.Name, tag);
                }
            }

            var ids = new string[names.Count];
            for (var i = 0; i < names.Count; i++)
            {
                AssetTag tag;
                if (!tagsByName.TryGetValue(names[i], out tag))
                {
                    tag = _assetManager.CreateTag(names[i]);
                    tagsByName.Add(tag.Name, tag);
                }

                ids[i] = tag.Id;
            }

            return ids;
        }

        private static IReadOnlyList<string> NormalizeTagNames(
            IReadOnlyList<string> tagNames)
        {
            if (tagNames == null || tagNames.Count == 0)
            {
                return Array.Empty<string>();
            }

            var seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var result = new List<string>(tagNames.Count);
            for (var i = 0; i < tagNames.Count; i++)
            {
                var name = (tagNames[i] ?? string.Empty).Trim();
                if (name.Length > 0 && seen.Add(name))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        private static bool HaveSameIds(
            IEnumerable<string> currentIds,
            IEnumerable<string> nextIds)
        {
            return new HashSet<string>(
                    currentIds ?? Array.Empty<string>(),
                    StringComparer.Ordinal)
                .SetEquals(nextIds ?? Array.Empty<string>());
        }

        private static RegisterFileRequest CreateRegisterFileRequest(
            AssetManagerFileSelection file,
            string versionGroupId = null,
            string variantGroupId = null)
        {
            return new RegisterFileRequest
            {
                FileName = file.FileName,
                FilePath = file.Path,
                SizeBytes = file.SizeBytes,
                VersionGroupId = versionGroupId,
                VariantGroupId = variantGroupId
            };
        }
    }
}
