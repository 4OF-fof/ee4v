using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class AnimationClipThumbnailCache
    {
        private static readonly Color EmptyColor =
            new Color(0.1f, 0.1f, 0.1f, 1f);
        private readonly Dictionary<AnimationClip, Entry> _entries =
            new Dictionary<AnimationClip, Entry>();

        internal void Draw(
            AnimationClip clip,
            Rect rect,
            FaceExpressionPreview preview,
            GameObject avatar,
            IReadOnlyList<string> rendererPaths,
            Action restorePreview = null)
        {
            if (clip == null || preview == null || avatar == null)
            {
                EditorGUI.DrawRect(rect, EmptyColor);
                return;
            }

            var dirtyCount = EditorUtility.GetDirtyCount(clip);
            if (!_entries.TryGetValue(clip, out var entry) ||
                entry.Texture == null ||
                entry.DirtyCount != dirtyCount)
            {
                Destroy(entry.Texture);
                Texture2D texture;
                try
                {
                    var channels = FaceExpressionClipEditor.Read(
                        avatar,
                        clip,
                        Array.Empty<string>(),
                        rendererPaths);
                    texture = preview.RenderThumbnail(
                        channels,
                        160,
                        160);
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
                _entries[clip] = entry;
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

        internal void Clear()
        {
            foreach (var entry in _entries.Values)
            {
                Destroy(entry.Texture);
            }

            _entries.Clear();
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
