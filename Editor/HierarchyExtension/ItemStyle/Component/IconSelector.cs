using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.HierarchyExtension.ItemStyle.Component {
    public class IconSelector : VisualElement {
        public static Action<Texture, List<ObjectStyleComponent>> OnIconChanged;
        private static Texture _separatorTexture;

        public IconSelector(List<GameObject> gameObjectList, List<ObjectStyleComponent> objectStyleComponentList) {
            Texture selectedIcon;
            var baseIcons = BuildIconListFromPrefs();
            baseIcons.RemoveAll(t => t == null);
            var componentIcons = new List<Texture>();
            if (gameObjectList != null)
                foreach (var tex in from go in gameObjectList
                         where go != null
                         select go.GetComponents<UnityEngine.Component>()
                         into comps
                         from comp in comps
                         where comp != null && comp.GetType().Name != "ObjectStyleComponent" &&
                             !EditorPrefsManager.IgnoreComponentNameList.Contains(comp.GetType().Name)
                         select EditorGUIUtility.ObjectContent(comp, comp.GetType())
                         into content
                         select content?.image
                         into tex
                         where tex != null && !componentIcons.Contains(tex)
                         select tex)
                    componentIcons.Add(tex);

            var mergedIconList = new List<Texture> { null };
            foreach (var tex in componentIcons.Where(tex => !mergedIconList.Contains(tex))) mergedIconList.Add(tex);
            foreach (var tex in baseIcons.Where(tex => !(mergedIconList.Contains(tex) && tex != Separator)))
                mergedIconList.Add(tex);

            if (objectStyleComponentList is { Count: 1 }) {
                var stored = objectStyleComponentList[0].icon;
                selectedIcon = stored != null && stored != Separator ? stored : mergedIconList[0];
            }
            else {
                selectedIcon = null;
            }

            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;

            var items = new List<VisualElement>();

            foreach (var icon in mergedIconList) {
                VisualElement item;
                if (icon == null) item = NotSelectedItem();
                else if (icon == Separator) item = SpacerItem();
                else item = IconPreview(icon);

                if (icon == selectedIcon && icon != Separator) item = SelectedStyle(item);

                items.Add(item);
                Add(item);

                if (icon == Separator) continue;
                var capturedItem = item;
                var capturedIcon = icon;
                capturedItem.RegisterCallback<ClickEvent>(_ =>
                {
                    foreach (var it in items) {
                        it.name = "icon-selector-item";
                        it.style.borderTopWidth = 0;
                        it.style.borderRightWidth = 0;
                        it.style.borderBottomWidth = 0;
                        it.style.borderLeftWidth = 0;
                        it.style.backgroundColor = Color.clear;
                    }

                    selectedIcon = capturedIcon;
                    capturedItem = SelectedStyle(capturedItem);
                    foreach (var component in objectStyleComponentList) component.icon = selectedIcon;

                    OnIconChanged?.Invoke(selectedIcon, objectStyleComponentList);
                    EditorApplication.RepaintHierarchyWindow();
                });
            }
        }

        private static Texture Separator => _separatorTexture ??= CreateSeparatorTexture();

        private static List<Texture> BuildIconListFromPrefs() {
            var list = new List<Texture> { Separator };
            var keys = EditorPrefsManager.IconList;
            if (keys == null || keys.Count == 0) return list;
            foreach (var key in keys.Where(key => !string.IsNullOrEmpty(key))) {
                if (key == "<SEP>") {
                    list.Add(Separator);
                    continue;
                }

                Texture tex;
                if (key.StartsWith("asset:")) {
                    var path = key["asset:".Length..];
                    tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
                }
                else {
                    tex = EditorGUIUtility.FindTexture(key);
                    if (tex == null) {
                        var content = EditorGUIUtility.IconContent(key);
                        tex = content?.image;
                    }
                }

                if (tex != null) list.Add(tex);
            }

            return list;
        }

        private static VisualElement SpacerItem() {
            return new VisualElement {
                name = "icon-selector-spacer",
                style = {
                    width = new StyleLength(new Length(100, LengthUnit.Percent)),
                    height = 4
                }
            };
        }

        private static VisualElement SelectedStyle(VisualElement item) {
            item.name = "icon-selector-selected";
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

        private static VisualElement IconPreview(Texture icon) {
            var item = new VisualElement {
                name = "icon-selector-item",
                style = {
                    width = 24, height = 24,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            var hoverColor = ColorPreset.MouseOverBackground;
            item.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (item.name != "icon-selector-selected") item.style.backgroundColor = hoverColor;
            });
            item.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (item.name != "icon-selector-selected") item.style.backgroundColor = Color.clear;
            });

            var iconImage = new Image {
                image = icon != null ? icon : EditorGUIUtility.IconContent("GameObject Icon").image,
                style = {
                    width = 16, height = 16,
                    marginRight = 4, marginLeft = 4
                }
            };
            item.Add(iconImage);
            return item;
        }

        private static VisualElement NotSelectedItem() {
            var notSelectedItem = new VisualElement {
                name = "icon-selector-item",
                style = {
                    width = 24, height = 24,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            var hoverColor = ColorPreset.MouseOverBackground;
            notSelectedItem.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (notSelectedItem.name != "icon-selector-selected")
                    notSelectedItem.style.backgroundColor = hoverColor;
            });
            notSelectedItem.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (notSelectedItem.name != "icon-selector-selected")
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

        private static Texture CreateSeparatorTexture() {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.clear);
            tex.Apply();
            tex.hideFlags = HideFlags.DontSave;
            return tex;
        }
    }
}