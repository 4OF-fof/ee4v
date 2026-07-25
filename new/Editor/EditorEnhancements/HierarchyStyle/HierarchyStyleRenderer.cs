using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Injector;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal sealed class HierarchyStyleRenderer
    {
        private const float RowLeft = 32f;

        private readonly HierarchyStyleService _service;
        private readonly HierarchyObjectIdentity _identity;
        private readonly HierarchyStyleIconApplier _iconApplier;
        private readonly HierarchyStyleAltTrigger _altTrigger;
        private readonly Action<
            IReadOnlyList<GameObject>,
            Vector2> _openEditor;

        public HierarchyStyleRenderer(
            HierarchyStyleService service,
            HierarchyObjectIdentity identity,
            HierarchyStyleIconApplier iconApplier,
            HierarchyStyleAltTrigger altTrigger,
            Action<IReadOnlyList<GameObject>, Vector2>
                openEditor)
        {
            _service = service ??
                throw new ArgumentNullException(
                    nameof(service));
            _identity = identity ??
                throw new ArgumentNullException(
                    nameof(identity));
            _iconApplier = iconApplier ??
                throw new ArgumentNullException(
                    nameof(iconApplier));
            _altTrigger = altTrigger ??
                throw new ArgumentNullException(
                    nameof(altTrigger));
            _openEditor = openEditor ??
                throw new ArgumentNullException(
                    nameof(openEditor));
        }

        public void Draw(ItemInjectionContext context)
        {
            if (context == null ||
                !context.IsHierarchyGameObject ||
                !(context.Target is GameObject gameObject))
            {
                return;
            }

            HandleAltTrigger(context, gameObject);

            var currentEvent = Event.current;
            if (currentEvent == null ||
                currentEvent.type != EventType.Repaint)
            {
                return;
            }

            var directStyle = _service.Get(
                _identity.Get(gameObject));
            var fallbackIcon =
                _iconApplier.ApplyConfigured(
                gameObject,
                directStyle);

            if (TryResolveBackgroundColor(
                    gameObject,
                    out var backgroundColor))
            {
                EditorGUI.DrawRect(
                    GetBackgroundRect(
                        context.SelectionRect,
                        EditorGUIUtility.currentViewWidth),
                    backgroundColor);
            }

            if (fallbackIcon != null)
            {
                GUI.DrawTexture(
                    GetIconRect(
                        context.SelectionRect),
                    fallbackIcon,
                    ScaleMode.ScaleToFit,
                    true);
            }
        }

        internal static Rect GetBackgroundRect(
            Rect selectionRect,
            float viewWidth)
        {
            return new Rect(
                RowLeft,
                selectionRect.y,
                Mathf.Max(0f, viewWidth - RowLeft),
                selectionRect.height);
        }

        internal static Rect GetIconRect(
            Rect selectionRect)
        {
            const float iconSize = 16f;
            return new Rect(
                selectionRect.x,
                selectionRect.y +
                (selectionRect.height - iconSize) * 0.5f,
                iconSize,
                iconSize);
        }

        private bool TryResolveBackgroundColor(
            GameObject gameObject,
            out Color color)
        {
            return HierarchyStyleInheritance
                .TryResolveBackgroundColor(
                    gameObject.transform,
                    transform => transform.parent,
                    transform => _service.Get(
                        _identity.Get(
                            transform.gameObject)),
                    out color);
        }

        private void HandleAltTrigger(
            ItemInjectionContext context,
            GameObject gameObject)
        {
            var currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            if (!_altTrigger.TryActivate(
                    context.InstanceId,
                    currentEvent.alt,
                    context.SelectionRect.Contains(
                        currentEvent.mousePosition)))
            {
                return;
            }

            var selectedIds = Selection.gameObjects
                .Where(selected =>
                    selected != null &&
                    selected.scene.IsValid())
                .Select(selected =>
                    selected.GetInstanceID())
                .ToArray();
            var targetIds =
                HierarchyStyleSelection.ResolveTargetIds(
                    gameObject.GetInstanceID(),
                    selectedIds);
            var targets = targetIds
                .Select(EditorUtility.InstanceIDToObject)
                .OfType<GameObject>()
                .Where(target =>
                    target.scene.IsValid())
                .ToArray();
            var anchor = GUIUtility.GUIToScreenPoint(
                new Vector2(
                    context.SelectionRect.xMax,
                    context.SelectionRect.y));

            EditorApplication.delayCall += () =>
                _openEditor(targets, anchor);
        }
    }
}
