using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ee4v.UI
{
    internal sealed partial class AssetPickerWindow
    {
        private VisualElement _animationPropertyPreview;
        private UiTextElement _animationPropertyTitle;
        private UiTextElement _animationTimeValue;
        private Slider _animationTime;
        private AnimationCurveGraph _animationGraph;
        private AnimationProperty _selectedAnimationProperty;
        private readonly List<KeyValuePair<AnimationProperty, NavigationItem>> _animationPropertyRows =
            new List<KeyValuePair<AnimationProperty, NavigationItem>>();

        private VisualElement BuildAnimationDetails()
        {
            var details = new VisualElement();
            details.AddToClassList("ee4v-ui-asset-picker__animation");
            details.AddToClassList("ee4v-ui-asset-picker__hidden");
            _animationSummary = UiTextFactory.Create(string.Empty, "ee4v-ui-asset-picker__animation-summary");
            _animationSummary.SetWhiteSpace(WhiteSpace.Normal);
            _animationSummary.tooltip = UiLocalization.Get("ui.assetPicker.animationKeyframesHint");
            details.Add(_animationSummary);
            _animationPropertyPreview = new VisualElement();
            _animationPropertyPreview.AddToClassList("ee4v-ui-asset-picker__curve-preview");
            _animationPropertyTitle = UiTextFactory.Create(string.Empty, UiClassNames.SectionTitle);
            _animationPropertyTitle.SetWhiteSpace(WhiteSpace.Normal);
            _animationPropertyPreview.Add(_animationPropertyTitle);
            _animationGraph = new AnimationCurveGraph();
            _animationGraph.TimeSelected += time =>
            {
                _animationTime.SetValueWithoutNotify(time);
                RefreshAnimationTime();
            };
            _animationPropertyPreview.Add(_animationGraph);
            _animationTime = UiTextFactory.CreateSlider(UiLocalization.Get("ui.assetPicker.animationTime"), 0f, 1f,
                "ee4v-ui-asset-picker__animation-time");
            _animationTime.RegisterValueChangedCallback(_ => RefreshAnimationTime());
            _animationPropertyPreview.Add(_animationTime);
            _animationTimeValue = UiTextFactory.Create(string.Empty, "ee4v-ui-asset-picker__animation-time-value");
            _animationTimeValue.SetWhiteSpace(WhiteSpace.Normal);
            _animationPropertyPreview.Add(_animationTimeValue);
            details.Add(_animationPropertyPreview);
            details.Add(UiTextFactory.Create(UiLocalization.Get("ui.assetPicker.animationProperties"),
                UiClassNames.SectionTitle, "ee4v-ui-asset-picker__animation-heading"));
            _animationProperties = new ScrollView(ScrollViewMode.Vertical);
            _animationProperties.AddToClassList("ee4v-ui-asset-picker__animation-properties");
            details.Add(_animationProperties);
            return details;
        }

        private void RefreshAnimationDetails(AnimationClip clip)
        {
            var version = AssetPickerCache.Version;
            if (_detailsClip == clip && _detailsVersion == version) return;
            var selectedBinding = _selectedAnimationProperty?.Binding;
            _detailsClip = clip;
            _detailsVersion = version;
            var properties = AnimationUtility.GetCurveBindings(clip)
                .Select(binding => new AnimationProperty(binding, AnimationUtility.GetEditorCurve(clip, binding), null))
                .Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)
                    .Select(binding => new AnimationProperty(binding, null, AnimationUtility.GetObjectReferenceCurve(clip, binding))))
                .ToArray();
            _animationSummary.SetText(string.Format(UiLocalization.Get("ui.assetPicker.animationSummary"),
                FormatTime(clip.length), UiLocalization.Get(clip.isLooping
                    ? "ui.assetPicker.animationLoopOn" : "ui.assetPicker.animationLoopOff"), properties.Sum(property => property.KeyCount)));
            _animationPropertyRows.Clear();
            _animationProperties.Clear();
            _animationProperties.scrollOffset = Vector2.zero;
            foreach (var group in properties.GroupBy(property => new { property.Binding.path, property.Binding.type })
                         .OrderBy(group => group.Key.path, StringComparer.Ordinal)
                         .ThenBy(group => group.Key.type?.FullName, StringComparer.Ordinal))
            {
                var target = UiTextFactory.Create(AnimationTarget(group.Key.path, group.Key.type),
                    UiClassNames.SecondaryText, "ee4v-ui-asset-picker__animation-target");
                target.SetWhiteSpace(WhiteSpace.Normal);
                _animationProperties.Add(target);
                foreach (var property in group.OrderBy(property => property.Binding.propertyName, StringComparer.Ordinal))
                {
                    var row = new NavigationItem(new NavigationItemState(property.Binding.propertyName,
                        property.Describe(clip.length)), () => SelectAnimationProperty(property));
                    row.AddToClassList("ee4v-ui-asset-picker__animation-property");
                    row.Row.TitleText.SetWhiteSpace(WhiteSpace.Normal);
                    row.Row.DescriptionText.SetWhiteSpace(WhiteSpace.Normal);
                    row.tooltip = AnimationTarget(property.Binding.path, property.Binding.type) + "\n" + property.Binding.propertyName;
                    _animationProperties.Add(row);
                    _animationPropertyRows.Add(new KeyValuePair<AnimationProperty, NavigationItem>(property, row));
                }
            }
            _animationTime.highValue = Mathf.Max(clip.length, 0.000001f);
            _animationTime.SetEnabled(clip.length > 0f);
            _animationTime.SetValueWithoutNotify(0f);
            _animationPropertyPreview.EnableInClassList("ee4v-ui-asset-picker__hidden", properties.Length == 0);
            if (properties.Length == 0)
                _animationProperties.Add(UiTextFactory.Create(UiLocalization.Get("ui.assetPicker.animationNoProperties"), UiClassNames.SecondaryText));
            SelectAnimationProperty(properties.FirstOrDefault(property => selectedBinding.HasValue && property.Binding.Equals(selectedBinding.Value))
                ?? properties.FirstOrDefault());
        }

        private void SelectAnimationProperty(AnimationProperty property)
        {
            _selectedAnimationProperty = property;
            foreach (var row in _animationPropertyRows)
                row.Value.SetSelected(row.Key == property);
            _animationPropertyTitle.SetText(property == null ? string.Empty : property.Binding.propertyName);
            _animationPropertyTitle.tooltip = property == null ? string.Empty : AnimationTarget(property.Binding.path, property.Binding.type);
            _animationGraph.SetProperty(property, _detailsClip == null ? 0f : _detailsClip.length);
            RefreshAnimationTime();
        }

        private void RefreshAnimationTime()
        {
            var property = _selectedAnimationProperty;
            var time = _detailsClip == null ? 0f : Mathf.Clamp(_animationTime.value, 0f, _detailsClip.length);
            _animationGraph.SetTime(time);
            _animationTimeValue.SetText(property == null ? string.Empty :
                (property.IsConstant ? UiLocalization.Get("ui.assetPicker.animationFixedState") + " · " : string.Empty) +
                string.Format(UiLocalization.Get("ui.assetPicker.animationTimeValue"), FormatTime(time), property.ValueAt(time)));
        }

        private static string AnimationTarget(string path, Type type) =>
            (string.IsNullOrEmpty(path) ? UiLocalization.Get("ui.assetPicker.animationRoot") : path) + " · " + type?.Name;

        private static string FormatTime(float value) => value.ToString("0.###");
        private static string FormatNumber(float value) => value.ToString("G6");

        private sealed class AnimationProperty
        {
            internal readonly EditorCurveBinding Binding;
            internal readonly AnimationCurve Curve;
            internal readonly Keyframe[] Keys;
            internal readonly ObjectReferenceKeyframe[] ObjectKeys;
            internal readonly bool IsConstant;
            internal int KeyCount => Curve != null ? Keys.Length : ObjectKeys.Length;

            internal AnimationProperty(EditorCurveBinding binding, AnimationCurve curve, ObjectReferenceKeyframe[] objectKeys)
            {
                Binding = binding;
                Curve = curve;
                Keys = curve?.keys ?? Array.Empty<Keyframe>();
                ObjectKeys = objectKeys ?? Array.Empty<ObjectReferenceKeyframe>();
                IsConstant = curve == null
                    ? ObjectKeys.Length > 0 && ObjectKeys.All(key => key.value == ObjectKeys[0].value)
                    : Keys.Length > 0 && Keys.All(key => key.value == Keys[0].value) &&
                      Enumerable.Range(0, Math.Max(0, Keys.Length - 1)).All(index =>
                          float.IsInfinity(Keys[index].outTangent) || float.IsInfinity(Keys[index + 1].inTangent) ||
                          Keys[index].outTangent == 0f && Keys[index + 1].inTangent == 0f);
            }

            internal string ValueAt(float time) => Curve == null ? ObjectName(ObjectAt(time)) : NumericValue(Curve.Evaluate(time));

            internal Object ObjectAt(float time)
            {
                if (ObjectKeys.Length == 0) return null;
                var value = ObjectKeys[0].value;
                foreach (var key in ObjectKeys)
                {
                    if (key.time > time) break;
                    value = key.value;
                }
                return value;
            }

            private string NumericValue(float value) =>
                (Binding.propertyName == "m_IsActive" || Binding.propertyName == "m_Enabled") && (value == 0f || value == 1f)
                    ? UiLocalization.Get(value == 0f ? "ui.assetPicker.animationLoopOff" : "ui.assetPicker.animationLoopOn") + " (" + FormatNumber(value) + ")"
                    : FormatNumber(value);

            private static string ObjectName(Object value) => value == null ? "None" : value.name;

            internal string Describe(float duration)
            {
                if (KeyCount == 0) return UiLocalization.Get("ui.assetPicker.animationNoKeys");
                var text = IsConstant
                    ? string.Format(UiLocalization.Get("ui.assetPicker.animationFixedValue"), ValueAt(0f))
                    : ValueAt(0f) + " → " + ValueAt(duration);
                if (Curve != null && !IsConstant)
                    text += " · " + string.Format(UiLocalization.Get("ui.assetPicker.animationKeyRange"),
                        FormatNumber(Keys.Min(key => key.value)), FormatNumber(Keys.Max(key => key.value)));
                return text + " · " + string.Format(UiLocalization.Get("ui.assetPicker.animationPropertyKeys"), KeyCount);
            }
        }

        private sealed class AnimationCurveGraph : VisualElement
        {
            private AnimationProperty _property;
            private float _duration;
            private float _time;
            private float _minimum;
            private float _maximum;
            private readonly UiTextElement _upper;
            private readonly UiTextElement _lower;
            private readonly UiTextElement _end;
            private Object[] _states = Array.Empty<Object>();
            internal event Action<float> TimeSelected;

            internal AnimationCurveGraph()
            {
                AddToClassList("ee4v-ui-asset-picker__curve-graph");
                tooltip = UiLocalization.Get("ui.assetPicker.animationCurveHint");
                _upper = UiTextFactory.Create(string.Empty, "ee4v-ui-asset-picker__curve-upper");
                _lower = UiTextFactory.Create(string.Empty, "ee4v-ui-asset-picker__curve-lower");
                Add(_upper);
                Add(_lower);
                Add(UiTextFactory.Create("0 s", "ee4v-ui-asset-picker__curve-start"));
                _end = UiTextFactory.Create(string.Empty, "ee4v-ui-asset-picker__curve-end");
                Add(_end);
                generateVisualContent += Draw;
                RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0 || _property == null) return;
                    var rect = PlotRect();
                    var duration = _duration > 0f ? _duration : 1f;
                    var x = evt.localPosition.x;
                    var time = Mathf.Clamp01((x - rect.x) / Mathf.Max(1f, rect.width)) * _duration;
                    var times = _property.Curve == null
                        ? _property.ObjectKeys.Select(key => key.time) : _property.Keys.Select(key => key.time);
                    foreach (var keyTime in times.OrderBy(keyTime => Mathf.Abs(rect.x + keyTime / duration * rect.width - x)))
                    {
                        if (Mathf.Abs(rect.x + keyTime / duration * rect.width - x) <= 8f)
                            time = Mathf.Clamp(keyTime, 0f, _duration);
                        break;
                    }
                    TimeSelected?.Invoke(time);
                    evt.StopPropagation();
                });
            }

            internal void SetProperty(AnimationProperty property, float duration)
            {
                _property = property;
                _duration = Mathf.Max(duration, 0f);
                EnableInClassList("ee4v-ui-asset-picker__curve-graph--objects", property != null && property.Curve == null);
                _states = property?.ObjectKeys.Select(key => key.value).Distinct().ToArray() ?? Array.Empty<Object>();
                var values = property?.Keys.Select(key => key.value).Where(Finite).ToList() ?? new List<float>();
                if (property?.Curve != null && !property.IsConstant)
                    for (var index = 0; index <= 256; index++)
                    {
                        var value = property.Curve.Evaluate(_duration * index / 256f);
                        if (Finite(value)) values.Add(value);
                    }
                _minimum = values.Count == 0 ? 0f : values.Min();
                _maximum = values.Count == 0 ? 1f : values.Max();
                var padding = _maximum == _minimum ? Mathf.Max(1f, Mathf.Abs(_minimum) * 0.1f) : (_maximum - _minimum) * 0.1f;
                _minimum -= padding;
                _maximum += padding;
                if (property?.Curve != null &&
                    (property.Binding.propertyName == "m_IsActive" || property.Binding.propertyName == "m_Enabled") &&
                    values.Count > 0 && values.All(value => value >= 0f && value <= 1f))
                {
                    _minimum = 0f;
                    _maximum = 1f;
                }
                _upper.SetText(property?.Curve == null ? string.Empty : FormatNumber(_maximum));
                _lower.SetText(property?.Curve == null ? string.Empty : FormatNumber(_minimum));
                _upper.tooltip = _upper.Text;
                _lower.tooltip = _lower.Text;
                _end.SetText(_duration > 0f ? FormatTime(_duration) + " s" : UiLocalization.Get("ui.assetPicker.animationFixedState"));
                MarkDirtyRepaint();
            }

            internal void SetTime(float time) { _time = time; MarkDirtyRepaint(); }
            private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
            private Rect PlotRect() => new Rect(_property.Curve == null ? 12f : 58f, 12f,
                contentRect.width - (_property.Curve == null ? 24f : 70f), contentRect.height - 36f);

            private void Draw(MeshGenerationContext context)
            {
                if (_property == null || contentRect.width < 64f || contentRect.height < 32f) return;
                var rect = PlotRect();
                var duration = _duration > 0f ? _duration : 1f;
                Vector2 Position(float time, float value) => new Vector2(rect.x + Mathf.Clamp01(time / duration) * rect.width,
                    rect.yMax - Mathf.Clamp01((value - _minimum) / (_maximum - _minimum)) * rect.height);
                var painter = context.painter2D;
                painter.lineWidth = 1f;
                painter.strokeColor = UiColorTokens.HierarchyDecorationGuide;
                for (var index = 0; index <= 4; index++)
                {
                    var x = rect.x + rect.width * index / 4f;
                    var y = rect.y + rect.height * index / 4f;
                    painter.BeginPath(); painter.MoveTo(new Vector2(x, rect.y)); painter.LineTo(new Vector2(x, rect.yMax)); painter.Stroke();
                    painter.BeginPath(); painter.MoveTo(new Vector2(rect.x, y)); painter.LineTo(new Vector2(rect.xMax, y)); painter.Stroke();
                }
                painter.strokeColor = UiColorTokens.Focus;
                painter.lineWidth = 2f;
                if (_property.Curve != null)
                {
                    var started = false;
                    painter.BeginPath();
                    var samples = _duration == 0f ? 1 : Mathf.Clamp(Mathf.CeilToInt(rect.width), 32, 512);
                    for (var index = 0; index <= samples; index++)
                    {
                        var time = _duration * index / samples;
                        var value = _property.Curve.Evaluate(time);
                        if (!Finite(value)) { started = false; continue; }
                        var point = Position(time, value);
                        if (started) painter.LineTo(point); else painter.MoveTo(point);
                        started = true;
                    }
                    if (_duration == 0f && _property.Keys.Length > 0)
                        painter.LineTo(new Vector2(rect.xMax, Position(0f, _property.Keys[0].value).y));
                    painter.Stroke();
                    foreach (var key in _property.Keys)
                        if (Finite(key.value)) DrawKey(painter, Position(key.time, key.value));
                }
                else if (_property.ObjectKeys.Length > 0)
                {
                    float StateY(Object value) => rect.yMax - rect.height * (Array.IndexOf(_states, value) + 1f) / (_states.Length + 1f);
                    var previous = new Vector2(rect.x, StateY(_property.ObjectKeys[0].value));
                    painter.BeginPath(); painter.MoveTo(previous);
                    foreach (var key in _property.ObjectKeys)
                    {
                        var x = Position(key.time, 0f).x;
                        painter.LineTo(new Vector2(x, previous.y));
                        previous = new Vector2(x, StateY(key.value));
                        painter.LineTo(previous);
                    }
                    painter.LineTo(new Vector2(rect.xMax, previous.y)); painter.Stroke();
                    foreach (var key in _property.ObjectKeys) DrawKey(painter, new Vector2(Position(key.time, 0f).x, StateY(key.value)));
                }
                painter.lineWidth = 1f;
                painter.strokeColor = UiColorTokens.TextPrimary;
                var cursor = Position(_time, 0f).x;
                painter.BeginPath(); painter.MoveTo(new Vector2(cursor, rect.y)); painter.LineTo(new Vector2(cursor, rect.yMax)); painter.Stroke();
            }

            private static void DrawKey(Painter2D painter, Vector2 point)
            {
                painter.fillColor = UiColorTokens.Focus;
                painter.BeginPath();
                painter.MoveTo(point + new Vector2(0f, -3f));
                painter.LineTo(point + new Vector2(3f, 0f));
                painter.LineTo(point + new Vector2(0f, 3f));
                painter.LineTo(point + new Vector2(-3f, 0f));
                painter.ClosePath(); painter.Fill();
            }
        }
    }
}
