using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal static class VrchatExpressionMenuWriter
    {
        private const string MenuTypeName =
            "VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu";
        private const int PageSize = 8;

        internal static bool IsAvailable => FindType(MenuTypeName) != null;

        internal static ScriptableObject Write(
            string assetPath,
            IReadOnlyList<GestureMatrixControllerWriter.EffectiveMenuEntry> entries,
            IReadOnlyList<Texture2D> icons)
        {
            var menuType = FindType(MenuTypeName);
            if (menuType == null)
            {
                throw new InvalidOperationException("VRCExpressionsMenu was not found.");
            }

            AssetDatabase.DeleteAsset(assetPath);
            var container = ScriptableObject.CreateInstance(menuType);
            AssetDatabase.CreateAsset(container, assetPath);

            var subMenus = new List<ScriptableObject>();
            var facialItems = BuildFacialItems(
                entries ?? Array.Empty<GestureMatrixControllerWriter.EffectiveMenuEntry>(),
                icons ?? Array.Empty<Texture2D>());
            var facialMenu = BuildPagedMenu(
                menuType,
                "FacialSet",
                facialItems,
                subMenus);
            if (GetControls(facialMenu).Count > 0)
            {
                AddControl(
                    container,
                    MenuNode.ForSubMenu("FacialSet", facialMenu));
            }

            foreach (var subMenu in subMenus)
            {
                AssetDatabase.AddObjectToAsset(subMenu, container);
                EditorUtility.SetDirty(subMenu);
            }

            EditorUtility.SetDirty(container);
            return container;
        }

        private static IReadOnlyList<MenuNode> BuildFacialItems(
            IReadOnlyList<GestureMatrixControllerWriter.EffectiveMenuEntry> entries,
            IReadOnlyList<Texture2D> icons)
        {
            var direct = new List<MenuNode>();
            var groups = new Dictionary<FaceGesture, List<MenuNode>>();
            var groupOrder = new List<FaceGesture>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var item = MenuNode.Toggle(
                    entry.Name,
                    GestureMatrixControllerWriter.MenuParameter,
                    index + 1,
                    index < icons.Count ? icons[index] : null);
                if (!entry.LeftGesture.HasValue)
                {
                    direct.Add(item);
                    continue;
                }

                var gesture = entry.LeftGesture.Value;
                if (!groups.TryGetValue(gesture, out var group))
                {
                    group = new List<MenuNode>();
                    groups.Add(gesture, group);
                    groupOrder.Add(gesture);
                }

                group.Add(item);
            }

            foreach (var gesture in groupOrder)
            {
                direct.Add(MenuNode.Group(gesture.ToString(), groups[gesture]));
            }

            return direct;
        }

        private static ScriptableObject BuildPagedMenu(
            Type menuType,
            string name,
            IReadOnlyList<MenuNode> source,
            ICollection<ScriptableObject> subMenus)
        {
            var menu = ScriptableObject.CreateInstance(menuType);
            menu.name = name;
            subMenus.Add(menu);

            var items = new List<MenuNode>();
            for (var index = 0; index < source.Count; index++)
            {
                var item = source[index];
                if (item.Children != null)
                {
                    var child = BuildPagedMenu(
                        menuType,
                        item.Label,
                        item.Children,
                        subMenus);
                    items.Add(MenuNode.ForSubMenu(item.Label, child));
                }
                else
                {
                    items.Add(item);
                }
            }

            if (items.Count <= PageSize)
            {
                foreach (var item in items)
                {
                    AddControl(menu, item);
                }

                return menu;
            }

            foreach (var item in items.Take(PageSize - 1))
            {
                AddControl(menu, item);
            }

            var next = BuildPagedMenu(
                menuType,
                name + " Next",
                items.Skip(PageSize - 1).ToArray(),
                subMenus);
            AddControl(menu, MenuNode.ForSubMenu("Next", next));
            return menu;
        }

        private static void AddControl(ScriptableObject menu, MenuNode item)
        {
            var controlsField = menu.GetType().GetField("controls");
            var controls = GetControls(menu);

            var controlType = controlsField.FieldType.GetGenericArguments()[0];
            var control = Activator.CreateInstance(controlType);
            SetField(control, "name", item.Label);
            SetField(control, "icon", item.Icon);
            SetEnumField(control, "type", item.SubMenu == null ? "Toggle" : "SubMenu");
            SetField(control, "value", item.Value);
            SetField(control, "subMenu", item.SubMenu);
            var parameterField = controlType.GetField("parameter");
            if (parameterField == null)
            {
                throw new InvalidOperationException(
                    "VRCExpressionsMenu.Control.parameter was not found.");
            }

            var parameter = Activator.CreateInstance(parameterField.FieldType);
            SetField(
                parameter,
                "name",
                item.SubMenu == null ? item.Parameter : string.Empty);
            parameterField.SetValue(control, parameter);
            SetEmptyArray(control, "subParameters");
            SetEmptyArray(control, "labels");

            controls.Add(control);
        }

        private static IList GetControls(ScriptableObject menu)
        {
            var controlsField = menu.GetType().GetField("controls");
            if (controlsField == null)
            {
                throw new InvalidOperationException("VRCExpressionsMenu.controls was not found.");
            }

            var controls = controlsField.GetValue(menu) as IList;
            if (controls != null)
            {
                return controls;
            }

            controls = (IList)Activator.CreateInstance(controlsField.FieldType);
            controlsField.SetValue(menu, controls);
            return controls;
        }

        private static void SetEmptyArray(object target, string name)
        {
            var field = target.GetType().GetField(name);
            if (field == null || !field.FieldType.IsArray)
            {
                throw new InvalidOperationException(
                    target.GetType().FullName + "." + name + " was not found.");
            }

            field.SetValue(
                target,
                Array.CreateInstance(field.FieldType.GetElementType(), 0));
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException(
                    target.GetType().FullName + "." + name + " was not found.");
            }

            field.SetValue(target, value);
        }

        private static void SetEnumField(object target, string name, string value)
        {
            var field = target.GetType().GetField(name);
            if (field == null)
            {
                throw new InvalidOperationException(
                    target.GetType().FullName + "." + name + " was not found.");
            }

            field.SetValue(target, Enum.Parse(field.FieldType, value));
        }

        private sealed class MenuNode
        {
            private MenuNode(
                string label,
                string parameter,
                float value,
                Texture2D icon,
                ScriptableObject subMenu,
                IReadOnlyList<MenuNode> children)
            {
                Label = label;
                Parameter = parameter;
                Value = value;
                Icon = icon;
                SubMenu = subMenu;
                Children = children;
            }

            internal string Label { get; }
            internal string Parameter { get; }
            internal float Value { get; }
            internal Texture2D Icon { get; }
            internal ScriptableObject SubMenu { get; }
            internal IReadOnlyList<MenuNode> Children { get; }

            internal static MenuNode Toggle(
                string label,
                string parameter,
                float value,
                Texture2D icon)
            {
                return new MenuNode(label, parameter, value, icon, null, null);
            }

            internal static MenuNode Group(string label, IReadOnlyList<MenuNode> children)
            {
                return new MenuNode(label, null, 0f, null, null, children);
            }

            internal static MenuNode ForSubMenu(string label, ScriptableObject subMenu)
            {
                return new MenuNode(label, null, 0f, null, subMenu, null);
            }
        }
    }
}
