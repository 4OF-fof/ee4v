using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class GestureAssignmentWindow : EditorWindow
    {
        private readonly VrchatFaceExpressionGateway _gateway =
            new VrchatFaceExpressionGateway();
        private GestureAssignmentView _view;
        private GameObject _avatar;
        private FaceExpressionPreview _preview;
        private readonly Dictionary<AnimationClip, ThumbnailEntry> _thumbnails =
            new Dictionary<AnimationClip, ThumbnailEntry>();
        private IReadOnlyList<string> _previewRendererPaths = Array.Empty<string>();

        [MenuItem("ee4v/Window/Gesture Assignment/Gesture Assignments")]
        private static void Open()
        {
            ShowFor(Selection.activeGameObject);
        }

        internal static void ShowFor(GameObject avatar)
        {
            var window = GetWindow<GestureAssignmentWindow>();
            window.titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("assignmentWindow.title"));
            window.minSize = new Vector2(900f, 680f);
            window.Show();
            window.SetAvatar(avatar);
            GestureAssignmentSettingsWindow.ShowWindow();
        }

        private void OnEnable()
        {
            I18N.Reloaded += Rebuild;
            Undo.undoRedoPerformed += ClearThumbnails;
            EditorApplication.projectChanged += ClearThumbnails;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _preview = new FaceExpressionPreview(Repaint);
            RestorePreview();
        }

        private void OnDisable()
        {
            I18N.Reloaded -= Rebuild;
            Undo.undoRedoPerformed -= ClearThumbnails;
            EditorApplication.projectChanged -= ClearThumbnails;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ClearThumbnails();
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
                RefreshAssignments();
            }
        }

        private void Rebuild()
        {
            if (rootVisualElement.panel == null)
            {
                return;
            }

            BuildContent();
            RefreshAssignments();
        }

        private void BuildContent()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("assignmentWindow.title"));
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(
                root,
                "Editor/UI/Components/Inputs/ui-button.uss",
                "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");

            _view = new GestureAssignmentView(
                CreateText(),
                DrawThumbnail);
            _view.AvatarChanged += SetAvatar;
            _view.ApplyRequested += Apply;
            root.Add(_view);
        }

        private void SetAvatar(GameObject avatar)
        {
            if (_avatar != avatar)
            {
                GestureAssignmentSession.ResetSynchronization();
            }

            _avatar = avatar;
            ClearThumbnails();
            _preview?.SetAvatar(avatar);
            _previewRendererPaths = FaceExpressionClipEditor.GetRendererPaths(avatar);
            RefreshAssignments();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                RestorePreview();
            }
        }

        private void RestorePreview()
        {
            ClearThumbnails();
            _preview?.SetAvatar(_avatar);
            _previewRendererPaths = FaceExpressionClipEditor.GetRendererPaths(_avatar);
        }

        private void DrawThumbnail(AnimationClip clip, Rect rect)
        {
            if (clip == null || _preview == null || _avatar == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
                return;
            }

            var dirtyCount = EditorUtility.GetDirtyCount(clip);
            if (!_thumbnails.TryGetValue(clip, out var entry) ||
                entry.Texture == null ||
                entry.DirtyCount != dirtyCount)
            {
                if (entry.Texture != null)
                {
                    DestroyImmediate(entry.Texture);
                }

                var channels = FaceExpressionClipEditor.Read(
                    _avatar,
                    clip,
                    Array.Empty<string>(),
                    _previewRendererPaths);
                var texture = _preview.RenderThumbnail(channels, 160, 160);
                if (texture != null)
                {
                    texture.hideFlags = HideFlags.HideAndDontSave;
                }

                entry = new ThumbnailEntry(texture, dirtyCount);
                _thumbnails[clip] = entry;
            }

            if (entry.Texture == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
                return;
            }

            GUI.DrawTexture(rect, entry.Texture, ScaleMode.ScaleAndCrop, false);
        }

        private void ClearThumbnails()
        {
            foreach (var entry in _thumbnails.Values)
            {
                if (entry.Texture != null)
                {
                    DestroyImmediate(entry.Texture);
                }
            }

            _thumbnails.Clear();
            _view?.MarkDirtyRepaint();
        }

        private readonly struct ThumbnailEntry
        {
            internal ThumbnailEntry(Texture2D texture, int dirtyCount)
            {
                Texture = texture;
                DirtyCount = dirtyCount;
            }

            internal Texture2D Texture { get; }
            internal int DirtyCount { get; }
        }

        private void RefreshAssignments()
        {
            _view?.SetAvatar(_avatar);
            if (_gateway.TryRead(_avatar, out var configuration))
            {
                GestureAssignmentSession.SetConfiguration(configuration);
                _view?.SetApplyEnabled(true);
                return;
            }

            GestureAssignmentSession.SetConfiguration(
                new FaceExpressionConfiguration(null, null));
            _view?.SetApplyEnabled(
                false,
                I18N.Get("status.descriptorMissing"));
        }

        private void Apply()
        {
            if (_gateway.TryApply(
                    _avatar,
                    GestureAssignmentSession.CreateConfiguration(),
                    out var controller,
                    out var error))
            {
                EditorGUIUtility.PingObject(controller);
                _gateway.TryRead(_avatar, out var saved);
                GestureAssignmentSession.SetConfiguration(saved);
                return;
            }

            ShowNotification(UiTextFactory.CreateGuiContent(I18N.Get(
                "status." + (error ?? "applyFailed"))));
        }

        internal static GestureAssignmentViewText CreateText()
        {
            return new GestureAssignmentViewText
            {
                Avatar = I18N.Get("field.avatar"),
                Apply = I18N.Get("action.apply"),
                LeftHand = I18N.Get("assignments.leftHand"),
                RightHand = I18N.Get("assignments.rightHand"),
                Selection = I18N.Get("assignments.selection"),
                ExpressionSettings = I18N.Get("assignments.expressionSettings"),
                Synchronization = I18N.Get("assignments.synchronization"),
                SynchronizeLeft = I18N.Get("assignments.synchronizeLeft"),
                SynchronizeRight = I18N.Get("assignments.synchronizeRight"),
                GlobalSettings = I18N.Get("assignments.globalSettings"),
                Clip = I18N.Get("field.clip"),
                EnableBlink = I18N.Get("assignments.enableBlink"),
                FixMouth = I18N.Get("assignments.fixMouth"),
                Unassigned = I18N.Get("assignments.unassigned"),
                MenuOnly = I18N.Get("assignments.menuOnly"),
                MenuOnlyHint = I18N.Get("assignments.menuOnlyHint"),
                MenuName = I18N.Get("assignments.menuName"),
                DisableMenuIcons = I18N.Get("assignments.disableMenuIcons"),
                AddMenuExpression = I18N.Get("action.addMenuExpression"),
                Remove = I18N.Get("action.remove"),
                GestureName = gesture => I18N.Get("gesture." + gesture)
            };
        }
    }
}
