using System;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class ProtectedAssetInspectorView :
        VisualElement
    {
        private const string RootClassName =
            "ee4v-asset-manager-protected-inspector";
        private const string ContentClassName =
            RootClassName + "__content";
        private const string MessageClassName =
            RootClassName + "__message";
        private const string ActionsClassName =
            RootClassName + "__actions";

        internal ProtectedAssetInspectorView(
            Action dismissWarning,
            Action unprotect)
        {
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
                    "assetManager.protection.inspector.message"),
                UiClassNames.SecondaryText,
                MessageClassName));

            var actions = new VisualElement();
            actions.AddToClassList(ActionsClassName);
            content.Add(actions);

            actions.Add(new UiButton(
                new UiButtonState(
                    I18N.Get(
                        "assetManager.protection.inspector.dismissWarning")),
                dismissWarning));

            actions.Add(new UiButton(
                new UiButtonState(
                    I18N.Get(
                        "assetManager.protection.inspector.unprotect"),
                    variant: UiButtonVariant.Ghost),
                unprotect));
        }
    }
}
