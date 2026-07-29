using System;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.SceneSwitcher
{
    internal sealed class SceneSwitcherWindow : EditorWindow
    {
        private const float WindowWidth = 200f;
        private const float WindowHeight = 300f;
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName =
            "ee4v-scene-switcher-window";

        private SceneSwitcherController _controller;
        private Func<string> _createFolder;
        private SceneSwitcherView _view;
        private int _sourceSceneHandle;

        internal static void ShowAt(
            Rect anchor,
            int sourceSceneHandle,
            SceneSwitcherController controller,
            Func<string> createFolder)
        {
            if (controller == null)
            {
                return;
            }

            CloseExistingWindows();
            var window = CreateInstance<SceneSwitcherWindow>();
            window.Initialize(
                sourceSceneHandle,
                controller,
                createFolder);
            window.ShowAsDropDown(
                anchor,
                new Vector2(WindowWidth, WindowHeight));
            window.Focus();
        }

        private static void CloseExistingWindows()
        {
            var windows =
                Resources.FindObjectsOfTypeAll<SceneSwitcherWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                windows[i].Close();
            }
        }

        private void Initialize(
            int sourceSceneHandle,
            SceneSwitcherController controller,
            Func<string> createFolder)
        {
            _sourceSceneHandle = sourceSceneHandle;
            _controller = controller;
            _createFolder = createFolder ?? (() => "Assets/Scene");
            titleContent = new GUIContent(
                I18N.Get("window.title"));
            minSize = new Vector2(WindowWidth, WindowHeight);
            maxSize = minSize;
        }

        private void OnEnable()
        {
            I18N.Reloaded += OnLocalizationReloaded;
        }

        private void OnDisable()
        {
            I18N.Reloaded -= OnLocalizationReloaded;
            DetachController();
        }

        private void CreateGUI()
        {
            BuildContent();
            AttachController();
            _controller?.RefreshCatalog();
            _view?.FocusSearch();
        }

        private void OnFocus()
        {
            _controller?.RefreshOpenScenes();
        }

        private void BuildContent()
        {
            titleContent = new GUIContent(
                I18N.Get("window.title"));
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/Button/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/SearchField/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/EditorEnhancements/SceneSwitcher/UI/scene-switcher-window.uss");

            _view = new SceneSwitcherView(
                CreateText(),
                EditorGUIUtility.IconContent(
                    "SceneAsset Icon").image,
                EditorGUIUtility.IconContent(
                    "Favorite Icon").image);
            _view.QueryChanged += _controller.SetQuery;
            _view.ActivateRequested += Activate;
            _view.AddRequested += Add;
            _view.FavoriteRequested +=
                _controller.ToggleFavorite;
            _view.OrderChanged += _controller.ApplyOrder;
            _view.CreateRequested += Create;
            root.Add(_view);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void AttachController()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.StateChanged -= Render;
            _controller.OperationFailed -= ShowFailure;
            _controller.StateChanged += Render;
            _controller.OperationFailed += ShowFailure;
            Render(_controller.State);
        }

        private void DetachController()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.StateChanged -= Render;
            _controller.OperationFailed -= ShowFailure;
        }

        private void Render(SceneSwitcherViewState state)
        {
            _view?.SetState(state);
        }

        private void Activate(string path)
        {
            if (_controller.Activate(
                    path,
                    _sourceSceneHandle))
            {
                Close();
            }
        }

        private void Add(string path)
        {
            if (_controller.Add(path))
            {
                Close();
            }
        }

        private void Create(string sceneName)
        {
            if (_controller.Create(
                    sceneName,
                    _createFolder()))
            {
                Close();
            }
        }

        private void ShowFailure(SceneOperationResult result)
        {
            if (result.Failure == SceneOperationFailure.None)
            {
                return;
            }

            string message;
            switch (result.Failure)
            {
                case SceneOperationFailure.InvalidName:
                    message = I18N.Get("error.invalidName");
                    break;
                case SceneOperationFailure.InvalidFolder:
                    message = I18N.Get("error.invalidFolder");
                    break;
                case SceneOperationFailure.AlreadyExists:
                    message = I18N.Get(
                        "error.alreadyExists",
                        result.Path);
                    break;
                default:
                    message = I18N.Get("error.failed");
                    break;
            }

            EditorUtility.DisplayDialog(
                I18N.Get("error.title"),
                message,
                I18N.Get("action.ok"));
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
            }
        }

        private void OnLocalizationReloaded()
        {
            if (_controller == null)
            {
                return;
            }

            DetachController();
            BuildContent();
            AttachController();
            _view.FocusSearch();
        }

        private static SceneSwitcherViewText CreateText()
        {
            return new SceneSwitcherViewText
            {
                SearchPlaceholder =
                    I18N.Get("window.search.placeholder"),
                SearchTooltip =
                    I18N.Get("window.search.tooltip"),
                ClearSearchTooltip =
                    I18N.Get("window.search.clearTooltip"),
                Empty = I18N.Get("window.empty"),
                NoMatches = I18N.Get("window.noMatches"),
                Open = I18N.Get("window.open"),
                OpenTooltip = I18N.Get("window.openTooltip"),
                FavoriteTooltip =
                    I18N.Get("window.favorite"),
                UnfavoriteTooltip =
                    I18N.Get("window.unfavorite"),
                CreateFormat =
                    I18N.Get("window.create")
            };
        }
    }
}
