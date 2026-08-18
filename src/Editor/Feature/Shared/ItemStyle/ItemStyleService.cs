using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ee4v.ItemStyle
{
    public sealed class ItemStyleService
    {
        private const int RecentIconLimit = 8;
        private readonly string _scope;
        private readonly IItemStyleRepository _repository;

        internal ItemStyleService(
            string scope,
            IItemStyleRepository repository)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                throw new ArgumentException(
                    "Style scope is required.",
                    nameof(scope));
            }

            _scope = scope;
            _repository = repository ??
                throw new ArgumentNullException(nameof(repository));
        }

        public static ItemStyleService Create(string scope)
        {
            return new ItemStyleService(scope, ItemStyleStore.instance);
        }

        public ItemStyleValue Get(string identity)
        {
            return _repository.Get(_scope, identity) ??
                ItemStyleValue.Empty(identity);
        }

        public void SetColor(
            IReadOnlyList<string> identities,
            Color color)
        {
            SaveIfChanged(Apply(
                identities,
                style => style.WithColor(color)));
        }

        public void SetIcon(
            IReadOnlyList<string> identities,
            string iconGuid)
        {
            var changed = Apply(
                identities,
                style => style.WithIcon(iconGuid));
            if (changed && !string.IsNullOrEmpty(iconGuid))
            {
                _repository.RecordRecentIcon(
                    _scope,
                    iconGuid,
                    RecentIconLimit);
            }

            SaveIfChanged(changed);
        }

        public void Clear(IReadOnlyList<string> identities)
        {
            SaveIfChanged(Apply(
                identities,
                style => ItemStyleValue.Empty(style.Identity)));
        }

        public IReadOnlyList<string> GetRecentIconGuids()
        {
            return _repository.GetRecentIconGuids(_scope);
        }

        public bool RemoveRecentIcon(string iconGuid)
        {
            var changed = _repository.RemoveRecentIcon(
                _scope,
                iconGuid);
            SaveIfChanged(changed);
            return changed;
        }

        private bool Apply(
            IReadOnlyList<string> identities,
            Func<ItemStyleValue, ItemStyleValue> update)
        {
            if (identities == null || update == null)
            {
                return false;
            }

            var changed = false;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < identities.Count; i++)
            {
                var identity = identities[i];
                if (string.IsNullOrEmpty(identity) ||
                    !visited.Add(identity))
                {
                    continue;
                }

                _repository.Put(_scope, update(Get(identity)));
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
