using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class PrefabSelector : NavigationItem
    {
        private readonly IReadOnlyList<GameObject> _candidates;
        private readonly HashSet<string> _candidatePaths;
        private readonly Action<VisualElement, GameObject, Action<GameObject>> _showPicker;
        private readonly bool _allowEmpty;
        private GameObject _value;

        public PrefabSelector(
            IReadOnlyList<GameObject> candidates,
            Action<VisualElement, GameObject, Action<GameObject>> showPicker = null,
            bool allowEmpty = false)
            : base(onClick: null)
        {
            _candidates = candidates ?? Array.Empty<GameObject>();
            _showPicker = showPicker;
            _allowEmpty = allowEmpty;
            _candidatePaths = new HashSet<string>(
                _candidates
                    .Where(candidate => candidate != null)
                    .Select(AssetDatabase.GetAssetPath),
                StringComparer.OrdinalIgnoreCase);
            AddToClassList(
                "ee4v-ui-prefab-selector");
            clicked += OpenPopup;
            Trailing.Add(new Icon(
                FluentUiIcons.CreateState(
                    "chevron_down.png",
                    UiSizeTokens.Size12)));

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            SetEnabled(_candidates.Count > 0);
            Refresh();
        }

        public event Action<GameObject> ValueChanged;

        public event Action SelectionRejected;

        public GameObject Value => _value;

        public void ClearValue()
        {
            SetValue(null);
        }

        public static void ShowPicker(VisualElement anchor,
            IReadOnlyList<GameObject> candidates, GameObject selected,
            Action<GameObject> select, bool showAsGrid = false)
        {
            PrefabPickerWindow.Show(anchor, candidates, selected, select, showAsGrid);
        }

        public void SetValueWithoutNotify(GameObject prefab)
        {
            if (!Accepts(prefab))
            {
                return;
            }

            _value = prefab;
            Refresh();
        }

        private void OpenPopup()
        {
            if (_showPicker != null)
            {
                _showPicker(this, _value, SetValue);
                return;
            }
            PrefabPickerWindow.Show(
                this,
                _candidates,
                _value,
                SetValue);
        }

        private void SetValue(GameObject prefab)
        {
            if (!Accepts(prefab))
            {
                SelectionRejected?.Invoke();
                return;
            }

            if (_value == prefab)
            {
                return;
            }

            _value = prefab;
            Refresh();
            ValueChanged?.Invoke(_value);
        }

        private void Refresh()
        {
            var path = AssetDatabase.GetAssetPath(_value);
            SetState(new NavigationItemState(
                _value != null
                    ? _value.name
                    : UiLocalization.Get("ui.prefabPicker.select"),
                path));
            tooltip = path;
        }

        private bool Accepts(GameObject prefab)
        {
            if (prefab == null) { return _allowEmpty; }
            var path = AssetDatabase.GetAssetPath(prefab);
            return _candidates.Contains(prefab) ||
                (!string.IsNullOrEmpty(path) && _candidatePaths.Contains(path));
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            var prefab = GetDraggedPrefab();
            DragAndDrop.visualMode = prefab != null && Accepts(prefab)
                ? DragAndDropVisualMode.Link
                : DragAndDropVisualMode.Rejected;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var prefab = GetDraggedPrefab();
            if (prefab != null && Accepts(prefab))
            {
                DragAndDrop.AcceptDrag();
                SetValue(prefab);
            }
            else
            {
                SelectionRejected?.Invoke();
            }

            evt.StopPropagation();
        }

        private static GameObject GetDraggedPrefab()
        {
            return DragAndDrop.objectReferences
                .OfType<GameObject>()
                .FirstOrDefault();
        }
    }
}
