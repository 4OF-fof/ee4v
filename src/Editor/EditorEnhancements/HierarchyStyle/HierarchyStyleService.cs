using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal sealed class HierarchyStyleService
    {
        internal const int RecentIconLimit = 8;

        private readonly IHierarchyStyleRepository _repository;

        public HierarchyStyleService(
            IHierarchyStyleRepository repository)
        {
            _repository = repository ??
                throw new ArgumentNullException(
                    nameof(repository));
        }

        public HierarchyStyleValue Get(string objectId)
        {
            return _repository.Get(objectId) ??
                HierarchyStyleValue.Empty(objectId);
        }

        public void SetBackgroundColor(
            IReadOnlyList<string> objectIds,
            Color color)
        {
            SaveIfChanged(Apply(
                objectIds,
                style =>
                    style.WithBackgroundColor(color)));
        }

        public void SetIcon(
            IReadOnlyList<string> objectIds,
            string iconGuid)
        {
            var changed = Apply(
                objectIds,
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
            IReadOnlyList<string> objectIds,
            Func<HierarchyStyleValue, HierarchyStyleValue>
                update)
        {
            if (objectIds == null || update == null)
            {
                return false;
            }

            var changed = false;
            var visited =
                new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < objectIds.Count; i++)
            {
                var objectId = objectIds[i];
                if (string.IsNullOrEmpty(objectId) ||
                    !visited.Add(objectId))
                {
                    continue;
                }

                _repository.Put(update(Get(objectId)));
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
