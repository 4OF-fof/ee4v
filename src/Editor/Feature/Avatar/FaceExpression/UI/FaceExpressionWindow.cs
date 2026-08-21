using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionWindow : EditorWindow
    {
        private FaceExpressionPreview _preview;
        private FaceExpressionView _view;
        private ISettingsService _settings;
        private GameObject _avatar;
        private AnimationClip _clip;
        private IReadOnlyList<BlendShapeChannel> _channels = Array.Empty<BlendShapeChannel>();

        [MenuItem("ee4v/Avatar/Face Expression Editor")]
        private static void Open()
        {
            ShowWindow();
            FaceExpressionGroupWindow.ShowWindow();
        }

        internal static void ShowWindow()
        {
            var window = GetWindow<FaceExpressionWindow>();
            window.titleContent = UiTextFactory.CreateGuiContent(I18N.Get("window.title"));
            window.minSize = new Vector2(720f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = CoreSettings.Current;
            _settings.Changed += OnSettingChanged;
            FaceExpressionGroupSession.Changed += ApplyGroupFilter;
            FaceExpressionGroupSession.MeshesChanged += RefreshClip;
            _preview = new FaceExpressionPreview(Repaint);
            Undo.undoRedoPerformed += RefreshClip;
            I18N.Reloaded += Rebuild;
        }

        private void OnDisable()
        {
            if (_settings != null)
            {
                _settings.Changed -= OnSettingChanged;
            }

            Undo.undoRedoPerformed -= RefreshClip;
            I18N.Reloaded -= Rebuild;
            FaceExpressionGroupSession.Changed -= ApplyGroupFilter;
            FaceExpressionGroupSession.MeshesChanged -= RefreshClip;
            _preview?.Dispose();
            _preview = null;
        }

        private void CreateGUI()
        {
            BuildContent();
            if (_avatar == null && Selection.activeGameObject != null)
            {
                SetAvatar(Selection.activeGameObject);
            }
            else
            {
                RenderState();
            }
        }

        private void Rebuild()
        {
            if (rootVisualElement.panel == null)
            {
                return;
            }

            BuildContent();
            RenderState();
        }

        private void BuildContent()
        {
            titleContent = UiTextFactory.CreateGuiContent(I18N.Get("window.title"));
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("ee4v-ui");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/SearchField/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");

            _view = new FaceExpressionView(CreateText(), rect => _preview?.Draw(rect));
            _view.AvatarChanged += SetAvatar;
            _view.ClipChanged += SetClip;
            _view.NewClipRequested += CreateClip;
            _view.ChannelChanged += ChangeChannel;
            _view.ResetViewRequested += () => _preview?.ResetView();
            root.Add(_view);
        }

        private void SetAvatar(GameObject avatar)
        {
            _avatar = avatar;
            _clip = null;
            _preview?.SetAvatar(avatar);
            FaceExpressionGroupSession.SetAvatar(avatar);
            _view?.SetAvatar(avatar);
            FaceExpressionSettings.EnsureNamePreset(avatar, _settings);
        }

        private void SetClip(AnimationClip clip)
        {
            _clip = clip;
            RefreshClip();
        }

        private void RefreshClip()
        {
            _channels = FaceExpressionClipEditor.Read(
                _avatar,
                _clip,
                FaceExpressionSettings.GetSeparators(_settings),
                FaceExpressionGroupSession.RendererPaths);
            _view?.SetClip(_clip);
            FaceExpressionGroupSession.UpdateChannels(_channels);
            _preview?.SetChannels(_channels);
        }

        private void ChangeChannel(BlendShapeChannel channel)
        {
            if (_clip == null)
            {
                return;
            }

            FaceExpressionClipEditor.Write(_clip, channel);
            _preview?.SetChannels(_channels);
        }

        private void CreateClip()
        {
            var folder = ProjectAssetSettings.EnsureAssetFolder(
                "Animation/Facial",
                _settings);
            var path = EditorUtility.SaveFilePanelInProject(
                I18N.Get("dialog.createTitle"),
                I18N.Get("dialog.defaultName"),
                "anim",
                I18N.Get("dialog.createMessage"),
                folder);
            var clip = FaceExpressionClipEditor.Create(path);
            if (clip == null)
            {
                return;
            }

            Selection.activeObject = clip;
            SetClip(clip);
        }

        private void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    FaceExpressionSettings.BlendShapeSeparators) ||
                ReferenceEquals(
                    args.Definition,
                    FaceExpressionSettings.BlendShapePresets))
            {
                RefreshClip();
            }
        }

        private void RenderState()
        {
            _view?.SetAvatar(_avatar);
            _view?.SetClip(_clip);
            ApplyGroupFilter();
            _preview?.SetChannels(_channels);
        }

        private void ApplyGroupFilter()
        {
            var selectedGroupName =
                FaceExpressionGroupSession.SelectedGroupName;
            _view?.SetChannels(
                FaceExpressionGroupSession.Filter(_channels),
                string.IsNullOrEmpty(selectedGroupName)
                    ? I18N.Get("group.all")
                    : selectedGroupName,
                FaceExpressionSettings.GetNameRule(_settings),
                !string.IsNullOrEmpty(
                    FaceExpressionGroupSession.SelectedGroupKey));
        }

        private static FaceExpressionViewText CreateText()
        {
            return new FaceExpressionViewText
            {
                Avatar = I18N.Get("field.avatar"),
                Clip = I18N.Get("field.clip"),
                NewClip = I18N.Get("action.newClip"),
                ResetView = I18N.Get("action.resetView"),
                SearchPlaceholder = I18N.Get("search.placeholder"),
                SearchTooltip = I18N.Get("search.tooltip"),
                ClearSearchTooltip = I18N.Get("search.clearTooltip"),
                BlendShapes = I18N.Get("section.blendShapes"),
                ClipOnly = I18N.Get("filter.clipOnly"),
                ClipOnlyTooltip = I18N.Get("filter.clipOnlyTooltip"),
                NoBlendShapes = I18N.Get("empty.blendShapes"),
                ClipRequired = I18N.Get("status.clipRequired")
            };
        }
    }
}
