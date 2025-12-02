using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.Wraps;
using _4OF.ee4v.HierarchyExtension.Components.CustomStyle;
using _4OF.ee4v.HierarchyExtension.ItemStyle;
using _4OF.ee4v.Runtime;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components {
    public class HierarchyBackgroundColor : IHierarchyExtensionComponent {
        public int Priority => -200;

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject == null) return;
            if (!Settings.I.enableCustomStyleItem) return;

            if (CustomStyleUtility.IsCustomStyleItem(gameObject)) return;

            var (style, isSelf) = GetEffectiveStyle(gameObject);
            if (style == null) return;

            if (isSelf) {
                SceneHierarchyWindowWrap.SetItemIcon(instanceID, style.icon as Texture2D);
                BackGroundColor.Draw(gameObject, fullRect, style.color, style.icon);
            }
            else {
                BackGroundColor.Draw(gameObject, fullRect, style.color);
            }
        }

        private (ObjectStyleComponent, bool) GetEffectiveStyle(GameObject obj) {
            var current = obj.transform;
            while (current != null) {
                var style = current.GetComponent<ObjectStyleComponent>();
                if (style != null && (style.color != Color.clear || style.icon != null))
                    return (style, current.gameObject == obj);
                current = current.parent;
            }

            return (null, false);
        }
    }
}