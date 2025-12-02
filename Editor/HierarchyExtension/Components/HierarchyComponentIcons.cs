using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace _4OF.ee4v.HierarchyExtension.Components {
    public class HierarchyComponentIcons : IHierarchyExtensionComponent {
        public int Priority => 100;

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject == null) return;

            if (!Settings.I.showComponentIcons) return;
            if (Settings.I.compatFaceEmo && gameObject.GetComponent<VRCAvatarDescriptor>() != null) return;

            var nameSize = EditorStyles.label.CalcSize(new GUIContent(gameObject.name));
            var startX = fullRect.x + nameSize.x + 17;

            var width = currentRect.xMax - startX;
            if (width <= 0) return;

            var componentRect = new Rect(startX, fullRect.y, width, fullRect.height);

            DrawComponents(gameObject, componentRect);
        }

        private void DrawComponents(GameObject obj, Rect rect) {
            var iconPosition = rect;
            iconPosition.x = iconPosition.xMax - 16f;
            iconPosition.width = iconPosition.height = 16f;

            var components = obj.GetComponents<Component>();

            foreach (var component in components) {
                if (iconPosition.x <= rect.x) break;

                if (component == null) continue;
                var typeName = component.GetType().Name;

                if (typeName == "ObjectStyleComponent") continue;
                if (Settings.I.ignoreComponentNameList.Contains(typeName)) continue;

                Texture image = AssetPreview.GetMiniThumbnail(component);
                if (image == null) continue;

                var iconRect = new Rect(iconPosition.x, iconPosition.y - 1f, iconPosition.width, iconPosition.height);

                var prevColor = GUI.color;
                if (component is Behaviour behaviour && !behaviour.enabled)
                    GUI.color = ColorPreset.InActiveItem;

                var tooltipContent = new GUIContent(string.Empty, null, typeName);
                GUI.DrawTexture(iconRect, image);
                GUI.Label(iconRect, tooltipContent, GUIStyle.none);

                GUI.color = prevColor;

                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && iconRect.Contains(e.mousePosition)) {
                    var anchorScreen = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax, rect.y));
                    ComponentInspectorWindow.Open(component, obj, anchorScreen);
                    e.Use();
                }

                iconPosition.x -= 16f;
            }
        }
    }
}