using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum UiIconSourceKind
    {
        None,
        Texture,
        Builtin
    }

    internal enum UiBuiltinIcon
    {
        Search
    }

    internal static class UiBuiltinIconResolver
    {
        public static bool TryResolve(UiBuiltinIcon icon, out Texture texture)
        {
            var iconName = GetIconName(icon);
            texture = EditorGUIUtility.FindTexture(iconName);
            if (texture != null)
            {
                return true;
            }

            var content = EditorGUIUtility.IconContent(iconName);
            texture = content != null ? content.image : null;
            return texture != null;
        }

        internal static string GetIconName(UiBuiltinIcon icon)
        {
            switch (icon)
            {
                case UiBuiltinIcon.Search:
                    return "Search Icon";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(icon), icon, null);
            }
        }
    }

    internal sealed class IconState
    {
        public IconState(
            UiIconSourceKind sourceKind,
            Texture texture = null,
            UiBuiltinIcon builtinIcon = UiBuiltinIcon.Search,
            float size = 16f,
            string tooltip = null)
        {
            SourceKind = sourceKind;
            Texture = texture;
            BuiltinIcon = builtinIcon;
            Size = Mathf.Max(0f, size);
            Tooltip = tooltip ?? string.Empty;
        }

        public UiIconSourceKind SourceKind { get; }

        public Texture Texture { get; }

        public UiBuiltinIcon BuiltinIcon { get; }

        public float Size { get; }

        public string Tooltip { get; }

        public static IconState Empty(float size = 16f, string tooltip = null)
        {
            return new IconState(UiIconSourceKind.None, size: size, tooltip: tooltip);
        }

        public static IconState FromTexture(Texture texture, float size = 16f, string tooltip = null)
        {
            return new IconState(UiIconSourceKind.Texture, texture, size: size, tooltip: tooltip);
        }

        public static IconState FromBuiltinIcon(UiBuiltinIcon builtinIcon, float size = 16f, string tooltip = null)
        {
            return new IconState(UiIconSourceKind.Builtin, builtinIcon: builtinIcon, size: size, tooltip: tooltip);
        }
    }

    internal sealed class Icon : VisualElement
    {
        private readonly Image _image;

        public Icon(IconState state = null)
        {
            AddToClassList(UiClassNames.Icon);

            _image = new Image
            {
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit
            };
            _image.AddToClassList(UiClassNames.IconImage);
            Add(_image);

            SetState(state ?? IconState.Empty());
        }

        public void SetState(IconState state)
        {
            state = state ?? IconState.Empty();

            var texture = ResolveTexture(state);
            var hasTexture = texture != null;
            var size = state.Size;

            tooltip = state.Tooltip;
            style.width = size;
            style.height = size;
            style.display = hasTexture ? DisplayStyle.Flex : DisplayStyle.None;

            _image.image = texture;
            _image.style.width = size;
            _image.style.height = size;
        }

        private static Texture ResolveTexture(IconState state)
        {
            switch (state.SourceKind)
            {
                case UiIconSourceKind.Texture:
                    return state.Texture;
                case UiIconSourceKind.Builtin:
                    return UiBuiltinIconResolver.TryResolve(state.BuiltinIcon, out var texture) ? texture : null;
                default:
                    return null;
            }
        }
    }
}
