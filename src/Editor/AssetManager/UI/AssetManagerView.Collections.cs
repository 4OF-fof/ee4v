using System;
using System.Collections.Generic;
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
        private void ShowCollectionContextMenu(
            VisualElement anchor,
            AssetCollection collection)
        {
            var menu = new GenericMenu();
            menu.AddItem(
                UiTextFactory.CreateGuiContent(I18N.Get("action.edit")),
                false,
                () => ShowCollectionEditor(anchor, collection));
            var ids = _manager.GetCollections()
                .Select(entry => entry.Id).ToList();
            var index = ids.IndexOf(collection.Id);
            AddCollectionMoveMenuItem(
                menu, collection.Id, "navigation.moveCollectionUp",
                -1, index > 0);
            AddCollectionMoveMenuItem(
                menu, collection.Id, "navigation.moveCollectionDown",
                1, index >= 0 && index < ids.Count - 1);
            menu.AddSeparator(string.Empty);
            menu.AddItem(
                UiTextFactory.CreateGuiContent(I18N.Get("action.delete")),
                false,
                () => DeleteCollection(collection?.Id));
            menu.ShowAsContext();
        }

        private void RegisterCollectionDrag(
            NavigationItem row,
            AssetCollection collection)
        {
            row.tooltip = collection.Name + "\n" +
                I18N.Get("navigation.reorderCollection");
            row.AddManipulator(new CollectionDragManipulator(
                () => new CollectionDragPayload
                {
                    Manager = _manager,
                    CollectionId = collection.Id,
                    Name = collection.Name
                }));
        }

        private void RegisterCollectionReordering(
            VisualElement section,
            IReadOnlyList<AssetCollection> collections)
        {
            var rows = section.Children().OfType<NavigationItem>().ToArray();
            if (rows.Length == 0)
            {
                return;
            }

            var indicator = new VisualElement { pickingMode = PickingMode.Ignore };
            indicator.AddToClassList("ee4v-asset-manager__collection-insertion-line");
            section.Add(indicator);
            NavigationItem activeRow = null;

            void ClearTarget()
            {
                activeRow?.RemoveFromClassList(
                    "ee4v-asset-manager__collection-drop-target");
                activeRow = null;
                indicator.style.display = DisplayStyle.None;
            }

            bool TryGetInsertion(
                Vector2 position,
                out CollectionDragPayload payload,
                out int index)
            {
                payload = DragAndDrop.GetGenericData(CollectionDragDataKey)
                    as CollectionDragPayload;
                index = 0;
                if (payload == null || !ReferenceEquals(payload.Manager, _manager) ||
                    position.y < rows[0].worldBound.yMin ||
                    position.y > rows[rows.Length - 1].worldBound.yMax +
                        rows[rows.Length - 1].resolvedStyle.marginBottom ||
                    position.x < section.worldBound.xMin ||
                    position.x > section.worldBound.xMax)
                {
                    return false;
                }
                while (index < rows.Length &&
                    position.y >= rows[index].worldBound.center.y)
                {
                    index++;
                }
                return true;
            }

            section.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (!TryGetInsertion(evt.mousePosition, out _, out var index))
                {
                    ClearTarget();
                    return;
                }

                var hoveredRow = rows.FirstOrDefault(row =>
                    row.worldBound.Contains(evt.mousePosition));
                if (!ReferenceEquals(activeRow, hoveredRow))
                {
                    activeRow?.RemoveFromClassList(
                        "ee4v-asset-manager__collection-drop-target");
                    activeRow = hoveredRow;
                    activeRow?.AddToClassList(
                        "ee4v-asset-manager__collection-drop-target");
                }

                var boundary = index == 0 ? rows[0].worldBound.yMin :
                    index == rows.Length ? rows[rows.Length - 1].worldBound.yMax :
                    (rows[index - 1].worldBound.yMax + rows[index].worldBound.yMin) * 0.5f;
                indicator.style.top = Mathf.Round(section.WorldToLocal(
                    new Vector2(section.worldBound.xMin, boundary)).y);
                indicator.style.display = DisplayStyle.Flex;
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                evt.StopPropagation();
            });
            section.RegisterCallback<DragLeaveEvent>(evt =>
            {
                if (ReferenceEquals(evt.target, section))
                {
                    ClearTarget();
                }
            });
            section.RegisterCallback<DragExitedEvent>(_ => ClearTarget());
            section.RegisterCallback<DetachFromPanelEvent>(_ => ClearTarget());
            section.RegisterCallback<DragPerformEvent>(evt =>
            {
                ClearTarget();
                if (!TryGetInsertion(evt.mousePosition, out var payload, out var index))
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                var insertAfter = index == rows.Length;
                MoveCollection(payload.CollectionId,
                    collections[insertAfter ? index - 1 : index].Id, insertAfter);
                evt.StopPropagation();
            });
        }

        private void AddCollectionMoveMenuItem(
            GenericMenu menu,
            string collectionId,
            string labelKey,
            int offset,
            bool enabled)
        {
            var label = UiTextFactory.CreateGuiContent(I18N.Get(labelKey));
            if (!enabled)
            {
                menu.AddDisabledItem(label);
                return;
            }
            menu.AddItem(label, false, () =>
            {
                var ids = _manager.GetCollections()
                    .Select(collection => collection.Id).ToList();
                var index = ids.IndexOf(collectionId);
                var destination = index + offset;
                if (index < 0 || destination < 0 || destination >= ids.Count)
                {
                    return;
                }
                MoveCollection(collectionId, ids[destination], offset > 0);
            });
        }

        private void MoveCollection(
            string collectionId,
            string targetId,
            bool insertAfter)
        {
            Run(() =>
            {
                var ids = _manager.GetCollections()
                    .Select(collection => collection.Id).ToList();
                var originalIndex = ids.IndexOf(collectionId);
                if (originalIndex < 0 || !ids.Contains(targetId) ||
                    string.Equals(collectionId, targetId, StringComparison.Ordinal))
                {
                    return;
                }
                ids.RemoveAt(originalIndex);
                var destination = ids.IndexOf(targetId) + (insertAfter ? 1 : 0);
                ids.Insert(destination, collectionId);
                if (destination != originalIndex)
                {
                    _manager.ReorderCollections(ids);
                }
            });
        }

        private void ShowNewCollection(VisualElement anchor)
        {
            AssetCollectionCreationPopup.Show(
                anchor,
                null,
                CreateCollection);
        }

        private void ShowCollectionEditor(
            VisualElement anchor,
            AssetCollection collection)
        {
            if (collection == null)
            {
                return;
            }

            AssetCollectionCreationPopup.Show(
                anchor,
                collection,
                (name, icon, root) => UpdateCollection(
                    collection.Id,
                    name,
                    icon,
                    root));
        }

        private bool UpdateCollection(
            string id,
            string name,
            AssetCollectionIcon icon,
            AssetFilterNode root)
        {
            return Run(() => _manager.UpdateCollection(
                id,
                new UpdateAssetCollectionRequest
                {
                    Name = name,
                    Icon = icon,
                    Root = root
                }));
        }

        private bool CreateCollection(
            string name,
            AssetCollectionIcon icon,
            AssetFilterNode root)
        {
            return Run(() =>
            {
                var created = _manager.CreateCollection(
                    new CreateAssetCollectionRequest
                    {
                        Name = name,
                        Icon = icon,
                        Root = root
                    });
                _viewState.SelectCollection(created.Id);
            });
        }

        private void DeleteCollection(string id)
        {
            if (string.IsNullOrEmpty(id) ||
                !Confirm(
                    I18N.Get("confirm.deleteCollection.title"),
                    I18N.Get("confirm.deleteCollection.message")))
            {
                return;
            }

            Run(() =>
            {
                _manager.DeleteCollection(id);
                _viewState.SelectPage(AssetManagerPage.Library);
            });
        }

    }
}
