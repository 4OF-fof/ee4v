using UnityEngine;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        internal static Texture2D CreateItemCardSampleThumbnail(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var horizontal = (byte)Mathf.RoundToInt(Mathf.Lerp(72f, 42f, (float)x / Mathf.Max(1, width - 1)));
                    var vertical = (byte)Mathf.RoundToInt(Mathf.Lerp(56f, 92f, (float)y / Mathf.Max(1, height - 1)));
                    var accent = ((x / 16) + (y / 16)) % 2 == 0 ? (byte)125 : (byte)95;
                    pixels[(y * width) + x] = new Color32(horizontal, vertical, accent, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
