using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    internal static class ProjectBrowserBackend
    {
        private static readonly Type ProjectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly MethodInfo OnGuiMethod = ProjectBrowserType?.GetMethod("OnGUI", InstanceFlags);
        private static readonly MethodInfo InitMethod = ProjectBrowserType?.GetMethod("Init", InstanceFlags, null, Type.EmptyTypes, null);
        private static readonly MethodInfo OnInspectorUpdateMethod = ProjectBrowserType?.GetMethod("OnInspectorUpdate", InstanceFlags);
        private static readonly MethodInfo NewPreviewTexturesMethod = typeof(AssetPreview).GetMethod(
            "HasAnyNewPreviewTexturesAvailable", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(int) }, null);
        private static readonly FieldInfo WindowPositionField = typeof(EditorWindow).GetField("m_Pos", InstanceFlags);
        private static readonly FieldInfo WindowParentField = typeof(EditorWindow).GetField("m_Parent", InstanceFlags);
        private static readonly FieldInfo LastInteractedBrowserField = ProjectBrowserType?.GetField(
            "s_LastInteractedProjectBrowser", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private sealed class EmbeddedBrowser
        {
            internal Action Draw;
            internal Action Initialize;
            internal string InitialFolder;
            internal bool InitializeIconSize = true;
            internal Action Update;
            internal Func<int, bool> HasNewPreviews;
            internal Action Repaint;
            internal object ListArea;
        }

        private static readonly Dictionary<EditorWindow, EmbeddedBrowser> EmbeddedBrowsers =
            new Dictionary<EditorWindow, EmbeddedBrowser>();
        private static readonly FieldInfo ListAreaField = ProjectBrowserType?.GetField("m_ListArea", InstanceFlags);
        private static readonly PropertyInfo ListRepaintProperty = ListAreaField?.FieldType.GetProperty("repaintCallback", InstanceFlags);
        private static readonly PropertyInfo ListGridSizeProperty = ListAreaField?.FieldType.GetProperty("gridSize", InstanceFlags);
        private static readonly PropertyInfo ListMaxGridSizeProperty = ListAreaField?.FieldType.GetProperty("maxGridSize", InstanceFlags);
        private static EditorWindow _drawingBrowser;
        private static ProjectBrowserState _drawingState;
        private static readonly MethodInfo ShowFolderContentsMethod = ProjectBrowserType?.GetMethod(
            "ShowFolderContents",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(int), typeof(bool) },
            null);
        private static readonly MethodInfo SetSearchMethod = ProjectBrowserType?.GetMethod(
            "SetSearch",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(string) },
            null);
        private static readonly MethodInfo ClearSearchMethod = ProjectBrowserType?.GetMethod(
            "ClearSearch",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        public static bool TryGetState(
            EditorWindow targetWindow,
            Rect? selectionRect,
            out ProjectBrowserState state)
        {
            state = null;

            var window = ResolveTargetWindow(targetWindow);
            if (window == null)
            {
                return false;
            }

            if (window == _drawingBrowser && _drawingState != null)
            {
                state = ApplySelectionOrientation(_drawingState, selectionRect);
                return true;
            }

            try
            {
                using (var serializedObject = new SerializedObject(window))
                {
                    var folderPath = GetFolderPath(serializedObject);
                    var folderGuid = string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)
                        ? null
                        : AssetDatabase.AssetPathToGUID(folderPath);
                    var searchText = GetSearchText(serializedObject);
                    var hasSearch = !string.IsNullOrEmpty(searchText);
                    var viewMode = GetViewMode(serializedObject);
                    var orientation = GetOrientation(serializedObject, viewMode,
                        window == _drawingBrowser ? null : selectionRect);

                    state = new ProjectBrowserState(
                        folderGuid,
                        folderPath,
                        searchText,
                        hasSearch,
                        viewMode,
                        orientation);
                    if (window == _drawingBrowser)
                    {
                        _drawingState = state;
                        state = ApplySelectionOrientation(state, selectionRect);
                    }
                    return true;
                }
            }
            catch (Exception)
            {
                state = null;
                return false;
            }
        }

        public static bool TryShowFolder(
            EditorWindow targetWindow,
            string folderGuid,
            bool reveal)
        {
            if (string.IsNullOrWhiteSpace(folderGuid) || ShowFolderContentsMethod == null)
            {
                return false;
            }

            var window = ResolveTargetWindow(targetWindow);
            if (window == null)
            {
                return false;
            }

            var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                return false;
            }

            var folderObject = AssetDatabase.LoadMainAssetAtPath(folderPath);
            if (folderObject == null)
            {
                return false;
            }

            try
            {
                using (var serializedObject = new SerializedObject(window))
                {
                    if (GetViewMode(serializedObject) !=
                        ProjectBrowserViewMode.TwoColumns)
                    {
                        return false;
                    }
                }

                ShowFolderContentsMethod.Invoke(window, new object[] { folderObject.GetInstanceID(), reveal });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TrySetSearch(
            EditorWindow targetWindow,
            string searchText)
        {
            if (searchText == null || SetSearchMethod == null)
            {
                return false;
            }

            var window = ResolveTargetWindow(targetWindow);
            if (window == null)
            {
                return false;
            }

            try
            {
                SetSearchMethod.Invoke(window, new object[] { searchText });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryClearSearch(EditorWindow targetWindow)
        {
            var window = ResolveTargetWindow(targetWindow);
            if (window == null)
            {
                return false;
            }

            try
            {
                if (ClearSearchMethod != null)
                {
                    ClearSearchMethod.Invoke(window, Array.Empty<object>());
                    return true;
                }

                if (SetSearchMethod == null)
                {
                    return false;
                }

                SetSearchMethod.Invoke(window, new object[] { string.Empty });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryGetOpenWindows(
            out IReadOnlyList<EditorWindow> windows)
        {
            if (ProjectBrowserType == null)
            {
                windows = Array.Empty<EditorWindow>();
                return false;
            }

            windows = Resources
                .FindObjectsOfTypeAll(ProjectBrowserType)
                .OfType<EditorWindow>()
                .Where(window => !EmbeddedBrowsers.ContainsKey(window))
                .ToArray();
            return true;
        }

        public static bool TryCreateEmbeddedBrowser(out EditorWindow browser, Action repaint, string folderPath)
        {
            browser = null;
            if (ProjectBrowserType == null || OnGuiMethod == null ||
                WindowPositionField == null || WindowParentField == null) return false;
            if (folderPath != null && (!AssetDatabase.IsValidFolder(folderPath) || InitMethod == null)) return false;

            var source = LastInteractedBrowserField?.GetValue(null) as EditorWindow;
            if (source == null || EmbeddedBrowsers.ContainsKey(source)) source = ResolveTargetWindow();
            var previous = LastInteractedBrowserField?.GetValue(null);
            try
            {
                var state = source == null ? null : EditorJsonUtility.ToJson(source);
                browser = ScriptableObject.CreateInstance(ProjectBrowserType) as EditorWindow;
                if (browser == null) return false;
                var embedded = new EmbeddedBrowser { Repaint = repaint, InitialFolder = folderPath };
                EmbeddedBrowsers.Add(browser, embedded);
                embedded.Draw = (Action)Delegate.CreateDelegate(typeof(Action), browser, OnGuiMethod);
                if (InitMethod != null)
                    embedded.Initialize = (Action)Delegate.CreateDelegate(typeof(Action), browser, InitMethod);
                if (OnInspectorUpdateMethod != null)
                    embedded.Update = (Action)Delegate.CreateDelegate(typeof(Action), browser, OnInspectorUpdateMethod);
                if (NewPreviewTexturesMethod != null)
                    embedded.HasNewPreviews = (Func<int, bool>)Delegate.CreateDelegate(typeof(Func<int, bool>), NewPreviewTexturesMethod);
                if (state != null) EditorJsonUtility.FromJsonOverwrite(state, browser);
                if (folderPath != null)
                {
                    using (var serialized = new SerializedObject(browser))
                    {
                        var viewMode = serialized.FindProperty("m_ViewMode");
                        if (viewMode == null) throw new InvalidOperationException("Project Browser view mode is unavailable.");
                        viewMode.intValue = 1;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                browser.hideFlags = HideFlags.DontSave;
                return true;
            }
            catch (Exception exception)
            {
                DestroyEmbeddedBrowser(browser);
                browser = null;
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                LastInteractedBrowserField?.SetValue(null, previous);
            }
        }

        public static bool TryDrawEmbeddedBrowser(EditorWindow browser, EditorWindow host, Rect rect)
        {
            if (browser == null || host == null || !EmbeddedBrowsers.TryGetValue(browser, out var embedded) ||
                Event.current == null || OnGuiMethod == null ||
                WindowPositionField == null || WindowParentField == null) return false;
            if (rect.width <= 0f || rect.height <= 0f) return true;

            var previousPosition = WindowPositionField.GetValue(browser);
            var previousParent = WindowParentField.GetValue(browser);
            var previousDrawingBrowser = _drawingBrowser;
            var previousDrawingState = _drawingState;
            try
            {
                _drawingBrowser = browser;
                _drawingState = null;
                WindowPositionField.SetValue(browser, new Rect(0f, 0f, rect.width, rect.height));
                WindowParentField.SetValue(browser, WindowParentField.GetValue(host));
                GUI.BeginGroup(rect);
                try
                {
                    if (embedded.InitializeIconSize || embedded.InitialFolder != null) embedded.Initialize?.Invoke();
                    if (embedded.InitialFolder != null)
                    {
                        if (!TryClearSearch(browser) || !TryShowFolder(browser,
                                AssetDatabase.AssetPathToGUID(embedded.InitialFolder), true)) return false;
                        embedded.InitialFolder = null;
                        _drawingState = null;
                        embedded.Repaint?.Invoke();
                    }
                    var listArea = ListAreaField?.GetValue(browser);
                    if (embedded.InitializeIconSize && listArea != null)
                    {
                        if (ListGridSizeProperty != null && ListMaxGridSizeProperty != null)
                            ListGridSizeProperty.SetValue(listArea, ListMaxGridSizeProperty.GetValue(listArea));
                        embedded.InitializeIconSize = false;
                    }
                    if (listArea != null && listArea != embedded.ListArea && ListRepaintProperty != null && embedded.Repaint != null)
                    {
                        var repaint = ListRepaintProperty.GetValue(listArea) as Action;
                        ListRepaintProperty.SetValue(listArea, repaint + embedded.Repaint);
                        embedded.ListArea = listArea;
                    }
                    embedded.Draw();
                }
                finally
                {
                    GUI.EndGroup();
                }
                return true;
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception is TargetInvocationException invocation
                    ? invocation.InnerException ?? invocation : exception);
                return false;
            }
            finally
            {
                _drawingBrowser = previousDrawingBrowser;
                _drawingState = previousDrawingState;
                WindowParentField.SetValue(browser, previousParent);
                WindowPositionField.SetValue(browser, previousPosition);
            }
        }

        public static bool TryUpdateEmbeddedBrowser(EditorWindow browser)
        {
            if (browser == null || !EmbeddedBrowsers.TryGetValue(browser, out var embedded)) return false;
            try
            {
                embedded.Update?.Invoke();
                if (embedded.HasNewPreviews?.Invoke(browser.GetInstanceID()) == true) embedded.Repaint?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        public static void DestroyEmbeddedBrowser(EditorWindow browser)
        {
            if (browser == null || !EmbeddedBrowsers.Remove(browser)) return;
            var wasLastInteracted = LastInteractedBrowserField?.GetValue(null) as EditorWindow == browser;
            UnityEngine.Object.DestroyImmediate(browser);
            if (wasLastInteracted) LastInteractedBrowserField?.SetValue(null, ResolveTargetWindow());
        }

        private static EditorWindow ResolveTargetWindow(
            EditorWindow preferredWindow = null)
        {
            if (preferredWindow != null)
            {
                return IsProjectBrowser(preferredWindow)
                    ? preferredWindow
                    : null;
            }

            if (_drawingBrowser != null) return _drawingBrowser;

            if (IsProjectBrowser(EditorWindow.focusedWindow))
            {
                return EditorWindow.focusedWindow;
            }

            if (IsProjectBrowser(EditorWindow.mouseOverWindow))
            {
                return EditorWindow.mouseOverWindow;
            }

            return ProjectBrowserType == null
                ? null
                : Resources.FindObjectsOfTypeAll(ProjectBrowserType).OfType<EditorWindow>()
                    .FirstOrDefault(window => !EmbeddedBrowsers.ContainsKey(window));
        }

        private static ProjectBrowserState ApplySelectionOrientation(ProjectBrowserState state, Rect? rect)
        {
            if (state.Orientation != ProjectBrowserOrientation.Unknown || !rect.HasValue) return state;
            return new ProjectBrowserState(state.FolderGuid, state.FolderPath, state.SearchText, state.HasSearch,
                state.ViewMode, rect.Value.height > EditorGUIUtility.singleLineHeight * 1.5f
                    ? ProjectBrowserOrientation.Vertical : ProjectBrowserOrientation.Horizontal);
        }

        private static bool IsProjectBrowser(EditorWindow window)
        {
            return window != null && ProjectBrowserType != null && ProjectBrowserType.IsInstanceOfType(window);
        }

        private static string GetFolderPath(SerializedObject serializedObject)
        {
            var currentFolders = serializedObject.FindProperty("m_SearchFilter.m_Folders");
            var currentFolderPath = GetFirstArrayString(currentFolders);
            if (!string.IsNullOrEmpty(currentFolderPath))
            {
                return currentFolderPath.Replace('\\', '/');
            }

            var lastFolders = serializedObject.FindProperty("m_LastFolders");
            var lastFolderPath = GetFirstArrayString(lastFolders);
            return string.IsNullOrEmpty(lastFolderPath) ? null : lastFolderPath.Replace('\\', '/');
        }

        private static string GetSearchText(SerializedObject serializedObject)
        {
            var searchProperty = serializedObject.FindProperty("m_SearchFilter.m_NameFilter");
            return searchProperty == null ? string.Empty : searchProperty.stringValue ?? string.Empty;
        }

        private static string GetFirstArrayString(SerializedProperty property)
        {
            if (property == null || !property.isArray || property.arraySize == 0)
            {
                return null;
            }

            var first = property.GetArrayElementAtIndex(0);
            return first == null ? null : first.stringValue;
        }

        private static ProjectBrowserViewMode GetViewMode(SerializedObject serializedObject)
        {
            var viewModeProperty = serializedObject.FindProperty("m_ViewMode");
            if (viewModeProperty == null)
            {
                return ProjectBrowserViewMode.Unknown;
            }

            switch (viewModeProperty.intValue)
            {
                case 0:
                    return ProjectBrowserViewMode.OneColumn;
                case 1:
                    return ProjectBrowserViewMode.TwoColumns;
                default:
                    return ProjectBrowserViewMode.Unknown;
            }
        }

        private static ProjectBrowserOrientation GetOrientation(
            SerializedObject serializedObject,
            ProjectBrowserViewMode viewMode,
            Rect? selectionRect)
        {
            if (viewMode == ProjectBrowserViewMode.OneColumn)
            {
                return ProjectBrowserOrientation.Horizontal;
            }

            var gridSizeProperty = serializedObject.FindProperty("m_ListAreaGridSize");
            if (gridSizeProperty != null)
            {
                return gridSizeProperty.floatValue > 20f
                    ? ProjectBrowserOrientation.Vertical
                    : ProjectBrowserOrientation.Horizontal;
            }

            if (selectionRect.HasValue)
            {
                return selectionRect.Value.height > EditorGUIUtility.singleLineHeight * 1.5f
                    ? ProjectBrowserOrientation.Vertical
                    : ProjectBrowserOrientation.Horizontal;
            }

            return ProjectBrowserOrientation.Unknown;
        }
    }
}
