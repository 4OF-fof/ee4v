using System.Collections.Generic;
using System.Globalization;
using _4OF.ee4v.Core.i18n;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.Core.UI {
    public abstract class ColorPreset {
        public static Color DefaultBackground = FromHex(EditorGUIUtility.isProSkin ? "#383838" : "#c8c8c8");
        public static Color ProjectBackground = FromHex(EditorGUIUtility.isProSkin ? "#333333" : "#bdbdbd");
        public static Color MouseOverBackground = FromHex(EditorGUIUtility.isProSkin ? "#444444" : "#b2b2b2");
        public static Color ActiveBackground = FromHex(EditorGUIUtility.isProSkin ? "#575757" : "#eeeeee");
        public static Color TextColor = FromHex(EditorGUIUtility.isProSkin ? "#cccccc" : "#000000");
        public static Color DepthLine = FromHex(EditorGUIUtility.isProSkin ? "#686868" : "#8e8e8e");

        public static Color InActiveItem = FromHex("#7f7f7f", EditorGUIUtility.isProSkin ? 1f : 0.3f);
        public static Color WarningButton = FromHex(EditorGUIUtility.isProSkin ? "#b71c1c" : "#ff5252");
        public static Color IconBorder = Color.black;

        public static Color WindowHeader = FromHex(EditorGUIUtility.isProSkin ? "#282828" : "#a5a5a5");
        public static Color WindowBorder = FromHex(EditorGUIUtility.isProSkin ? "#191919" : "#8a8a8a");

        public static Color ItemSelectedBorder = FromHex("#3f7fff");
        public static Color ItemSelectedBackGround = FromHex("#3f7fff", 0.3f);

        public static StyleColor TabBackground = new(DefaultBackground);
        public static StyleColor AddButtonHover = new(MouseOverBackground);
        public static StyleColor TabHoveredBackground = new(MouseOverBackground);
        public static StyleColor TabSelectedBackground = new(ActiveBackground);
        public static StyleColor TabBorder = new(WindowBorder);
        public static StyleColor TabText = new(TextColor);
        public static StyleColor DropFolderArea = new(FromHex("#334c7f", 0.3f));
        public static StyleColor TabCloseButtonHover = new(FromHex("#e53333", 0.8f));

        private static readonly Dictionary<int, Texture2D> AlphaGradientCache = new();

        private static Color FromHex(string hex, float alpha = 1f) {
            if (string.IsNullOrEmpty(hex)) return Color.white;

            if (hex[0] == '#') hex = hex[1..];

            switch (hex.Length) {
                case 6 when uint.TryParse(hex, NumberStyles.HexNumber, null, out var hexVal): {
                    var r = ((hexVal >> 16) & 0xFF) / 255f;
                    var g = ((hexVal >> 8)  & 0xFF) / 255f;
                    var b = (hexVal         & 0xFF) / 255f;
                    return new Color(r, g, b, Mathf.Clamp01(alpha));
                }
                case 3: {
                    var rC = hex[0];
                    var gC = hex[1];
                    var bC = hex[2];
                    var rr = new string(rC, 2);
                    var gg = new string(gC, 2);
                    var bb = new string(bC, 2);
                    var expanded = rr + gg + bb;
                    if (uint.TryParse(expanded, NumberStyles.HexNumber, null, out var hexVal)) {
                        var r = ((hexVal >> 16) & 0xFF) / 255f;
                        var g = ((hexVal >> 8)  & 0xFF) / 255f;
                        var b = (hexVal         & 0xFF) / 255f;
                        return new Color(r, g, b, Mathf.Clamp01(alpha));
                    }

                    break;
                }
            }

            Debug.LogError(I18N.Get("Debug.Core.InvalidHex", hex));
            return Color.white;
        }

        public static void DrawGradient(Rect rect, Color leftColor, Color rightColor) {
            if (rect.width <= 0 || rect.height <= 0) return;

            var w = Mathf.Max(1, Mathf.CeilToInt(rect.width));
            var h = Mathf.Max(1, Mathf.CeilToInt(rect.height));

            var key = w;
            key = (key * 397) ^ h;
            key = (key * 397) ^ leftColor.GetHashCode();
            key = (key * 397) ^ rightColor.GetHashCode();

            if (!AlphaGradientCache.TryGetValue(key, out var tex) || tex == null) {
                tex = GenerateGradientTexture(w, h, leftColor, rightColor);
                AlphaGradientCache[key] = tex;
            }

            var prev = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        private static Texture2D GenerateGradientTexture(int width, int height, Color leftColor, Color rightColor) {
            var tex = new Texture2D(width, height, TextureFormat.ARGB32, false) {
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[width * height];
            for (var x = 0; x < width; x++) {
                var t = width == 1 ? 0f : x / (float)(width - 1);
                var a = Mathf.Lerp(leftColor.a, rightColor.a, t);
                var col = Color.Lerp(leftColor, rightColor, t);
                col.a = a;
                for (var y = 0; y < height; y++) pixels[y * width + x] = col;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}