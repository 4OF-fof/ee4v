using System;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class ProtectedAssetInspectorViewState
    {
        internal string AssetName { get; set; } =
            string.Empty;
        internal string AssetPath { get; set; } =
            string.Empty;
        internal bool CanCreateMaterialVariant { get; set; }
        internal bool CanCreatePrefabVariant { get; set; }
        internal bool CanCreateEditableCopy { get; set; }
    }

    internal sealed class ProtectedAssetInspectorView :
        VisualElement
    {
        private const string RootClassName =
            "ee4v-asset-manager-protected-inspector";
        private const string ContentClassName =
            RootClassName + "__content";
        private const string TitleClassName =
            RootClassName + "__title";
        private const string MessageClassName =
            RootClassName + "__message";
        private const string AssetClassName =
            RootClassName + "__asset";
        private const string PathClassName =
            RootClassName + "__path";
        private const string ActionsClassName =
            RootClassName + "__actions";

        internal ProtectedAssetInspectorView(
            ProtectedAssetInspectorViewState state,
            Action createMaterialVariant,
            Action createPrefabVariant,
            Action createEditableCopy,
            Action unprotect)
        {
            state = state ??
                new ProtectedAssetInspectorViewState();
            AddToClassList(RootClassName);
            AddToClassList("ee4v-ui");
            UiStyleUtility.AddPackageStyleSheet(
                this,
                "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                this,
                "Editor/UI/Components/Inputs/Button/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(
                this,
                "Editor/AssetManager/UI/Inspector/protected-asset-inspector.uss");

            var content = new VisualElement();
            content.AddToClassList(ContentClassName);
            Add(content);

            content.Add(UiTextFactory.Create(
                I18N.Get(
                    "assetManager.protection.inspector.title"),
                UiClassNames.WindowTitle,
                TitleClassName));
            content.Add(UiTextFactory.Create(
                I18N.Get(
                    "assetManager.protection.inspector.message"),
                UiClassNames.SecondaryText,
                MessageClassName));
            content.Add(UiTextFactory.Create(
                I18N.Get(
                    "assetManager.protection.inspector.asset",
                    state.AssetName),
                UiClassNames.SectionTitle,
                AssetClassName));
            content.Add(UiTextFactory.Create(
                state.AssetPath,
                UiClassNames.SecondaryText,
                PathClassName));

            var actions = new VisualElement();
            actions.AddToClassList(ActionsClassName);
            content.Add(actions);

            if (state.CanCreateMaterialVariant)
            {
                actions.Add(CreateButton(
                    I18N.Get(
                        "assetManager.protection.inspector.createMaterialVariant"),
                    createMaterialVariant));
            }

            if (state.CanCreatePrefabVariant)
            {
                actions.Add(CreateButton(
                    I18N.Get(
                        "assetManager.protection.inspector.createPrefabVariant"),
                    createPrefabVariant));
            }

            if (state.CanCreateEditableCopy)
            {
                actions.Add(CreateButton(
                    I18N.Get(
                        "assetManager.protection.inspector.createEditableCopy"),
                    createEditableCopy));
            }

            actions.Add(new UiButton(
                new UiButtonState(
                    I18N.Get(
                        "assetManager.protection.inspector.unprotect"),
                    variant: UiButtonVariant.Ghost),
                unprotect));
        }

        private static UiButton CreateButton(
            string label,
            Action onClick)
        {
            return new UiButton(
                new UiButtonState(
                    label),
                onClick);
        }
    }
}
