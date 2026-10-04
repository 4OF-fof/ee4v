using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class ExpressionConversionView : VisualElement
    {
        private readonly GameObject _avatar;
        private readonly AnimationClip _source;
        private readonly string _sourceRevision;
        private readonly EditorCurveBinding[] _bindings;
        private readonly BlendShapeChannel[] _targets;
        private readonly int[] _mapping;
        private readonly Action<AnimationClip> _saved;
        private readonly MessagePanel _feedback;
        private readonly UiButton _saveButton;
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private ExpressionTargetPickerWindow _targetPicker;

        internal ExpressionConversionView(GameObject avatar, AnimationClip source, Action back, Action<AnimationClip> saved)
        {
            _avatar = avatar;
            _source = source;
            _saved = saved;
            AddToClassList("ee4v-expression-conversion");
            RegisterCallback<DetachFromPanelEvent>(_ => _targetPicker?.Close());
            _bindings = AnimationUtility.GetCurveBindings(_source)
                .Where(binding => binding.type == typeof(SkinnedMeshRenderer) &&
                    binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)).ToArray();
            _sourceRevision = FaceExpressionApi.Revision(_source);
            _targets = FaceExpressionClipEditor.Read(_avatar, null, FaceExpressionSettings.GetSeparators(),
                    FaceExpressionClipEditor.GetRendererPaths(_avatar))
                .Where(channel => !channel.IsHeader).ToArray();
            _mapping = new int[_bindings.Length];
            var header = new SectionHeader(I18N.Get("conversion.title"));
            header.AddToClassList("ee4v-face-expression__section-header");
            header.Insert(0, new UiButton(string.Empty, back, I18N.Get("conversion.back"),
                FluentUiIcons.CreateState("arrow_left.png", UiSizeTokens.Size16), UiButtonVariant.Ghost));
            Add(header);
            var context = UiTextFactory.Create(_source.name + " → " + _avatar.name,
                "ee4v-expression-conversion__context");
            context.tooltip = AssetDatabase.GetAssetPath(_source);
            Add(context);
            var columns = new VisualElement();
            columns.AddToClassList("ee4v-expression-conversion__columns");
            columns.Add(UiTextFactory.Create(I18N.Get("conversion.source"), UiClassNames.SectionTitle,
                "ee4v-expression-conversion__source"));
            columns.Add(UiTextFactory.Create(I18N.Get("conversion.target"), UiClassNames.SectionTitle,
                "ee4v-expression-conversion__target"));
            Add(columns);
            var list = new ScrollView();
            list.AddToClassList("ee4v-expression-conversion__list");
            Add(list);
            var choices = new List<string> { I18N.Get("conversion.unmapped"), I18N.Get("conversion.skip") };
            choices.AddRange(_targets.Select(channel => channel.RendererPath + " / " + channel.Name));
            for (var index = 0; index < _bindings.Length; index++)
            {
                var binding = _bindings[index];
                var rowIndex = index;
                var match = Array.FindIndex(_targets, channel => channel.Binding.Equals(binding));
                _mapping[index] = match < 0 ? 0 : match + 2;
                UiButton field = null;
                field = new UiButton(choices[_mapping[index]], () =>
                {
                    _targetPicker = ExpressionTargetPickerWindow.Show(field, _targets, _mapping[rowIndex], selected =>
                    {
                        if (panel == null) { return; }
                        _mapping[rowIndex] = selected;
                        field.SetLabel(choices[selected]);
                        field.tooltip = choices[selected];
                        _feedback.SetState(null);
                        RefreshMapping();
                    });
                }, icon: FluentUiIcons.CreateState("search.png", UiSizeTokens.Size16));
                field.SetContentAlignment(Justify.FlexStart);
                field.LabelText.SetWhiteSpace(WhiteSpace.Normal);
                field.AddToClassList("ee4v-expression-conversion__target");
                field.AddToClassList("ee4v-expression-conversion__target-picker");
                field.tooltip = choices[_mapping[index]];
                var row = new VisualElement();
                row.AddToClassList("ee4v-expression-conversion__row");
                var origin = new VisualElement();
                origin.AddToClassList("ee4v-expression-conversion__source");
                origin.Add(UiTextFactory.Create(binding.propertyName.Substring(11),
                    "ee4v-expression-conversion__name"));
                origin.Add(UiTextFactory.Create(string.IsNullOrEmpty(binding.path) ? _source.name : binding.path,
                    UiClassNames.SecondaryText, "ee4v-expression-conversion__path"));
                row.Add(origin);
                row.Add(field);
                list.Add(row);
                _rows.Add(row);
            }
            _feedback = new MessagePanel();
            _feedback.AddToClassList("ee4v-expression-conversion__feedback");
            Add(_feedback);
            var actions = new ActionBar();
            actions.AddToClassList("ee4v-expression-conversion__actions");
            actions.Leading.Add(new UiButton(I18N.Get("conversion.back"), back, variant: UiButtonVariant.Ghost));
            _saveButton = new UiButton(I18N.Get("conversion.save"), Save);
            actions.Actions.Add(_saveButton);
            Add(actions);
            RefreshMapping();
        }

        private void RefreshMapping()
        {
            var duplicate = _mapping.Where(index => index >= 2).GroupBy(index => index)
                .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
            var unmapped = _mapping.Count(index => index == 0);
            _saveButton.SetEnabled(unmapped == 0 && duplicate.Length == 0 &&
                _mapping.Any(index => index >= 2));
            for (var index = 0; index < _rows.Count; index++)
            {
                var conflicting = duplicate.Contains(_mapping[index]);
                _rows[index].EnableInClassList("ee4v-expression-conversion__row--unmapped", _mapping[index] == 0);
                _rows[index].EnableInClassList("ee4v-expression-conversion__row--conflict", conflicting);
                _rows[index].tooltip = conflicting ? I18N.Get("conversion.duplicate") : string.Empty;
            }
        }

        private void ShowError(string message)
        {
            _feedback.SetState(new MessagePanelState(message));
        }

        private void Save()
        {
            if (_avatar == null || _source == null) { return; }
            if (_sourceRevision != FaceExpressionApi.Revision(_source))
            {
                ShowError(I18N.Get("conversion.sourceChanged"));
                return;
            }
            var selected = _mapping.Where(index => index >= 2).ToArray();
            if (_mapping.Any(index => index == 0) || selected.Length == 0 || selected.Distinct().Count() != selected.Length)
            {
                ShowError(I18N.Get("conversion.invalidMapping"));
                return;
            }
            var path = EditorUtility.SaveFilePanelInProject(I18N.Get("conversion.title"),
                _source.name + "_" + _avatar.name, "anim", I18N.Get("conversion.save"),
                Path.GetDirectoryName(AssetDatabase.GetAssetPath(_source))?.Replace('\\', '/') ?? "Assets");
            if (string.IsNullOrEmpty(path)) { return; }
            if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                ShowError(I18N.Get("conversion.newPathRequired"));
                return;
            }
            AnimationClip converted = null;
            try
            {
                converted = UnityEngine.Object.Instantiate(_source);
                converted.name = Path.GetFileNameWithoutExtension(path);
                foreach (var binding in AnimationUtility.GetCurveBindings(converted))
                { AnimationUtility.SetEditorCurve(converted, binding, null); }
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(converted))
                { AnimationUtility.SetObjectReferenceCurve(converted, binding, null); }
                AnimationUtility.SetAnimationEvents(converted, Array.Empty<AnimationEvent>());
                for (var index = 0; index < _bindings.Length; index++)
                {
                    if (_mapping[index] < 2) { continue; }
                    var target = _targets[_mapping[index] - 2];
                    var renderer = string.IsNullOrEmpty(target.RendererPath)
                        ? _avatar.GetComponent<SkinnedMeshRenderer>()
                        : _avatar.transform.Find(target.RendererPath)?.GetComponent<SkinnedMeshRenderer>();
                    if (renderer?.sharedMesh == null || renderer.sharedMesh.GetBlendShapeIndex(target.Name) < 0)
                    { throw new InvalidOperationException(I18N.Get("conversion.targetChanged")); }
                    AnimationUtility.SetEditorCurve(converted, target.Binding,
                        AnimationUtility.GetEditorCurve(_source, _bindings[index]));
                }
                AssetDatabase.CreateAsset(converted, path);
                AssetDatabase.SaveAssets();
                _saved?.Invoke(converted);
            }
            catch (Exception exception)
            {
                if (converted != null && !EditorUtility.IsPersistent(converted)) { UnityEngine.Object.DestroyImmediate(converted); }
                ShowError(exception.GetBaseException().Message);
            }
        }
    }
}
