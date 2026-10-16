using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ee4v.UI
{
    public enum AssetPickerAnimationFilter { All, Facial }

    /// <summary>Opens a project asset browser popup for dragging into existing object fields.</summary>
    public static class AssetPicker
    {
        public static UiButton CreateButton(Type objectType, Func<Object> getReference = null,
            Func<GameObject> getUsageContext = null, Func<AssetPickerAnimationFilter> getAnimationFilter = null)
            => CreateButton(() => objectType, getReference, getUsageContext, getAnimationFilter);

        public static UiButton CreateButton(Func<Type> getObjectType, Func<Object> getReference = null,
            Func<GameObject> getUsageContext = null, Func<AssetPickerAnimationFilter> getAnimationFilter = null)
        {
            if (getObjectType == null) throw new ArgumentNullException(nameof(getObjectType));
            UiButton button = null;
            button = new UiButton(UiLocalization.Get("ui.assetPicker.open"),
                () => Show(button, getObjectType(), getReference?.Invoke(), getUsageContext?.Invoke(),
                    getAnimationFilter?.Invoke() ?? AssetPickerAnimationFilter.All),
                icon: FluentUiIcons.CreateState("search.png", UiSizeTokens.Size14),
                variant: UiButtonVariant.Ghost);
            return button;
        }

        public static void Show(VisualElement anchor, Type objectType, Object reference = null,
            GameObject usageContext = null, AssetPickerAnimationFilter animationFilter = AssetPickerAnimationFilter.All)
        {
            if (objectType == null || !typeof(Object).IsAssignableFrom(objectType))
                throw new ArgumentException("A Unity Object type is required.", nameof(objectType));
            var referenceType = GetReferenceType(objectType);
            AssetPickerWindow.Show(anchor, objectType,
                reference != null && referenceType != null && referenceType.IsInstanceOfType(reference) && EditorUtility.IsPersistent(reference)
                    ? reference : null, usageContext, animationFilter);
        }

        public static void ShowProject(VisualElement anchor, Type objectType, string folderPath)
        {
            if (objectType == null || !typeof(Object).IsAssignableFrom(objectType))
                throw new ArgumentException("A Unity Object type is required.", nameof(objectType));
            if (!AssetDatabase.IsValidFolder(folderPath))
                throw new ArgumentException("An existing project folder is required.", nameof(folderPath));
            AssetPickerWindow.ShowProject(anchor, objectType, folderPath);
        }

        public static Type GetReferenceType(Type objectType) =>
            objectType == typeof(Object) || typeof(AnimationClip).IsAssignableFrom(objectType) ? null :
            typeof(Texture).IsAssignableFrom(objectType) ? typeof(Material) : objectType;

    }
}
