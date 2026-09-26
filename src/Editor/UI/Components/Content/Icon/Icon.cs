using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public static class FluentUiIcons
    {
        internal const string IconDirectory =
            "/Editor/ThirdParty/FluentUiSystemIcons/Png512/";

        public static Texture2D LoadTexture(string iconFileName)
        {
            var packageRoot = PackageAssetApi.GetPackageRootAssetPath();
            return string.IsNullOrEmpty(packageRoot) ||
                   string.IsNullOrWhiteSpace(iconFileName)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(
                    packageRoot + IconDirectory + iconFileName);
        }

        public static IconState CreateState(
            string iconFileName,
            float size = UiSizeTokens.Size16,
            string tooltip = null,
            Color? tintColor = null)
        {
            if (size <= UiSizeTokens.Size12)
            {
                switch (iconFileName)
                {
                    case "add.png":
                        iconFileName = "add_12.png";
                        break;
                    case "subtract.png":
                        iconFileName = "subtract_12.png";
                        break;
                }
            }

            var texture = LoadTexture(iconFileName);
            return texture == null
                ? null
                : IconState.FromTexture(
                    texture,
                    size,
                    tooltip,
                    tintColor ?? UiColorTokens.TextPrimary);
        }
    }

    public enum UiIconSourceKind
    {
        Texture,
        Builtin
    }

    public enum UiBuiltinIcon
    {
        Folder,
        Scene,
        GameObject,
        ModelFile,
        VisibilityHidden
    }

    public static class UiBuiltinIconResolver
    {
        public static Texture LoadTexture(UiBuiltinIcon icon)
        {
            return TryResolve(icon, out var texture) ? texture : null;
        }

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
                case UiBuiltinIcon.Folder:
                    return new[]
                    {
                        "Folder Icon",
                        "d_Folder Icon"
                    };
                case UiBuiltinIcon.Scene:
                    return new[]
                    {
                        "SceneAsset Icon",
                        "d_SceneAsset Icon"
                    };
                case UiBuiltinIcon.GameObject:
                    return new[]
                    {
                        "GameObject Icon",
                        "d_GameObject Icon"
                    };
                case UiBuiltinIcon.ModelFile:
                    return new[] { "Prefab Icon", "d_Prefab Icon", "Mesh Icon", "d_Mesh Icon", "DefaultAsset Icon" };
                case UiBuiltinIcon.VisibilityHidden:
                    return new[]
                    {
                        "scenevis_hidden_hover",
                        "scenevis_hidden",
                        "d_scenevis_hidden_hover",
                        "d_scenevis_hidden",
                        "animationvisibilitytoggleoff"
                    };
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(icon), icon, null);
            }
        }
    }

    public sealed class IconState
    {
        public IconState(
            UiIconSourceKind sourceKind,
            Texture texture = null,
            UiBuiltinIcon builtinIcon = UiBuiltinIcon.Folder,
            float size = 16f,
            string tooltip = null,
            Color? tintColor = null)
        {
            if (sourceKind == UiIconSourceKind.Texture && texture == null)
            {
                throw new System.ArgumentNullException(nameof(texture), "Texture source requires a texture.");
            }

            SourceKind = sourceKind;
            Texture = texture;
            BuiltinIcon = builtinIcon;
            Size = size < 0f ? 0f : size;
            Tooltip = tooltip ?? string.Empty;
            TintColor = tintColor;
        }

        public UiIconSourceKind SourceKind { get; }

        public Texture Texture { get; }

        public UiBuiltinIcon BuiltinIcon { get; }

        public float Size { get; }

        public string Tooltip { get; }

        public Color? TintColor { get; }

        public static IconState FromTexture(
            Texture texture,
            float size = 16f,
            string tooltip = null,
            Color? tintColor = null)
        {
            return new IconState(
                UiIconSourceKind.Texture,
                texture,
                size: size,
                tooltip: tooltip,
                tintColor: tintColor);
        }

        public static IconState FromBuiltinIcon(
            UiBuiltinIcon builtinIcon,
            float size = UiSizeTokens.Size16,
            string tooltip = null)
        {
            return new IconState(UiIconSourceKind.Builtin, builtinIcon: builtinIcon, size: size, tooltip: tooltip);
        }

    }

    public sealed class Icon : VisualElement
    {
        private const string RootClassName = "ee4v-ui-icon";
        private const string ImageClassName = "ee4v-ui-icon__image";
        private readonly Image _image;

        public Icon(IconState state = null)
        {
            AddToClassList(RootClassName);
            pickingMode = PickingMode.Ignore;

            _image = new Image
            {
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit
            };
            _image.AddToClassList(ImageClassName);
            Add(_image);

            SetState(state ?? CreateDefaultState());
        }

        public void SetState(IconState state)
        {
            state = state ?? CreateDefaultState();
            if (state == null)
            {
                _image.image = null;
                tooltip = string.Empty;
                style.display = DisplayStyle.None;
                return;
            }

            var size = state.Size;

            tooltip = state.Tooltip;
            style.width = size;
            style.height = size;
            style.display = DisplayStyle.Flex;

            ApplySource(state);
            SetSize(size);
        }

        public void SetSize(float size)
        {
            var safeSize = Mathf.Max(0f, size);
            style.width = safeSize;
            style.height = safeSize;
            _image.style.width = safeSize;
            _image.style.height = safeSize;
        }

        private void ApplySource(IconState state)
        {
            _image.image = null;
            _image.tintColor = state.TintColor ?? Color.white;

            switch (state.SourceKind)
            {
                case UiIconSourceKind.Texture:
                    _image.image = state.Texture;
                    return;
                case UiIconSourceKind.Builtin:
                    if (UiBuiltinIconResolver.TryResolve(
                            state.BuiltinIcon,
                            out var texture))
                    {
                        _image.image = texture;
                    }

                    return;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(state.SourceKind), state.SourceKind, null);
            }
        }

        private static IconState CreateDefaultState()
        {
            return FluentUiIcons.CreateState("search.png");
        }
    }
}
