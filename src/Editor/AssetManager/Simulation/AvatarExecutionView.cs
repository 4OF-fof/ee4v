using System;
using BlackStartX.GestureManager;
using BlackStartX.GestureManager.Editor;
using BlackStartX.GestureManager.Editor.Modules.Vrc3;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.Simulation
{
    public sealed class AvatarExecutionView : VisualElement, IDisposable
    {
        private readonly GameObject _avatar;
        private readonly Action _repaint;
        private readonly GestureManager _manager;
        private readonly PrefabScenePreview _viewport;
        private readonly UiTextElement _status;
        private readonly VisualElement _inputs;
        private readonly VisualElement _radialHost;
        private readonly IMGUIContainer _radialCanvas;
        private readonly UiButton[,] _gestures = new UiButton[2, 8];
        private readonly FloatField[] _weights = new FloatField[2];
        private ModuleVrc3 _module;
        private RadialMenu _radial;
        private UnityEditor.Editor _editor;
        private bool _failed;
        private bool _disposed;

        public AvatarExecutionView(GameObject avatar, Action repaint)
        {
            _avatar = avatar;
            _repaint = repaint;
            _manager = GestureManagerIntegration.FindManager(avatar);
            AddToClassList("ee4v-execution");
            var preview = new PreviewPane(Text("title"));
            preview.AddToClassList("ee4v-modification-workflow__preview-pane");
            _viewport = new PrefabScenePreview();
            _viewport.AddToClassList("ee4v-modification-workflow__preview");
            _viewport.SetFlexibleLayout(true);
            _viewport.SetViewToggleVisible(false);
            _viewport.SetFullBodyFraming(false);
            _viewport.SetPrefab(avatar);
            preview.Content.Add(_viewport);
            Add(preview);

            var controls = new ScrollView(ScrollViewMode.Vertical);
            controls.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            controls.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            controls.AddToClassList("ee4v-execution__controls");
            _status = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            _status.AddToClassList("ee4v-execution__status");
            controls.Add(_status);
            _inputs = new VisualElement();
            controls.Add(_inputs);
            _radialHost = new VisualElement();
            _radialHost.name = "executionRadial";
            _radialHost.AddToClassList("ee4v-execution__radial");
            _radialCanvas = new IMGUIContainer(DrawRadial);
            _radialHost.Add(_radialCanvas);
            _inputs.Add(_radialHost);
            var hands = new VisualElement();
            hands.AddToClassList("ee4v-execution__hands");
            for (var hand = 0; hand < 2; hand++)
            {
                var handIndex = hand;
                var column = new VisualElement();
                column.AddToClassList("ee4v-execution__hand");
                column.Add(UiTextFactory.Create(Text(hand == 0 ? "leftHand" : "rightHand"),
                    UiClassNames.SectionTitle));
                for (var gesture = 0; gesture < 8; gesture++)
                {
                    var gestureIndex = gesture;
                    var button = new UiButton(string.Empty, () =>
                    {
                        SetParameter(handIndex == 0 ? "GestureLeft" : "GestureRight", gestureIndex);
                        Refresh();
                    }, variant: UiButtonVariant.Ghost);
                    button.name = "executionGesture" + handIndex + "_" + gestureIndex;
                    column.Add(button);
                    _gestures[hand, gesture] = button;
                }
                var weight = UiTextFactory.CreateFloatField(Text("weight"));
                weight.RegisterValueChangedCallback(evt => SetParameter(
                    handIndex == 0 ? "GestureLeftWeight" : "GestureRightWeight", Mathf.Clamp01(evt.newValue)));
                column.Add(weight);
                _weights[hand] = weight;
                hands.Add(column);
            }
            _inputs.Add(hands);
            Add(controls);
            schedule.Execute(Refresh).Every(33);
            Refresh();
        }

        private void Refresh()
        {
            if (_disposed) { return; }
            if (_manager == null)
            {
                _status.SetText(Text("missing"));
            }
            else if (_avatar == null)
            {
                _status.SetText(Text("avatarUnavailable"));
            }
            else if (!EditorApplication.isPlaying)
            {
                _status.SetText(Text("ready"));
            }
            else if (!_failed && (_module == null || !_module.Active))
            {
                try
                {
                    _radial?.RemoveFromHierarchy();
                    if (_editor != null) { UnityEngine.Object.DestroyImmediate(_editor); }
                    _module = GestureManagerIntegration.Connect(_avatar, _manager);
                    _editor = UnityEditor.Editor.CreateEditor(_manager, typeof(GestureManagerEditor));
                    _radial = _module.GetOrCreateRadial(_editor);
                    for (var hand = 0; hand < 2; hand++)
                    for (var gesture = 0; gesture < 8; gesture++)
                    {
                        _gestures[hand, gesture].SetLabel(_module.GetGestureTextNameByIndex(gesture));
                    }
                    _viewport.ResetView();
                    _status.SetText(Text("running"));
                }
                catch (Exception exception)
                {
                    _failed = true;
                    _status.SetText(string.Format(Text("failed"), exception.GetBaseException().Message));
                    Debug.LogException(exception);
                }
            }
            var active = EditorApplication.isPlaying && _avatar != null && _module != null && _module.Active && !_failed;
            _inputs.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (!active) { return; }
            for (var hand = 0; hand < 2; hand++)
            {
                var gesture = _module.GetParam(hand == 0 ? "GestureLeft" : "GestureRight");
                var selected = gesture == null ? 0 : Mathf.RoundToInt(gesture.FloatValue());
                for (var index = 0; index < 8; index++)
                {
                    _gestures[hand, index].SetEnabled(gesture != null);
                    _gestures[hand, index].EnableInClassList("ee4v-execution__gesture--selected", selected == index);
                }
                var weight = _module.GetParam(hand == 0 ? "GestureLeftWeight" : "GestureRightWeight");
                _weights[hand].SetEnabled(weight != null);
                _weights[hand].SetValueWithoutNotify(weight?.FloatValue() ?? 0f);
            }
            _radialCanvas.MarkDirtyRepaint();
            _repaint?.Invoke();
        }

        private void SetParameter(string name, float value)
        {
            if (EditorApplication.isPlaying && _module != null && _module.Active)
            {
                _module.GetParam(name)?.Set(_module, value);
            }
        }

        private void DrawRadial()
        {
            if (_disposed || !EditorApplication.isPlaying || _radial == null || !_module.Active) { return; }
            var rect = GUILayoutUtility.GetRect(RadialMenu.Size, RadialMenu.Size, GUILayout.ExpandWidth(true));
            rect.x += (rect.width - RadialMenu.Size) * 0.5f;
            rect.width = RadialMenu.Size;
            if (Event.current.type != EventType.Layout && Event.current.type != EventType.Used)
            {
                _radial.Rect = rect;
            }
            _radial.Render(_radialHost, rect);
        }

        private static string Text(string key) => I18N.Get("workflow.execution." + key);

        public void Dispose()
        {
            _disposed = true;
            _viewport.Dispose();
            _radial?.RemoveFromHierarchy();
            if (_editor != null) { UnityEngine.Object.DestroyImmediate(_editor); }
            _editor = null;
        }
    }
}
