using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.Navigation {
    public class NavigationDragManipulator : PointerManipulator {
        private readonly Dictionary<Ulid, VisualElement> _folderRowMap;
        private Ulid _draggingFolderId = Ulid.Empty;

        public NavigationDragManipulator(Dictionary<Ulid, VisualElement> folderRowMap) {
            _folderRowMap = folderRowMap;
        }

        public event Action<Ulid, Ulid> OnFolderMoved;
        public event Action<Ulid, Ulid, int> OnFolderReordered;

        protected override void RegisterCallbacksOnTarget() {
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        protected override void UnregisterCallbacksFromTarget() {
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        public void RegisterFolderItem(VisualElement itemRow, VisualElement treeItemContainer,
            VisualElement parentContainer, Func<Ulid, Ulid> getParentFolderId,
            Func<VisualElement, Ulid, int> getChildIndex) {
            itemRow.RegisterCallback<PointerMoveEvent>(evt => OnPointerMove(evt, itemRow, treeItemContainer));
            itemRow.RegisterCallback<PointerEnterEvent>(evt => OnPointerEnter(evt, itemRow, treeItemContainer));
            itemRow.RegisterCallback<PointerLeaveEvent>(_ =>
                OnPointerLeaveItem(itemRow, treeItemContainer));
            itemRow.RegisterCallback<PointerUpEvent>(evt => OnPointerUpItem(evt, itemRow, treeItemContainer,
                parentContainer, getParentFolderId, getChildIndex));
        }

        public void StartDrag(Ulid folderId, VisualElement itemRow) {
            _draggingFolderId = folderId;
            itemRow.style.opacity = 0.5f;
        }

        private void OnPointerMove(PointerMoveEvent evt, VisualElement itemRow, VisualElement treeItemContainer) {
            if (evt.pressedButtons != 1) return;

            if (_draggingFolderId == Ulid.Empty) return;

            if (_draggingFolderId != (Ulid)treeItemContainer.userData) UpdateDropVisualFeedback(itemRow, evt.position);
        }

        private void OnPointerEnter(PointerEnterEvent evt, VisualElement itemRow, VisualElement treeItemContainer) {
            if (_draggingFolderId == Ulid.Empty || _draggingFolderId == (Ulid)treeItemContainer.userData) return;
            UpdateDropVisualFeedback(itemRow, evt.position);
        }

        private void OnPointerLeaveItem(VisualElement itemRow, VisualElement treeItemContainer) {
            if (_draggingFolderId == Ulid.Empty || _draggingFolderId == (Ulid)treeItemContainer.userData) return;
            ClearDropVisualFeedback(itemRow);
        }

        private void OnPointerUpItem(PointerUpEvent evt, VisualElement itemRow, VisualElement treeItemContainer,
            VisualElement parentContainer, Func<Ulid, Ulid> getParentFolderId,
            Func<VisualElement, Ulid, int> getChildIndex) {
            if (_draggingFolderId == Ulid.Empty || _draggingFolderId == (Ulid)treeItemContainer.userData) return;

            var targetFolderId = (Ulid)treeItemContainer.userData;
            var sourceFolderId = _draggingFolderId;

            ClearDropVisualFeedback(itemRow);

            var localPos = itemRow.WorldToLocal(evt.position);
            var height = itemRow.resolvedStyle.height;
            var normalizedY = localPos.y / height;

            switch (normalizedY) {
                case < 0.25f: {
                    var targetParentId = getParentFolderId(targetFolderId);
                    var targetIndex = getChildIndex(parentContainer, targetFolderId);
                    if (targetIndex >= 0)
                        OnFolderReordered?.Invoke(targetParentId, sourceFolderId, targetIndex);
                    break;
                }
                case > 0.75f: {
                    var targetParentId = getParentFolderId(targetFolderId);
                    var targetIndex = getChildIndex(parentContainer, targetFolderId);
                    if (targetIndex >= 0)
                        OnFolderReordered?.Invoke(targetParentId, sourceFolderId, targetIndex + 1);
                    break;
                }
                default:
                    OnFolderMoved?.Invoke(sourceFolderId, targetFolderId);
                    break;
            }

            itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);
        }

        private void OnPointerUp(PointerUpEvent evt) {
            if (_draggingFolderId == Ulid.Empty) return;
            if (_folderRowMap.TryGetValue(_draggingFolderId, out var row))
                row.style.opacity = 1.0f;
            else
                Debug.LogWarning($"itemRow not found for: {_draggingFolderId}");
            _draggingFolderId = Ulid.Empty;
        }

        private void OnPointerLeave(PointerLeaveEvent evt) {
            if (_draggingFolderId == Ulid.Empty) return;
            if (_folderRowMap.TryGetValue(_draggingFolderId, out var row))
                row.style.opacity = 1.0f;
            _draggingFolderId = Ulid.Empty;
        }

        private static void UpdateDropVisualFeedback(VisualElement itemRow, Vector2 worldPosition) {
            ClearDropVisualFeedback(itemRow);

            var localPos = itemRow.WorldToLocal(worldPosition);
            var height = itemRow.resolvedStyle.height;
            var normalizedY = localPos.y / height;

            switch (normalizedY) {
                case < 0.25f:
                    itemRow.style.borderTopWidth = 2;
                    itemRow.style.borderTopColor = ColorPreset.AccentBlue;
                    break;
                case > 0.75f:
                    itemRow.style.borderBottomWidth = 2;
                    itemRow.style.borderBottomColor = ColorPreset.AccentBlue;
                    break;
                default:
                    itemRow.style.backgroundColor = ColorPreset.AccentBlue40Style;
                    break;
            }
        }

        private static void ClearDropVisualFeedback(VisualElement itemRow) {
            itemRow.style.borderTopWidth = 0;
            itemRow.style.borderBottomWidth = 0;
            itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);
        }
    }
}