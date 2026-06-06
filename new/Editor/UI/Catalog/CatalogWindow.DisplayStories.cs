using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private void BuildInfoCardStory(VisualElement parent)
        {
            var preset = InfoCardStoryPreset.Simple;
            var eyebrow = string.Empty;
            var title = "Feature Test Manager";
            var description = string.Empty;
            var badgeText = string.Empty;
            var bodyText = "カードは単体の情報表示面や、設定グループの土台として使えます。";
            Action refresh = null;

            Action<InfoCardStoryPreset> applyPreset = selectedPreset =>
            {
                preset = selectedPreset;
                switch (selectedPreset)
                {
                    case InfoCardStoryPreset.Result:
                        eyebrow = "I18N";
                        title = "解析結果";
                        description = "件数付きの結果カードとして使う用途を想定した preset です。";
                        badgeText = "12";
                        bodyText = "不足キー 8 件\n未参照エントリ 4 件";
                        break;
                    default:
                        eyebrow = string.Empty;
                        title = "Feature Test Manager";
                        description = string.Empty;
                        badgeText = string.Empty;
                        bodyText = "カードは単体の情報表示面や、設定グループの土台として使えます。";
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "InfoCard の各プロパティを編集し、値の有無ごとの見た目を確認します。");

            var eyebrowField = AddTextField(controls.Content, "Eyebrow", eyebrow, value =>
            {
                eyebrow = value;
                refresh();
            });
            var titleField = AddTextField(controls.Content, "タイトル（必須）", title, value =>
            {
                title = value;
                refresh();
            });
            var descriptionField = AddTextField(controls.Content, "説明", description, value =>
            {
                description = value;
                refresh();
            });
            var badgeField = AddTextField(controls.Content, "バッジ", badgeText, value =>
            {
                badgeText = value;
                refresh();
            });
            var bodyTextField = AddTextField(controls.Content, "本文テキスト", bodyText, value =>
            {
                bodyText = value;
                refresh();
            }, true);

            var preview = CreatePreviewSection(parent);
            var card = new InfoCard();
            preview.Body.Add(card);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(InfoCardStoryPreset.Simple.ToString(), "Simple"),
                            new TabCardTabState(InfoCardStoryPreset.Result.ToString(), "Result")
                        },
                        preset.ToString()),
                    id => applyPreset((InfoCardStoryPreset)Enum.Parse(typeof(InfoCardStoryPreset), id)));

                eyebrowField.SetValueWithoutNotify(eyebrow);
                titleField.SetValueWithoutNotify(title);
                descriptionField.SetValueWithoutNotify(description);
                badgeField.SetValueWithoutNotify(badgeText);
                bodyTextField.SetValueWithoutNotify(bodyText);

                card.SetState(new InfoCardState(title, description, eyebrow, badgeText));
                card.Body.Clear();

                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    var bodyLabel = UiTextFactory.Create(bodyText);
                    bodyLabel.SetWhiteSpace(WhiteSpace.Normal);
                    card.Body.Add(bodyLabel);
                }
            };

            applyPreset(preset);
            FinalizeControlsSection(parent, controls);
        }

        private void BuildCopyableTextAreaStory(VisualElement parent)
        {
            var text = "ja-JP/Core: testing.window.failureDetailsTitle (Editor/Core/Localization/ja-JP/core.jsonc)\n" +
                       "en-US/Core: testing.window.copy (Editor/Core/Localization/en-US/core.jsonc)";
            var buttonText = "Copy";
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "表示する長文と button text を変えながら、詳細結果表示用 text area を確認します。");
            var textField = AddTextField(controls.Content, "Text", text, nextValue =>
            {
                text = nextValue;
                refresh();
            }, true);
            var buttonField = AddTextField(controls.Content, "Button", buttonText, nextValue =>
            {
                buttonText = nextValue;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var textArea = new CopyableTextArea();
            preview.Body.Add(textArea);

            refresh = () =>
            {
                textField.SetValueWithoutNotify(text);
                buttonField.SetValueWithoutNotify(buttonText);
                textArea.SetState(new CopyableTextAreaState(text, buttonText));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private void BuildAlertsStory(VisualElement parent)
        {
            var tone = UiBannerTone.Info;
            var title = "情報表示";
            var message = "非ブロッキングな案内やエラー通知に使います。";
            Action refresh = null;
            Action<UiBannerTone> applyPreset = selectedTone =>
            {
                tone = selectedTone;
                switch (selectedTone)
                {
                    case UiBannerTone.Warning:
                        title = "警告表示";
                        message = "確認が必要な状態や注意喚起に使います。";
                        break;
                    case UiBannerTone.Error:
                        title = "エラー表示";
                        message = "処理失敗や設定不備など、強く伝える必要がある状態に使います。";
                        break;
                    default:
                        title = "情報表示";
                        message = "非ブロッキングな案内やエラー通知に使います。";
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "タイトル、メッセージ、tone を切り替えて通知の見た目を確認します。");

            var toneField = AddEnumField(controls.Content, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });
            var titleField = AddTextField(controls.Content, "タイトル", title, value =>
            {
                title = value;
                refresh();
            });
            var messageField = AddTextField(controls.Content, "メッセージ", message, value =>
            {
                message = value;
                refresh();
            }, true);

            var preview = CreatePreviewSection(parent);
            var alerts = new Alerts();
            preview.Body.Add(CreatePreviewSurface(alerts, true));

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(UiBannerTone.Info.ToString(), "Info"),
                            new TabCardTabState(UiBannerTone.Warning.ToString(), "Warning"),
                            new TabCardTabState(UiBannerTone.Error.ToString(), "Error")
                        },
                        tone.ToString()),
                    id => applyPreset((UiBannerTone)Enum.Parse(typeof(UiBannerTone), id)));

                toneField.SetValueWithoutNotify((Enum)(object)tone);
                titleField.SetValueWithoutNotify(title);
                messageField.SetValueWithoutNotify(message);
                alerts.SetState(new AlertsState(tone, title, message));
            };

            applyPreset(tone);
            FinalizeControlsSection(parent, controls);
        }

        private void BuildStatusBadgeStory(VisualElement parent)
        {
            var text = I18N.Get("catalog.status.running");
            var tone = UiStatusTone.Running;
            Action refresh = null;
            Action<UiStatusTone> applyPreset = selectedTone =>
            {
                tone = selectedTone;
                switch (selectedTone)
                {
                    case UiStatusTone.Passed:
                        text = I18N.Get("catalog.status.passed");
                        break;
                    case UiStatusTone.Failed:
                        text = I18N.Get("catalog.status.failed");
                        break;
                    case UiStatusTone.Skipped:
                        text = I18N.Get("catalog.status.skipped");
                        break;
                    case UiStatusTone.Inconclusive:
                        text = I18N.Get("catalog.status.inconclusive");
                        break;
                    case UiStatusTone.Idle:
                        text = I18N.Get("catalog.status.idle");
                        break;
                    default:
                        text = I18N.Get("catalog.status.running");
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "状態テキストと tone を切り替えて badge の見た目を確認します。");

            var textField = AddTextField(controls.Content, "テキスト", text, value =>
            {
                text = value;
                refresh();
            });
            var toneField = AddEnumField(controls.Content, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var badge = new StatusBadge();
            var surface = CreatePreviewSurface(true);
            surface.Add(badge);
            preview.Body.Add(surface);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(UiStatusTone.Idle.ToString(), "Idle"),
                            new TabCardTabState(UiStatusTone.Running.ToString(), "Running"),
                            new TabCardTabState(UiStatusTone.Passed.ToString(), "Passed"),
                            new TabCardTabState(UiStatusTone.Failed.ToString(), "Failed"),
                            new TabCardTabState(UiStatusTone.Skipped.ToString(), "Skipped"),
                            new TabCardTabState(UiStatusTone.Inconclusive.ToString(), "Inconclusive")
                        },
                        tone.ToString()),
                    id => applyPreset((UiStatusTone)Enum.Parse(typeof(UiStatusTone), id)));

                textField.SetValueWithoutNotify(text);
                toneField.SetValueWithoutNotify((Enum)(object)tone);
                badge.SetState(new StatusBadgeState(text, tone));
            };

            applyPreset(tone);
            FinalizeControlsSection(parent, controls);
        }

        private void BuildIconStory(VisualElement parent)
        {
            var sourceKind = UiIconSourceKind.Builtin;
            var builtinIcon = UiBuiltinIcon.Search;
            Texture texture = null;
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "source を切り替え、texture 指定と enum 管理の Unity 内蔵アイコン指定を確認します。");

            var sourceField = AddEnumField(controls.Content, "ソース", sourceKind, value =>
            {
                sourceKind = value;
                refresh();
            });
            var builtinField = AddEnumField(controls.Content, "内蔵アイコン", builtinIcon, value =>
            {
                builtinIcon = value;
                refresh();
            });
            var textureField = AddObjectField<Texture>(controls.Content, "Texture", texture, value =>
            {
                texture = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            var icon = new Icon();
            surface.Add(icon);
            preview.Body.Add(surface);

            refresh = () =>
            {
                sourceField.SetValueWithoutNotify((Enum)(object)sourceKind);
                builtinField.SetValueWithoutNotify((Enum)(object)builtinIcon);
                textureField.SetValueWithoutNotify(texture);

                builtinField.style.display = sourceKind == UiIconSourceKind.Builtin ? DisplayStyle.Flex : DisplayStyle.None;
                textureField.style.display = sourceKind == UiIconSourceKind.Texture ? DisplayStyle.Flex : DisplayStyle.None;

                switch (sourceKind)
                {
                    case UiIconSourceKind.Texture:
                        icon.SetState(texture != null
                            ? IconState.FromTexture(texture, tooltip: texture.name)
                            : IconState.FromBuiltinIcon(builtinIcon, tooltip: "Assign a texture"));
                        break;
                    case UiIconSourceKind.Builtin:
                        icon.SetState(IconState.FromBuiltinIcon(builtinIcon, tooltip: UiBuiltinIconResolver.GetIconName(builtinIcon)));
                        break;
                }
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private void BuildAssetManagerItemCardStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.flexDirection = FlexDirection.Row;
            surface.style.alignItems = Align.FlexStart;
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 12f;
            surface.style.paddingBottom = 12f;

            var thumbnail = CreateItemCardSampleThumbnail(132, 132);
            var itemCard = new ItemCard(new ItemCardState("Sample Avatar Asset", thumbnail));
            itemCard.style.marginRight = 16f;
            surface.Add(itemCard);
            surface.Add(new ItemCard(new ItemCardState("No Thumbnail Item")));

            preview.Body.Add(surface);
        }

        private static Texture2D CreateItemCardSampleThumbnail(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var horizontal = (byte)Mathf.RoundToInt(Mathf.Lerp(72f, 42f, (float)x / Mathf.Max(1, width - 1)));
                    var vertical = (byte)Mathf.RoundToInt(Mathf.Lerp(56f, 92f, (float)y / Mathf.Max(1, height - 1)));
                    var accent = ((x / 16) + (y / 16)) % 2 == 0 ? (byte)125 : (byte)95;
                    pixels[(y * width) + x] = new Color32(horizontal, vertical, accent, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
