using System;
using System.Collections.Generic;
using Ee4v.Core.Injector;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyDecoration
{
    internal static class HierarchyDecorationRenderer
    {
        private const float DividerLeftInset = 32f;

        private static readonly List<Component> ComponentBuffer =
            new List<Component>();
        private static GUIStyle _dividerTextStyle;

        public static void Draw(ItemInjectionContext context)
        {
            if (context == null ||
                !context.IsHierarchyGameObject ||
                !(context.Target is GameObject gameObject) ||
                Event.current == null ||
                Event.current.type != EventType.Repaint ||
                !gameObject.name.StartsWith(
                    HierarchyDecoration.DividerTarget.NamePrefix,
                    StringComparison.Ordinal))
            {
                return;
            }

            ComponentBuffer.Clear();
            gameObject.GetComponents(ComponentBuffer);
            if (!HierarchyDecoration.DividerTarget.Matches(
                    gameObject.name,
                    ComponentBuffer,
                    gameObject.transform.childCount,
                    gameObject.transform.parent != null,
                    out var text))
            {
                return;
            }

            DrawDivider(
                context.SelectionRect,
                EditorGUIUtility.currentViewWidth,
                text);
        }

        private static void DrawDivider(
            Rect selectionRect,
            float viewWidth,
            string text)
        {
            var backgroundRect = new Rect(
                DividerLeftInset,
                selectionRect.y,
                Mathf.Max(0f, viewWidth - DividerLeftInset),
                selectionRect.height);
            DrawRect(
                backgroundRect,
                UiColorTokens.HierarchyDecorationBackground);

            if (string.IsNullOrWhiteSpace(text))
            {
                DrawRect(
                    GetHorizontalLine(backgroundRect),
                    UiColorTokens.HierarchyDecorationGuide);
                return;
            }

            var content = UiTextFactory.CreateGuiContent(text);
            var textStyle = GetDividerTextStyle();
            var textWidth = Mathf.Min(
                Mathf.Ceil(textStyle.CalcSize(content).x),
                Mathf.Max(
                    0f,
                    backgroundRect.width - UiSpacingTokens.Medium));
            var textRect = new Rect(
                backgroundRect.center.x - textWidth * 0.5f,
                backgroundRect.y,
                textWidth,
                backgroundRect.height);
            GetDividerLineRects(
                backgroundRect,
                textRect,
                out var leftLine,
                out var rightLine);
            DrawRect(
                leftLine,
                UiColorTokens.HierarchyDecorationGuide);
            DrawRect(
                rightLine,
                UiColorTokens.HierarchyDecorationGuide);
            GUI.Label(textRect, content, textStyle);
        }

        private static void GetDividerLineRects(
            Rect backgroundRect,
            Rect textRect,
            out Rect leftLine,
            out Rect rightLine)
        {
            var lineY = Mathf.Floor(
                backgroundRect.y +
                (backgroundRect.height - UiBorderTokens.Hairline) *
                0.5f);
            var leftEnd = Mathf.Clamp(
                textRect.x - UiSpacingTokens.Xs,
                backgroundRect.x,
                backgroundRect.xMax);
            var rightStart = Mathf.Clamp(
                textRect.xMax + UiSpacingTokens.Xs,
                backgroundRect.x,
                backgroundRect.xMax);
            leftLine = new Rect(
                backgroundRect.x,
                lineY,
                leftEnd - backgroundRect.x,
                UiBorderTokens.Hairline);
            rightLine = new Rect(
                rightStart,
                lineY,
                backgroundRect.xMax - rightStart,
                UiBorderTokens.Hairline);
        }

        private static Rect GetHorizontalLine(Rect rect)
        {
            return new Rect(
                rect.x,
                Mathf.Floor(
                    rect.y +
                    (rect.height - UiBorderTokens.Hairline) * 0.5f),
                rect.width,
                UiBorderTokens.Hairline);
        }

        private static GUIStyle GetDividerTextStyle()
        {
            if (_dividerTextStyle == null)
            {
                _dividerTextStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    fontSize = UiTypographyTokens.SmallFontSize,
                    richText = false,
                    wordWrap = false
                };
            }

            _dividerTextStyle.normal.textColor =
                UiColorTokens.HierarchyDecorationText;
            return _dividerTextStyle;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            if (rect.width > 0f && rect.height > 0f)
            {
                EditorGUI.DrawRect(rect, color);
            }
        }
    }
}
