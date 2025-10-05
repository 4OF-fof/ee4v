using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.HierarchyExtension.UI.HierarchyItem.Window._Component {
    public static class ComponentList {
        public static VisualElement Element(GameObject gameObject, System.Action<bool> onLockChanged) {
            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap
                }
            };
            foreach (var component in gameObject.GetComponents<Component>()) {
                if (component.GetType().Name == "ObjectStyleComponent") continue;
                var componentButton = new Button {
                    style = {
                        height = 24,
                        marginRight = 4, marginBottom = 4,
                        paddingRight = 8, paddingLeft = 8, paddingTop = 2, paddingBottom = 2,
                        fontSize = 12,
                        backgroundColor = Color.clear,
                        borderTopRightRadius = 10, borderTopLeftRadius = 10,
                        borderBottomRightRadius = 10, borderBottomLeftRadius = 10,
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        justifyContent = Justify.Center,
                    },
                    focusable = false
                };

                componentButton.clicked += () => {
                    var anchor = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    onLockChanged?.Invoke(true);
                    ComponentInspector.Open(component, gameObject, anchor);
                };

                var buttonIcon = new Image {
                    image = AssetPreview.GetMiniThumbnail(component),
                    style = {
                        width = 16, height = 16,
                        paddingRight = 4
                    }
                };
                componentButton.Add(buttonIcon);
                var buttonLabel = new Label(component.GetType().Name);
                componentButton.Add(buttonLabel);

                root.Add(componentButton);
            }
            return root;
        }
    }
}