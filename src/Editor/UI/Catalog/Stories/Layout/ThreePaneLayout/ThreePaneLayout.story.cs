using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ThreePaneLayoutCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 45; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Layout/ThreePaneLayout/three-pane-layout.uss");
                registry.RegisterStory(new StoryRegistration(
                    "three-pane-layout",
                    "Layout",
                    "ThreePaneLayout",
                    "左右の補助ペインと中央の main 領域を持つ汎用 layout component です。",
                    "上段 toolbar と下段 pane を left / main / right で同期して組み立てます。左右ペインは split bar の drag で幅変更でき、toolbar 上の button で折りたためます。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildThreePaneLayoutStory(parent)));
            }
        }

        private void BuildThreePaneLayoutStory(VisualElement parent)
        {
            var leftWidth = 240f;
            var rightWidth = 280f;
            var leftMinWidth = 180f;
            var leftMaxWidth = 320f;
            var mainMinWidth = 360f;
            var rightMinWidth = 220f;
            var rightMaxWidth = 360f;
            var leftCollapsed = false;
            var rightCollapsed = false;
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "左右ペインの幅、min/max、折りたたみ状態を変更して layout の制約を確認します。");

            var leftWidthField = CreateFloatField("Left Width", leftWidth, value =>
            {
                leftWidth = Mathf.Max(0f, value);
                refresh();
            });
            controls.Content.Add(leftWidthField);

            var leftMinWidthField = CreateFloatField("Left Min", leftMinWidth, value =>
            {
                leftMinWidth = Mathf.Max(0f, value);
                refresh();
            });
            controls.Content.Add(leftMinWidthField);

            var leftMaxWidthField = CreateFloatField("Left Max", leftMaxWidth, value =>
            {
                leftMaxWidth = Mathf.Max(0f, value);
                refresh();
            });
            controls.Content.Add(leftMaxWidthField);

            var rightWidthField = CreateFloatField("Right Width", rightWidth, value =>
            {
                rightWidth = Mathf.Max(0f, value);
                refresh();
            });
            controls.Content.Add(rightWidthField);

            var rightMinWidthField = CreateFloatField("Right Min", rightMinWidth, value =>
            {
                rightMinWidth = Mathf.Max(0f, value);
                refresh();
            });
            controls.Content.Add(rightMinWidthField);

            var rightMaxWidthField = CreateFloatField("Right Max", rightMaxWidth, value =>
            {
                rightMaxWidth = Mathf.Max(0f, value);
                refresh();
            });
            controls.Content.Add(rightMaxWidthField);

            var mainMinWidthField = CreateFloatField("Main Min", mainMinWidth, value =>
            {
                mainMinWidth = Mathf.Max(0f, value);
                refresh();
            });
            controls.Content.Add(mainMinWidthField);

            var leftCollapsedToggle = new Toggle("Left Collapsed")
            {
                value = leftCollapsed
            };
            leftCollapsedToggle.RegisterValueChangedCallback(evt =>
            {
                leftCollapsed = evt.newValue;
                refresh();
            });
            controls.Content.Add(leftCollapsedToggle);

            var rightCollapsedToggle = new Toggle("Right Collapsed")
            {
                value = rightCollapsed
            };
            rightCollapsedToggle.RegisterValueChangedCallback(evt =>
            {
                rightCollapsed = evt.newValue;
                refresh();
            });
            controls.Content.Add(rightCollapsedToggle);

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.paddingLeft = UiSpacingTokens.None;
            surface.style.paddingRight = UiSpacingTokens.None;
            surface.style.paddingTop = UiSpacingTokens.None;
            surface.style.paddingBottom = UiSpacingTokens.None;
            surface.style.height = 360f;

            var layout = new ThreePaneLayout();
            layout.style.flexGrow = 1f;
            layout.LeftToolbarContent.Add(CreateToolbarSample("Left tools"));
            layout.MainToolbarContent.Add(CreateToolbarSample("Main tools"));
            layout.RightToolbarContent.Add(CreateToolbarSample("Right tools"));
            layout.LeftPaneContent.Add(CreatePaneSample("Left", "Tree, filter, or navigation content"));
            layout.MainContent.Add(CreatePaneSample("Main", "Compose toolbar and primary content here"));
            layout.RightPaneContent.Add(CreatePaneSample("Right", "Inspector, properties, or details content"));
            layout.LeftPaneWidthChanged += value =>
            {
                leftWidth = value;
                leftWidthField.SetValueWithoutNotify(value);
            };
            layout.RightPaneWidthChanged += value =>
            {
                rightWidth = value;
                rightWidthField.SetValueWithoutNotify(value);
            };
            layout.LeftCollapsedChanged += value =>
            {
                leftCollapsed = value;
                leftCollapsedToggle.SetValueWithoutNotify(value);
            };
            layout.RightCollapsedChanged += value =>
            {
                rightCollapsed = value;
                rightCollapsedToggle.SetValueWithoutNotify(value);
            };

            surface.Add(layout);
            preview.Body.Add(surface);

            refresh = () =>
            {
                leftWidthField.SetValueWithoutNotify(leftWidth);
                leftMinWidthField.SetValueWithoutNotify(leftMinWidth);
                leftMaxWidthField.SetValueWithoutNotify(leftMaxWidth);
                rightWidthField.SetValueWithoutNotify(rightWidth);
                rightMinWidthField.SetValueWithoutNotify(rightMinWidth);
                rightMaxWidthField.SetValueWithoutNotify(rightMaxWidth);
                mainMinWidthField.SetValueWithoutNotify(mainMinWidth);
                leftCollapsedToggle.SetValueWithoutNotify(leftCollapsed);
                rightCollapsedToggle.SetValueWithoutNotify(rightCollapsed);

                layout.SetState(new ThreePaneLayoutState(
                    leftWidth,
                    rightWidth,
                    leftMinWidth,
                    leftMaxWidth,
                    mainMinWidth,
                    rightMinWidth,
                    rightMaxWidth,
                    leftCollapsed,
                    rightCollapsed));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private static FloatField CreateFloatField(string label, float value, Action<float> onValueChanged)
        {
            var field = new FloatField(label)
            {
                value = value
            };
            field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return field;
        }

        private static VisualElement CreatePaneSample(string title, string body)
        {
            var container = new VisualElement();
            container.style.flexGrow = 1f;
            container.style.minWidth = 0f;
            container.style.minHeight = 0f;

            var titleLabel = UiTextFactory.Create(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = UiSpacingTokens.Medium;
            container.Add(titleLabel);

            var bodyLabel = UiTextFactory.Create(body);
            bodyLabel.SetWhiteSpace(WhiteSpace.Normal);
            container.Add(bodyLabel);
            return container;
        }

        private static VisualElement CreateToolbarSample(string text)
        {
            var container = new VisualElement();
            container.style.flexGrow = 1f;
            container.style.minWidth = 0f;
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var label = UiTextFactory.Create(text);
            label.SetWhiteSpace(WhiteSpace.NoWrap);
            container.Add(label);
            return container;
        }
    }
}
