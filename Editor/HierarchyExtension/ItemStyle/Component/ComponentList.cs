using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.HierarchyExtension.ItemStyle.Component {
    public static class ComponentList {
        public static VisualElement Element(GameObject gameObject, Action<bool> onLockChanged) {
            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap
                }
            };
            foreach (var component in gameObject.GetComponents<UnityEngine.Component>()) {
                if (component == null || component.GetType().Name == "ObjectStyleComponent") continue;
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
                        justifyContent = Justify.Center
                    },
                    focusable = false
                };

                componentButton.clicked += () =>
                {
                    var anchor = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    onLockChanged?.Invoke(true);
                    ComponentInspectorWindow.Open(component, gameObject, anchor);
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

            var renderer = gameObject.GetComponent<Renderer>();
            Material[] materialList = null;
            if (renderer != null) materialList = renderer.sharedMaterials;

            if (materialList == null) return root;

            foreach (var material in materialList) {
                if (material == null) continue;
                var materialButton = new Button {
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
                        justifyContent = Justify.Center
                    },
                    focusable = false
                };

                materialButton.clicked += () =>
                {
                    var anchor = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    onLockChanged?.Invoke(true);
                    ComponentInspectorWindow.Open(material, gameObject, anchor);
                };

                var buttonIcon = new Image {
                    image = AssetPreview.GetMiniThumbnail(material),
                    style = {
                        width = 16, height = 16,
                        paddingRight = 4
                    }
                };
                materialButton.Add(buttonIcon);
                var buttonLabel = new Label(material.name);
                materialButton.Add(buttonLabel);

                root.Add(materialButton);
            }

            return root;
        }
    }
}