using System;
using UnityEngine;

namespace Ee4v.UI
{
    public sealed class PreviewGridBackground : IDisposable
    {
        private const int GridTextureSize = 64;
        private const int GridCellSize = 16;
        private readonly bool _lightBackground;
        private Texture2D _gridTexture;

        public PreviewGridBackground(bool lightBackground)
        {
            _lightBackground = lightBackground;
        }

        public void Draw(Rect rect)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureGridTexture();
            if (_gridTexture == null)
            {
                return;
            }

            GUI.DrawTextureWithTexCoords(
                rect,
                _gridTexture,
                new Rect(
                    0f,
                    0f,
                    rect.width / GridTextureSize,
                    rect.height / GridTextureSize),
                false);
        }

        private void EnsureGridTexture()
        {
            if (_gridTexture != null)
            {
                return;
            }

            var baseColor = _lightBackground
                ? new Color32(96, 100, 111, 255)
                : new Color32(31, 33, 36, 255);
            var minorColor = _lightBackground
                ? new Color32(109, 113, 123, 255)
                : new Color32(43, 46, 51, 255);
            var majorColor = _lightBackground
                ? new Color32(136, 139, 150, 255)
                : new Color32(61, 65, 72, 255);
            var pixels = new Color32[GridTextureSize * GridTextureSize];
            for (var y = 0; y < GridTextureSize; y++)
            {
                for (var x = 0; x < GridTextureSize; x++)
                {
                    var major = x == 0 || y == 0;
                    var minor = x % GridCellSize == 0 ||
                                y % GridCellSize == 0;
                    pixels[(y * GridTextureSize) + x] = major
                        ? majorColor
                        : minor
                            ? minorColor
                            : baseColor;
                }
            }

            _gridTexture = new Texture2D(
                GridTextureSize,
                GridTextureSize,
                TextureFormat.RGBA32,
                false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            _gridTexture.SetPixels32(pixels);
            _gridTexture.Apply(false, true);
        }

        public void Dispose()
        {
            if (_gridTexture == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(_gridTexture);
            _gridTexture = null;
        }
    }
}
