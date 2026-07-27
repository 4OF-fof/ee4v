using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ee4v.FolderStyle
{
    internal sealed class FolderStyleService
    {
        internal const int RecentIconLimit = 8;

        private readonly IFolderStyleRepository _repository;

        public FolderStyleService(IFolderStyleRepository repository)
        {
            _repository = repository ??
                throw new ArgumentNullException(nameof(repository));
        }

        public FolderStyleValue Get(string folderGuid)
        {
            return _repository.Get(folderGuid) ??
                FolderStyleValue.Empty(folderGuid);
        }

        public void SetColor(
            IReadOnlyList<string> folderGuids,
            Color color)
        {
            SaveIfChanged(Apply(
                folderGuids,
                style => style.WithColor(color)));
        }

        public void SetIcon(
            IReadOnlyList<string> folderGuids,
            string iconGuid)
        {
            var changed = Apply(
                folderGuids,
                style => style.WithIcon(iconGuid));
            if (changed &&
                !string.IsNullOrEmpty(iconGuid))
            {
                _repository.RecordRecentIcon(
                    iconGuid,
                    RecentIconLimit);
            }

            SaveIfChanged(changed);
        }

        public void Clear(IReadOnlyList<string> folderGuids)
        {
            SaveIfChanged(Apply(
                folderGuids,
                style => FolderStyleValue.Empty(
                    style.FolderGuid)));
        }

        public IReadOnlyList<string> GetRecentIconGuids()
        {
            return _repository.GetRecentIconGuids();
        }

        public void RemoveRecentIcon(string iconGuid)
        {
            SaveIfChanged(
                _repository.RemoveRecentIcon(iconGuid));
        }

        private bool Apply(
            IReadOnlyList<string> folderGuids,
            Func<FolderStyleValue, FolderStyleValue> update)
        {
            if (folderGuids == null || update == null)
            {
                return false;
            }

            var changed = false;
            var visited =
                new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < folderGuids.Count; i++)
            {
                var folderGuid = folderGuids[i];
                if (string.IsNullOrEmpty(folderGuid) ||
                    !visited.Add(folderGuid))
                {
                    continue;
                }

                _repository.Put(
                    update(Get(folderGuid)));
                changed = true;
            }

            return changed;
        }

        private void SaveIfChanged(bool changed)
        {
            if (changed)
            {
                _repository.Save();
            }
        }
    }
}
