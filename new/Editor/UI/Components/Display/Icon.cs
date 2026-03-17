using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum UiIconSourceKind
    {
        Texture,
        Builtin
    }

    internal enum UiBuiltinIcon
    {
        Search,
        DisclosureClosed,
        DisclosureOpen
    }

    internal static class UiBuiltinIconResolver
    {
        public static bool TryResolve(UiBuiltinIcon icon, out Texture texture)
        {
            var iconNames = GetIconNames(icon);
            for (var i = 0; i < iconNames.Length; i++)
            {
                var iconName = iconNames[i];
                texture = EditorGUIUtility.FindTexture(iconName);
                if (texture != null)
                {
                    return true;
                }

                var content = EditorGUIUtility.IconContent(iconName);
                texture = content != null ? content.image : null;
                if (texture != null)
                {
                    return true;
                }
            }

            texture = null;
            return false;
        }

        internal static string GetIconName(UiBuiltinIcon icon)
        {
            return GetIconNames(icon)[0];
        }

        private static string[] GetIconNames(UiBuiltinIcon icon)
        {
            switch (icon)
            {
                case UiBuiltinIcon.Search:
                    return new[] { "Search Icon" };
                case UiBuiltinIcon.DisclosureClosed:
                    return new[]
                    {
                        "IN foldout",
                        "d_IN_foldout",
                        "Foldout",
                        "d_Foldout"
                    };
                case UiBuiltinIcon.DisclosureOpen:
                    return new[]
                    {
                        "IN foldout on",
                        "d_IN_foldout on",
                        "Foldout On",
                        "d_Foldout On"
                    };
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
            if (sourceKind == UiIconSourceKind.Texture && texture == null)
            {
                throw new System.ArgumentNullException(nameof(texture), "Texture source requires a texture.");
            }

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

            SetState(state ?? IconState.FromBuiltinIcon(UiBuiltinIcon.Search));
        }

        public void SetState(IconState state)
        {
            state = state ?? IconState.FromBuiltinIcon(UiBuiltinIcon.Search);

            var texture = ResolveTexture(state);
            var size = state.Size;

            tooltip = state.Tooltip;
            style.width = size;
            style.height = size;
            style.display = DisplayStyle.Flex;

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
                    throw new System.ArgumentOutOfRangeException(nameof(state.SourceKind), state.SourceKind, null);
            }
        }
    }
}
