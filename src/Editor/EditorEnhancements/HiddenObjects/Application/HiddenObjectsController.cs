using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectsController
    {
        private readonly IHiddenObjectRepository _repository;
        private readonly IHiddenObjectNavigator _navigator;
        private readonly IHiddenObjectExclusionSource _exclusionSource;
        private readonly HashSet<int> _selectedInstanceIds =
            new HashSet<int>();
        private IReadOnlyList<HiddenObjectSnapshotItem> _snapshot =
            Array.Empty<HiddenObjectSnapshotItem>();
        private int _selectedSceneHandle;
        private string _query = string.Empty;

        public HiddenObjectsController(
            IHiddenObjectRepository repository,
            IHiddenObjectNavigator navigator,
            IHiddenObjectExclusionSource exclusionSource = null)
        {
            _repository = repository ??
                throw new ArgumentNullException(nameof(repository));
            _navigator = navigator ??
                throw new ArgumentNullException(nameof(navigator));
            _exclusionSource = exclusionSource;
            State = CreateState();
        }

        public event Action<HiddenObjectsState> StateChanged;

        public HiddenObjectsState State { get; private set; }

        public void Refresh()
        {
            var loaded = _repository.Load() ??
                Array.Empty<HiddenObjectSnapshotItem>();
            var exclusions = _exclusionSource != null
                ? _exclusionSource.Load()
                : HiddenObjectExclusionRules.None;
            _snapshot = HiddenObjectExclusionPolicy.Apply(
                loaded,
                exclusions);
            var hiddenIds = new HashSet<int>(
                _snapshot
                    .Where(item => item.IsHidden)
                    .Select(item => item.InstanceId));
            _selectedInstanceIds.RemoveWhere(
                instanceId => !hiddenIds.Contains(instanceId));

            if (_selectedSceneHandle != 0 &&
                !_snapshot.Any(item =>
                    item.SceneHandle == _selectedSceneHandle))
            {
                _selectedSceneHandle = 0;
            }

            PublishState();
        }

        public void SetQuery(string query)
        {
            var next = (query ?? string.Empty).Trim();
            if (string.Equals(
                    _query,
                    next,
                    StringComparison.Ordinal))
            {
                return;
            }

            _query = next;
            PublishState();
        }

        public void SetSceneFilter(int sceneHandle)
        {
            if (sceneHandle != 0 &&
                !_snapshot.Any(item =>
                    item.SceneHandle == sceneHandle))
            {
                sceneHandle = 0;
            }

            if (_selectedSceneHandle == sceneHandle)
            {
                return;
            }

            _selectedSceneHandle = sceneHandle;
            PublishState();
        }

        public void SetSelected(int instanceId, bool selected)
        {
            var isHidden = _snapshot.Any(item =>
                item.InstanceId == instanceId && item.IsHidden);
            if (!isHidden)
            {
                return;
            }

            var changed = selected
                ? _selectedInstanceIds.Add(instanceId)
                : _selectedInstanceIds.Remove(instanceId);
            if (changed)
            {
                PublishState();
            }
        }

        public void SelectAllVisible()
        {
            var visibleIds = EnumerateNodes(State.SceneGroups)
                .Where(node => node.IsHidden)
                .Select(node => node.InstanceId);
            var changed = false;
            foreach (var instanceId in visibleIds)
            {
                changed |= _selectedInstanceIds.Add(instanceId);
            }

            if (changed)
            {
                PublishState();
            }
        }

        public void ClearSelection()
        {
            if (_selectedInstanceIds.Count == 0)
            {
                return;
            }

            _selectedInstanceIds.Clear();
            PublishState();
        }

        public int RevealSelected(string undoOperationName)
        {
            if (_selectedInstanceIds.Count == 0)
            {
                return 0;
            }

            var count = _repository.Reveal(
                _selectedInstanceIds.ToArray(),
                undoOperationName);
            Refresh();
            return count;
        }

        public void Focus(int instanceId)
        {
            _navigator.Focus(instanceId);
        }

        private void PublishState()
        {
            State = CreateState();
            StateChanged?.Invoke(State);
        }

        private HiddenObjectsState CreateState()
        {
            var groups = HiddenObjectTreeBuilder.Build(
                _snapshot,
                _selectedSceneHandle,
                _query);
            var sceneOptions = _snapshot
                .GroupBy(item => new
                {
                    item.SceneHandle,
                    item.SceneName
                })
                .OrderBy(group => group.Min(item => item.Order))
                .Select(group => new HiddenObjectSceneOption(
                    group.Key.SceneHandle,
                    group.Key.SceneName))
                .ToArray();
            var totalHiddenCount = _snapshot.Count(item => item.IsHidden);
            var visibleHiddenCount = EnumerateNodes(groups)
                .Count(node => node.IsHidden);

            return new HiddenObjectsState(
                groups,
                sceneOptions,
                _selectedInstanceIds.ToArray(),
                _selectedSceneHandle,
                _query,
                totalHiddenCount,
                visibleHiddenCount);
        }

        private static IEnumerable<HiddenObjectTreeNode> EnumerateNodes(
            IReadOnlyList<HiddenObjectSceneGroup> groups)
        {
            for (var groupIndex = 0;
                 groupIndex < groups.Count;
                 groupIndex++)
            {
                var roots = groups[groupIndex].Roots;
                for (var rootIndex = 0;
                     rootIndex < roots.Count;
                     rootIndex++)
                {
                    foreach (var node in EnumerateNode(roots[rootIndex]))
                    {
                        yield return node;
                    }
                }
            }
        }

        private static IEnumerable<HiddenObjectTreeNode> EnumerateNode(
            HiddenObjectTreeNode node)
        {
            yield return node;
            for (var i = 0; i < node.Children.Count; i++)
            {
                foreach (var child in EnumerateNode(node.Children[i]))
                {
                    yield return child;
                }
            }
        }
    }
}
