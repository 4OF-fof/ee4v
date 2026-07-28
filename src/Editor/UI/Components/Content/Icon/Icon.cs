using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum UiIconSourceKind
    {
        Texture,
        Builtin,
        Fluent
    }

    internal enum UiBuiltinIcon
    {
        Search,
        Filter,
        Sort,
        Close,
        Add,
        Refresh,
        Back,
        Forward,
        Pin,
        Star,
        Home,
        Assets,
        Package,
        Grid,
        Store,
        Folder,
        FolderEmpty,
        Uncategorized,
        Tag,
        SmartCollection,
        DisclosureClosed,
        DisclosureOpen,
        GenericFile,
        ArchiveFile,
        ImageFile,
        TextFile,
        UnityFile,
        ModelFile,
        AudioFile,
        ScriptFile,
        VisibilityHidden
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
                case UiBuiltinIcon.Filter:
                    return new[]
                    {
                        "FilterByType",
                        "d_FilterByType",
                        "Search Icon"
                    };
                case UiBuiltinIcon.Sort:
                    return new[]
                    {
                        "AlphabeticalSorting",
                        "d_AlphabeticalSorting",
                        "Search Icon"
                    };
                case UiBuiltinIcon.Close:
                    return new[]
                    {
                        "CrossIcon",
                        "d_CrossIcon",
                        "winbtn_win_close",
                        "d_winbtn_win_close"
                    };
                case UiBuiltinIcon.Add:
                    return new[]
                    {
                        "Toolbar Plus",
                        "d_Toolbar Plus",
                        "CreateAddNew",
                        "d_CreateAddNew"
                    };
                case UiBuiltinIcon.Refresh:
                    return new[]
                    {
                        "Refresh",
                        "d_Refresh",
                        "TreeEditor.Refresh"
                    };
                case UiBuiltinIcon.Back:
                    return new[]
                    {
                        "tab_prev",
                        "d_tab_prev",
                        "Animation.PrevKey",
                        "d_Animation.PrevKey"
                    };
                case UiBuiltinIcon.Forward:
                    return new[]
                    {
                        "tab_next",
                        "d_tab_next",
                        "Animation.NextKey",
                        "d_Animation.NextKey"
                    };
                case UiBuiltinIcon.Pin:
                    return new[]
                    {
                        "Pinned",
                        "d_Pinned",
                        "Favorite",
                        "d_Favorite",
                        "Favorite Icon",
                        "d_Favorite Icon"
                    };
                case UiBuiltinIcon.Star:
                    return new[]
                    {
                        "Favorite",
                        "d_Favorite",
                        "Favorite Icon",
                        "d_Favorite Icon"
                    };
                case UiBuiltinIcon.Home:
                    return new[]
                    {
                        "Folder Icon",
                        "d_Folder Icon"
                    };
                case UiBuiltinIcon.Assets:
                    return new[]
                    {
                        "Project",
                        "d_Project",
                        "DefaultAsset Icon",
                        "d_DefaultAsset Icon"
                    };
                case UiBuiltinIcon.Package:
                    return new[]
                    {
                        "Package Manager",
                        "d_Package Manager",
                        "PackageManager",
                        "d_PackageManager",
                        "DefaultAsset Icon",
                        "d_DefaultAsset Icon"
                    };
                case UiBuiltinIcon.Grid:
                    return new[]
                    {
                        "Grid.BoxTool",
                        "d_Grid.BoxTool",
                        "GridLayoutGroup Icon",
                        "d_GridLayoutGroup Icon",
                        "Project",
                        "d_Project"
                    };
                case UiBuiltinIcon.Store:
                    return new[]
                    {
                        "Asset Store",
                        "d_Asset Store",
                        "Package Manager",
                        "d_Package Manager"
                    };
                case UiBuiltinIcon.Folder:
                    return new[]
                    {
                        "Folder Icon",
                        "d_Folder Icon"
                    };
                case UiBuiltinIcon.FolderEmpty:
                    return new[]
                    {
                        "FolderEmpty Icon",
                        "d_FolderEmpty Icon",
                        "Folder Icon",
                        "d_Folder Icon"
                    };
                case UiBuiltinIcon.Uncategorized:
                    return new[]
                    {
                        "UnLinked",
                        "d_UnLinked",
                        "DefaultAsset Icon",
                        "d_DefaultAsset Icon"
                    };
                case UiBuiltinIcon.Tag:
                    return new[]
                    {
                        "FilterByLabel",
                        "d_FilterByLabel",
                        "FilterByType",
                        "d_FilterByType"
                    };
                case UiBuiltinIcon.SmartCollection:
                    return new[]
                    {
                        "Search Icon",
                        "d_Search Icon",
                        "FilterByType",
                        "d_FilterByType"
                    };
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
                case UiBuiltinIcon.GenericFile:
                    return new[] { "DefaultAsset Icon", "d_DefaultAsset Icon" };
                case UiBuiltinIcon.ArchiveFile:
                    return new[] { "Package Manager", "d_Package Manager", "DefaultAsset Icon", "d_DefaultAsset Icon" };
                case UiBuiltinIcon.ImageFile:
                    return new[] { "Texture Icon", "d_Texture Icon", "RawImage Icon", "d_RawImage Icon", "DefaultAsset Icon" };
                case UiBuiltinIcon.TextFile:
                    return new[] { "TextAsset Icon", "d_TextAsset Icon", "TextScriptImporter Icon", "d_TextScriptImporter Icon", "DefaultAsset Icon" };
                case UiBuiltinIcon.UnityFile:
                    return new[] { "UnityLogo", "d_UnityLogo", "SceneAsset Icon", "d_SceneAsset Icon", "DefaultAsset Icon" };
                case UiBuiltinIcon.ModelFile:
                    return new[] { "Prefab Icon", "d_Prefab Icon", "Mesh Icon", "d_Mesh Icon", "DefaultAsset Icon" };
                case UiBuiltinIcon.AudioFile:
                    return new[] { "AudioClip Icon", "d_AudioClip Icon", "DefaultAsset Icon" };
                case UiBuiltinIcon.ScriptFile:
                    return new[] { "cs Script Icon", "d_cs Script Icon", "TextAsset Icon", "d_TextAsset Icon", "DefaultAsset Icon" };
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

    internal sealed class IconState
    {
        public IconState(
            UiIconSourceKind sourceKind,
            Texture texture = null,
            UiBuiltinIcon builtinIcon = UiBuiltinIcon.Search,
            UiFluentIcon fluentIcon = UiFluentIcon.Search,
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
            FluentIcon = fluentIcon;
            Size = size < 0f ? 0f : size;
            Tooltip = tooltip ?? string.Empty;
        }

        public UiIconSourceKind SourceKind { get; }

        public Texture Texture { get; }

        public UiBuiltinIcon BuiltinIcon { get; }

        public UiFluentIcon FluentIcon { get; }

        public float Size { get; }

        public string Tooltip { get; }

        public static IconState FromTexture(Texture texture, float size = 16f, string tooltip = null)
        {
            return new IconState(UiIconSourceKind.Texture, texture, size: size, tooltip: tooltip);
        }

        public static IconState FromBuiltinIcon(
            UiBuiltinIcon builtinIcon,
            float size = UiSizeTokens.Size16,
            string tooltip = null)
        {
            return new IconState(UiIconSourceKind.Builtin, builtinIcon: builtinIcon, size: size, tooltip: tooltip);
        }

        public static IconState FromFluentIcon(
            UiFluentIcon fluentIcon,
            float size = UiSizeTokens.Size16,
            string tooltip = null)
        {
            return new IconState(
                UiIconSourceKind.Fluent,
                fluentIcon: fluentIcon,
                size: size,
                tooltip: tooltip);
        }

    }

    internal sealed class Icon : VisualElement
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

            SetState(state ?? IconState.FromBuiltinIcon(UiBuiltinIcon.Search));
        }

        public void SetState(IconState state)
        {
            state = state ?? IconState.FromBuiltinIcon(UiBuiltinIcon.Search);

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
            _image.tintColor = Color.white;

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
                case UiIconSourceKind.Fluent:
                    if (UiFluentIconResolver.TryResolve(
                            state.FluentIcon,
                            out var fluentTexture))
                    {
                        _image.image = fluentTexture;
                        _image.tintColor =
                            UiColorTokens.TextPrimary;
                        return;
                    }

                    return;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(state.SourceKind), state.SourceKind, null);
            }
        }
    }
}
