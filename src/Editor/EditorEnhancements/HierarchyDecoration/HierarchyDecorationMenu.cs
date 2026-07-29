using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyDecoration
{
    internal static class HierarchyDecorationMenu
    {
        private const string MenuPath =
            "GameObject/HierarchyDecoration/div";

        [MenuItem(MenuPath, false, 10)]
        private static void CreateDivider(MenuCommand menuCommand)
        {
            var gameObject = new GameObject(
                HierarchyDecorationRules.SeparatorName);
            GameObjectUtility.SetParentAndAlign(
                gameObject,
                menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(
                gameObject,
                I18N.Get("undo.createDivider"));
            Selection.activeObject = gameObject;
        }
    }
}
