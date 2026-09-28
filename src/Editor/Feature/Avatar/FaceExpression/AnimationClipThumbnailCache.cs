using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class AnimationClipThumbnailCache
    {
        private const int MinimumRenderSize = 64;
        private const int MaximumRenderSize = 512;
        private const int RenderSizeStep = 64;
        private static readonly Color EmptyColor =
            new Color(0.1f, 0.1f, 0.1f, 1f);
        private readonly Dictionary<AnimationClip, Dictionary<int, Entry>>
            _entries =
                new Dictionary<AnimationClip, Dictionary<int, Entry>>();

        internal void Draw(
            AnimationClip clip,
            Rect rect,
            FaceExpressionPreview preview,
            GameObject avatar,
            IReadOnlyList<string> rendererPaths,
            Action restorePreview = null)
        {
            DrawAtTime(
                clip,
                0f,
                rect,
                preview,
                avatar,
                rendererPaths,
                restorePreview);
        }

        internal void DrawAtTime(
            AnimationClip clip,
            float time,
            Rect rect,
            FaceExpressionPreview preview,
            GameObject avatar,
            IReadOnlyList<string> rendererPaths,
            Action restorePreview = null,
            bool refreshWhenDirty = true)
        {
            if (rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            if (clip == null || preview == null || avatar == null)
            {
                EditorGUI.DrawRect(rect, EmptyColor);
                return;
            }

            var renderSize = GetRenderSize(rect);
            var frameRate = clip.frameRate <= 0f ? 60f : clip.frameRate;
            var frame = Mathf.RoundToInt(Mathf.Max(0f, time) * frameRate);
            var dirtyCount = EditorUtility.GetDirtyCount(clip);
            if (!_entries.TryGetValue(clip, out var clipEntries))
            {
                clipEntries = new Dictionary<int, Entry>();
                _entries.Add(clip, clipEntries);
            }

            if (!clipEntries.TryGetValue(frame, out var entry) ||
                entry.Texture == null ||
                entry.Texture.width < renderSize ||
                (refreshWhenDirty && entry.DirtyCount != dirtyCount))
            {
                Destroy(entry.Texture);
                Texture2D texture;
                try
                {
                    var channels = FaceExpressionClipEditor.Read(
                        avatar,
                        clip,
                        Array.Empty<string>(),
                        rendererPaths,
                        frame / frameRate);
                    texture = preview.RenderThumbnail(
                        channels,
                        renderSize,
                        renderSize);
                }
                finally
                {
                    restorePreview?.Invoke();
                }

                if (texture != null)
                {
                    texture.hideFlags = HideFlags.HideAndDontSave;
                }

                entry = new Entry(texture, dirtyCount);
                clipEntries[frame] = entry;
            }

            if (entry.Texture == null)
            {
                EditorGUI.DrawRect(rect, EmptyColor);
                return;
            }

            GUI.DrawTexture(
                rect,
                entry.Texture,
                ScaleMode.ScaleAndCrop,
                false);
        }

        private static int GetRenderSize(Rect rect)
        {
            var pixelScale = Mathf.Max(
                2f, EditorGUIUtility.pixelsPerPoint);
            var pixelSize = Mathf.Max(rect.width, rect.height) *
                pixelScale;
            var rounded = Mathf.CeilToInt(pixelSize / RenderSizeStep) *
                RenderSizeStep;
            return Mathf.Clamp(
                rounded,
                MinimumRenderSize,
                MaximumRenderSize);
        }

        internal void Clear()
        {
            foreach (var clipEntries in _entries.Values)
            {
                foreach (var entry in clipEntries.Values)
                {
                    Destroy(entry.Texture);
                }
            }

            _entries.Clear();
        }

        internal void Invalidate(AnimationClip clip)
        {
            if (clip == null || !_entries.TryGetValue(clip, out var clipEntries))
            {
                return;
            }

            foreach (var entry in clipEntries.Values)
            {
                Destroy(entry.Texture);
            }

            _entries.Remove(clip);
        }

        private static void Destroy(Texture2D texture)
        {
            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private readonly struct Entry
        {
            internal Entry(Texture2D texture, int dirtyCount)
            {
                Texture = texture;
                DirtyCount = dirtyCount;
            }

            internal Texture2D Texture { get; }
            internal int DirtyCount { get; }
        }
    }
}
