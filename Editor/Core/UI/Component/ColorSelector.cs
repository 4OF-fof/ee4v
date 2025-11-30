using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.ProjectExtension.ItemStyle;
using _4OF.ee4v.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.Core.UI.Component {
    public class ColorSelector : VisualElement {
        private const float Alpha = 0.7f;
        public static Action<Color, List<ObjectStyleComponent>> OnColorChangedComponent;

        private static readonly List<Color> DarkColorList = new() {
            new Color(0.7f, 0f, 0f, Alpha),
            new Color(0.7f, 0.35f, 0f, Alpha),
            new Color(0.7f, 0.7f, 0f, Alpha),
            new Color(0.35f, 0.7f, 0f, Alpha),
            new Color(0f, 0.7f, 0f, Alpha),
            new Color(0f, 0.7f, 0.35f, Alpha),
            new Color(0f, 0.7f, 0.7f, Alpha),
            new Color(0f, 0.35f, 0.7f, Alpha),
            new Color(0f, 0f, 0.7f, Alpha),
            new Color(0.35f, 0f, 0.7f, Alpha),
            new Color(0.7f, 0f, 0.7f, Alpha),
            new Color(0.7f, 0f, 0.35f, Alpha)
        };

        private static readonly List<Color> LightColorList = new() {
            new Color(1f, 0.2f, 0.2f, Alpha),
            new Color(1f, 0.55f, 0.2f, Alpha),
            new Color(1f, 1f, 0.2f, Alpha),
            new Color(0.55f, 1f, 0.2f, Alpha),
            new Color(0.2f, 1f, 0.2f, Alpha),
            new Color(0.2f, 1f, 0.55f, Alpha),
            new Color(0.2f, 1f, 1f, Alpha),
            new Color(0.2f, 0.55f, 1f, Alpha),
            new Color(0.2f, 0.2f, 1f, Alpha),
            new Color(0.55f, 0.2f, 1f, Alpha),
            new Color(1f, 0.2f, 1f, Alpha),
            new Color(1f, 0.2f, 0.55f, Alpha)
        };

        private static Color _selectedColor = Color.clear;

        public ColorSelector(List<ObjectStyleComponent> componentList) {
            ColorList.RemoveAll(c => c == Color.clear);
            ColorList.Insert(0, Color.clear);
            if (componentList is { Count: 1 })
                _selectedColor = componentList[0].color != Color.clear ? componentList[0].color : ColorList[0];
            else
                _selectedColor = Color.clear;
            Add(CreateColorSelectorElement(color =>
                {
                    foreach (var component in componentList) component.color = color;
                    OnColorChangedComponent?.Invoke(color, componentList);
                }
            ));
        }

        public ColorSelector(List<string> folderPaths) {
            ColorList.RemoveAll(c => c == Color.clear);
            ColorList.Insert(0, Color.clear);

            if (folderPaths is { Count: 1 }) {
                var guid = AssetDatabase.AssetPathToGUID(folderPaths[0]);
                var folderStyle = FolderStyleList.instance.Contents.FirstOrDefault(s => s.guid == guid);
                var existingColor = folderStyle?.color ?? Color.clear;
                _selectedColor = existingColor != Color.clear ? existingColor : ColorList[0];
            }
            else {
                _selectedColor = Color.clear;
            }

            Add(CreateColorSelectorElement(color =>
                {
                    if (folderPaths != null)
                        foreach (var folderPath in folderPaths) {
                            var guid = AssetDatabase.AssetPathToGUID(folderPath);
                            if (string.IsNullOrEmpty(guid)) continue;

                            var idx = FolderStyleService.IndexOfGuid(guid);
                            if (color == Color.clear) {
                                if (idx >= 0) FolderStyleList.instance.RemoveFolderStyle(idx);
                            }
                            else {
                                if (idx == -1)
                                    FolderStyleList.instance.AddFolderStyle(guid, color, null);
                                else
                                    FolderStyleList.instance.UpdateFolderStyle(idx, color: color);
                            }
                        }

                    EditorApplication.RepaintProjectWindow();
                }
            ));
        }

        private static List<Color> ColorList => EditorGUIUtility.isProSkin ? DarkColorList : LightColorList;

        private static VisualElement CreateColorSelectorElement(Action<Color> onColorSelected) {
            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap
                }
            };

            var items = new List<VisualElement>();

            foreach (var color in ColorList) {
                var item = color == Color.clear ? NotSelectedItem() : ColorPreview(color);
                if (color == _selectedColor) item = SelectedStyle(item);

                items.Add(item);
                root.Add(item);

                item.RegisterCallback<ClickEvent>(_ =>
                {
                    foreach (var it in items) {
                        it.name = "color-selector-item";
                        it.style.borderTopWidth = 0;
                        it.style.borderRightWidth = 0;
                        it.style.borderBottomWidth = 0;
                        it.style.borderLeftWidth = 0;
                        it.style.backgroundColor = Color.clear;
                    }

                    _selectedColor = color;
                    item = SelectedStyle(item);

                    onColorSelected?.Invoke(_selectedColor);
                    EditorApplication.RepaintHierarchyWindow();
                });
            }

            return root;
        }

        private static VisualElement SelectedStyle(VisualElement item) {
            item.name = "color-selector-selected";
            item.style.borderTopWidth = 1;
            item.style.borderRightWidth = 1;
            item.style.borderBottomWidth = 1;
            item.style.borderLeftWidth = 1;
            var selectedBorderColor = ColorPreset.ItemSelectedBorder;
            var selectedBackgroundColor = ColorPreset.ItemSelectedBackGround;
            item.style.borderTopColor = new StyleColor(selectedBorderColor);
            item.style.borderRightColor = new StyleColor(selectedBorderColor);
            item.style.borderBottomColor = new StyleColor(selectedBorderColor);
            item.style.borderLeftColor = new StyleColor(selectedBorderColor);
            item.style.backgroundColor = new StyleColor(selectedBackgroundColor);
            return item;
        }

        private static VisualElement ColorPreview(Color color) {
            var item = new VisualElement {
                name = "color-selector-item",
                style = {
                    width = 24, height = 24,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            var hoverColor = ColorPreset.MouseOverBackground;
            item.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (item.name != "color-selector-selected") item.style.backgroundColor = hoverColor;
            });
            item.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (item.name != "color-selector-selected") item.style.backgroundColor = Color.clear;
            });

            var colorArea = new VisualElement {
                style = {
                    width = 16, height = 16,
                    backgroundColor = color,
                    marginRight = 4, marginLeft = 4,
                    borderTopRightRadius = 50, borderTopLeftRadius = 50,
                    borderBottomRightRadius = 50, borderBottomLeftRadius = 50
                }
            };
            item.Add(colorArea);
            return item;
        }

        private static VisualElement NotSelectedItem() {
            var notSelectedItem = new VisualElement {
                name = "color-selector-item",
                style = {
                    width = 24, height = 24,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            var hoverColor = ColorPreset.MouseOverBackground;
            notSelectedItem.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (notSelectedItem.name != "color-selector-selected")
                    notSelectedItem.style.backgroundColor = hoverColor;
            });
            notSelectedItem.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (notSelectedItem.name != "color-selector-selected")
                    notSelectedItem.style.backgroundColor = Color.clear;
            });
            var notSelectedIcon = new Image {
                image = EditorGUIUtility.IconContent("winbtn_win_close").image,
                scaleMode = ScaleMode.ScaleToFit,
                style = {
                    width = 16, height = 16
                }
            };
            notSelectedItem.Add(notSelectedIcon);
            return notSelectedItem;
        }
    }
}