using UnityEngine;

namespace _4OF.ee4v.Core.Utility {
    public static class TextureUtility {
        public static Texture2D FitImage(Texture2D source, int maxSize) {
            if (!source) return null;

            if (source.width <= maxSize && source.height <= maxSize) return source;

            var scale = Mathf.Min((float)maxSize / source.width, (float)maxSize / source.height);
            var newW = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            var newH = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            var rt = RenderTexture.GetTemporary(newW, newH, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
            var prevActive = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            var resized = new Texture2D(newW, newH, TextureFormat.RGBA32, false, false);
            resized.ReadPixels(new Rect(0, 0, newW, newH), 0, 0);
            resized.Apply();

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            return resized;
        }
    }
}
