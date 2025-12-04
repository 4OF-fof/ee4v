using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Dialog {
    public class VerificationResultDialog {
        private IAssetRepository _repository;
        private VerificationResult _result;

        public VisualElement CreateContent(VerificationResult result, IAssetRepository repository) {
            _result = result;
            _repository = repository;

            var content = new VisualElement {
                style = {
                    paddingLeft = 18, paddingRight = 18,
                    paddingTop = 14, paddingBottom = 14,
                    minWidth = 400, maxWidth = 600,
                    maxHeight = 600
                }
            };

            var title = new Label(I18N.Get("UI.AssetManager.Dialog.Verification.Title")) {
                style = {
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6
                }
            };
            content.Add(title);

            var desc = new Label(I18N.Get("UI.AssetManager.Dialog.Verification.Description")) {
                style = { marginBottom = 10, whiteSpace = WhiteSpace.Normal }
            };
            content.Add(desc);

            var scroll = new ScrollView {
                style = {
                    flexGrow = 1,
                    backgroundColor = ColorPreset.TransparentBlack10Style,
                    borderTopWidth = 1, borderBottomWidth = 1,
                    borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = ColorPreset.WindowBorder, borderBottomColor = ColorPreset.WindowBorder,
                    borderLeftColor = ColorPreset.WindowBorder, borderRightColor = ColorPreset.WindowBorder,
                    marginBottom = 10,
                    minHeight = 200
                }
            };
            content.Add(scroll);

            if (_result.MissingInCache.Count > 0)
                AddSection(scroll,
                    I18N.Get("UI.AssetManager.Dialog.Verification.FoundOnDisk", _result.MissingInCache.Count),
                    _result.MissingInCache,
                    id =>
                    {
                        var name = _result.OnDisk.TryGetValue(id, out var m) ? m.Name : id.ToString();
                        return $"(null) -> {name}";
                    },
                    ColorPreset.Success);

            if (_result.MissingOnDisk.Count > 0)
                AddSection(scroll,
                    I18N.Get("UI.AssetManager.Dialog.Verification.MissingOnDisk", _result.MissingOnDisk.Count),
                    _result.MissingOnDisk,
                    id =>
                    {
                        var name = _repository.GetAsset(id)?.Name ?? id.ToString();
                        return $"{name} -> (null)";
                    },
                    ColorPreset.Warning);

            if (_result.Modified.Count > 0)
                AddSection(scroll,
                    I18N.Get("UI.AssetManager.Dialog.Verification.Modified", _result.Modified.Count),
                    _result.Modified,
                    asset =>
                    {
                        var cached = _repository.GetAsset(asset.ID);
                        var cachedName = cached?.Name ?? "(null)";
                        return $"{cachedName} -> {asset.Name}";
                    },
                    ColorPreset.AccentBlue);

            var buttonRow = new VisualElement {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 8 }
            };

            var applyBtn = new Button(() => Apply(content)) {
                text = I18N.Get("UI.AssetManager.Dialog.Verification.Apply"),
                style = { width = 100, backgroundColor = ColorPreset.Success, color = ColorPreset.TextColor }
            };
            buttonRow.Add(applyBtn);

            content.Add(buttonRow);
            return content;
        }

        private static void AddSection<T>(VisualElement container, string title, List<T> items,
            Func<T, string> getLabel, Color color) {
            var header = new Label(title) {
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 8, marginBottom = 4, marginLeft = 4,
                    color = color
                }
            };
            container.Add(header);

            foreach (var item in items) {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 10, marginBottom = 2
                    }
                };

                row.Add(new Label(getLabel(item)));
                container.Add(row);
            }
        }

        private void Apply(VisualElement content) {
            _repository.ApplyVerificationResult(_result);
            CloseDialog(content);
        }

        private static void CloseDialog(VisualElement content) {
            var dialog = content.parent;
            var container = dialog?.parent;
            container?.RemoveFromHierarchy();
        }
    }
}