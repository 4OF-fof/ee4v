using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.Internal.EditorAPI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerProtectedInspectorPresenter :
        IDisposable
    {
        private const double RefreshIntervalSeconds = 0.2d;

        private readonly IAssetManagerProtectionActions _actions;
        private readonly Dictionary<int, HostState> _states =
            new Dictionary<int, HostState>();
        private readonly Dictionary<int, string>
            _dismissedWarningAssetGuids =
                new Dictionary<int, string>();
        private double _nextRefresh;
        private bool _initialized;
        private bool _disposed;

        internal AssetManagerProtectedInspectorPresenter(
            IAssetManagerProtectionActions actions)
        {
            _actions = actions ??
                throw new ArgumentNullException(nameof(actions));
        }

        internal void Initialize()
        {
            if (_initialized || _disposed)
            {
                return;
            }

            _initialized = true;
            _actions.Changed += ForceRefresh;
            EditorApplication.update += OnEditorUpdate;
            ForceRefresh();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _actions.Changed -= ForceRefresh;
            EditorApplication.update -= OnEditorUpdate;
            foreach (var state in _states.Values)
            {
                state.Restore();
            }

            _states.Clear();
            _dismissedWarningAssetGuids.Clear();
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup <
                _nextRefresh)
            {
                return;
            }

            _nextRefresh =
                EditorApplication.timeSinceStartup +
                RefreshIntervalSeconds;
            Refresh();
        }

        private void ForceRefresh()
        {
            _nextRefresh = 0d;
            Refresh();
        }

        private void Refresh()
        {
            if (_disposed ||
                !InspectorHost.TryGetSnapshots(
                    out var snapshots))
            {
                return;
            }

            var activeWindowIds =
                new HashSet<int>();
            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot?.Window == null)
                {
                    continue;
                }

                var windowId =
                    snapshot.Window.GetInstanceID();
                activeWindowIds.Add(windowId);
                if (!TryResolveProtectedAsset(
                        snapshot,
                        out var assetGuid,
                        out var assetPath))
                {
                    _dismissedWarningAssetGuids.Remove(
                        windowId);
                    Restore(windowId);
                    continue;
                }

                if (_dismissedWarningAssetGuids.TryGetValue(
                        windowId,
                        out var dismissedWarningAssetGuid))
                {
                    if (string.Equals(
                            dismissedWarningAssetGuid,
                            assetGuid,
                            StringComparison.Ordinal))
                    {
                        Restore(windowId);
                        continue;
                    }

                    _dismissedWarningAssetGuids.Remove(
                        windowId);
                }

                if (_states.TryGetValue(
                        windowId,
                        out var current) &&
                    current.Matches(
                        snapshot,
                        assetGuid))
                {
                    current.BringOverlayToFront();
                    continue;
                }

                Restore(windowId);
                var view = CreateView(
                    windowId,
                    assetGuid,
                    assetPath);
                if (HostState.TryCreate(
                        snapshot,
                        assetGuid,
                        view,
                        out var created))
                {
                    _states[windowId] = created;
                }
            }

            foreach (var windowId in _states.Keys
                         .Where(id =>
                             !activeWindowIds.Contains(id))
                         .ToArray())
            {
                Restore(windowId);
            }

            foreach (var windowId in _dismissedWarningAssetGuids.Keys
                         .Where(id =>
                             !activeWindowIds.Contains(id))
                         .ToArray())
            {
                _dismissedWarningAssetGuids.Remove(
                    windowId);
            }
        }

        private bool TryResolveProtectedAsset(
            InspectorHostSnapshot snapshot,
            out string assetGuid,
            out string assetPath)
        {
            assetGuid = string.Empty;
            assetPath = string.Empty;
            if (snapshot.InspectedObjects == null ||
                snapshot.InspectedObjects.Count != 1)
            {
                return false;
            }

            var inspected = snapshot.InspectedObjects[0];
            if (inspected == null)
            {
                return false;
            }

            assetPath = inspected is AssetImporter importer
                ? importer.assetPath
                : AssetDatabase.GetAssetPath(inspected);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            assetGuid =
                AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(assetGuid) ||
                !_actions.IsProtected(assetGuid))
            {
                return false;
            }

            return true;
        }

        private ProtectedAssetInspectorView CreateView(
            int windowId,
            string assetGuid,
            string assetPath)
        {
            return new ProtectedAssetInspectorView(
                () => DismissWarning(
                    windowId,
                    assetGuid),
                () => AssetManagerProtectionMenu.Unprotect(
                    _actions,
                    assetGuid,
                    assetPath));
        }

        private void DismissWarning(
            int windowId,
            string assetGuid)
        {
            _dismissedWarningAssetGuids[windowId] =
                assetGuid;
            Restore(windowId);
        }

        private void Restore(int windowId)
        {
            if (!_states.TryGetValue(
                    windowId,
                    out var state))
            {
                return;
            }

            state.Restore();
            _states.Remove(windowId);
        }

        private sealed class HostState
        {
            private readonly VisualElement _editors;
            private readonly VisualElement _view;

            private HostState(
                InspectorHostSnapshot snapshot,
                string assetGuid,
                VisualElement view)
            {
                Window = snapshot.Window;
                AssetGuid = assetGuid;
                _editors = snapshot.EditorsElement;
                _view = view;
            }

            private EditorWindow Window { get; }
            private string AssetGuid { get; }

            internal static bool TryCreate(
                InspectorHostSnapshot snapshot,
                string assetGuid,
                VisualElement view,
                out HostState state)
            {
                state = null;
                var parent =
                    snapshot.EditorsElement?.parent;
                if (parent == null || view == null)
                {
                    return false;
                }

                state = new HostState(
                    snapshot,
                    assetGuid,
                    view);
                parent.Add(view);

                return true;
            }

            internal bool Matches(
                InspectorHostSnapshot snapshot,
                string assetGuid)
            {
                return Window == snapshot.Window &&
                       _view?.parent != null &&
                       ReferenceEquals(
                           _editors,
                           snapshot.EditorsElement) &&
                       string.Equals(
                           AssetGuid,
                           assetGuid,
                           StringComparison.Ordinal);
            }

            internal void BringOverlayToFront()
            {
                _view?.BringToFront();
            }

            internal void Restore()
            {
                _view?.RemoveFromHierarchy();
            }
        }
    }
}
