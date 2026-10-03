using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarInfo
{
    public sealed class AvatarInfoView : VisualElement
    {
        private readonly bool _mobile;
        private readonly bool _hasAao;
        private readonly Action<bool> _platformChanged;
        private readonly AvatarInfoEditOptions _editing;
        private PopupField<AvatarInfoSdkAvatar> _blueprintPicker;
        private readonly Dictionary<AvatarInfoSdkAvatar, string> _blueprintLabels =
            new Dictionary<AvatarInfoSdkAvatar, string>();
        private UiButton _fetchAvatars;
        private MessagePanel _avatarListError;
        private bool _avatarListLoaded;
        private CancellationTokenSource _avatarListRequest;
        private string _selectedBlueprintId;

        public AvatarInfoView(string avatarName, string blueprintId,
            IReadOnlyList<GameObject> warnings,
            AvatarPlayModePerformanceCache.Record record,
            bool mobile, bool hasAao, Action<bool> platformChanged,
            Action<GameObject> openAttachmentSettings,
            AvatarInfoEditOptions editing = null,
            AvatarInfoParameterMemory parameterMemory = null,
            AvatarBuildSizeCache.Record buildSize = null)
        {
            UiComposition.Prepare(this, "Editor/Feature/Avatar/AvatarInfo/UI/avatar-info.uss");
            _mobile = mobile;
            _hasAao = hasAao;
            _platformChanged = platformChanged;
            _editing = editing;
            _selectedBlueprintId = blueprintId;
            RegisterCallback<DetachFromPanelEvent>(_ => CancelAvatarListRequest());
            var content = this;
            content.AddToClassList("ee4v-avatar-info__controls-content");
            content.AddToClassList("ee4v-avatar-info__overview");
            var header = new VisualElement();
            header.AddToClassList("ee4v-avatar-info__overview-header");
            header.Add(UiTextFactory.Create(I18N.Get("workflow.overview.avatar"),
                UiClassNames.SectionTitle));
            content.Add(header);
            AddIdentityControls(content, avatarName, blueprintId);

            if (warnings.Count > 0)
            {
                content.Add(UiTextFactory.Create(I18N.Get("workflow.overview.warnings"),
                    UiClassNames.SectionTitle,
                    "ee4v-avatar-info__overview-section-title"));
            }
            foreach (var prefab in warnings)
            {
                var warning = new VisualElement();
                warning.AddToClassList("ee4v-avatar-info__overview-warning");
                warning.Add(new MessagePanel(new MessagePanelState(string.Empty, string.Format(
                    I18N.Get("workflow.overview.attachmentWarning"), prefab.name),
                    MessageSeverity.Warning)));
                warning.Add(new UiButton(I18N.Get("workflow.overview.openAttachmentSettings"),
                    () => openAttachmentSettings?.Invoke(prefab)));
                content.Add(warning);
            }

            content.Add(UiTextFactory.Create(I18N.Get("workflow.overview.performance"),
                UiClassNames.SectionTitle,
                "ee4v-avatar-info__overview-section-title"));
            var report = mobile ? record?.Mobile : record?.Desktop;
            var metrics = report?.Metrics ?? Array.Empty<AvatarInfoAnalysis.PerformanceMetric>();
            var help = report == null && record?.Error == null
                ? I18N.Get(record == null ? "workflow.overview.playModeRequired"
                    : "workflow.overview.playModeNotCaptured") : null;
            content.Add(BuildPerformanceSummary(report?.Rating, metrics, record, help));
            var memory = record?.ParameterMemory ?? parameterMemory;
            if (memory != null)
                content.Add(BuildParameterMemoryCard(memory, record?.ParameterMemory != null));
            var information = new VisualElement();
            information.AddToClassList("ee4v-avatar-info__performance-grid");
            information.AddToClassList("ee4v-avatar-info__information-grid");
            if (buildSize != null)
                information.Add(CreateInformationCard("workflow.overview.downloadSize", FormatSize(buildSize.DownloadBytes),
                    "overviewDownloadSize", true));
            if (buildSize?.UncompressedBytes != null)
                information.Add(CreateInformationCard("workflow.overview.uncompressedSize", FormatSize(buildSize.UncompressedBytes),
                    "overviewUncompressedSize", true));
            if (record?.CapturedAt != null)
                information.Add(CreateInformationCard("workflow.overview.performanceCapturedAt", FormatCapturedAt(record.CapturedAt),
                    "overviewPerformanceCapturedAt", true, compact: true));
            if (buildSize != null)
                information.Add(CreateInformationCard("workflow.overview.buildCapturedAt", FormatCapturedAt(buildSize.CapturedAt),
                    "overviewBuildCapturedAt", true, compact: true));
            if (information.childCount > 0) content.Add(information);
            if (report == null)
            {
                if (record?.Error != null)
                    content.Add(new MessagePanel(new MessagePanelState(string.Empty,
                        string.Format(I18N.Get("workflow.overview.performanceFailed"), record.Error),
                        MessageSeverity.Error)));
                return;
            }
            foreach (var group in new[] { "rendering", "dynamics", "effects" })
            {
                AddPerformanceGroup(content, group, metrics.Where(metric =>
                    GetPerformanceMetricGroup(metric.Category) == group));
            }
        }

        private VisualElement BuildPerformancePlatformSwitch()
        {
            var tabs = new VisualElement();
            tabs.AddToClassList("ee4v-avatar-info__performance-platform-switch");
            tabs.tooltip = I18N.Get("workflow.overview.platform");
            foreach (var mobile in new[] { false, true })
            {
                var button = new UiButton(I18N.Get(mobile
                    ? "workflow.overview.mobile" : "workflow.overview.desktop"), () =>
                {
                    if (_mobile != mobile) { _platformChanged?.Invoke(mobile); }
                }, variant: UiButtonVariant.Ghost);
                button.name = mobile ? "overviewQuest" : "overviewPc";
                button.AddToClassList("ee4v-avatar-info__performance-platform-button");
                button.EnableInClassList("ee4v-avatar-info__performance-platform-button--active",
                    _mobile == mobile);
                tabs.Add(button);
            }
            return tabs;
        }

        private static string GetPerformanceMetricGroup(string category)
        {
            switch (category)
            {
                case "PolyCount":
                case "SkinnedMeshCount":
                case "MeshCount":
                case "MaterialCount":
                case "BoneCount":
                case "TextureMegabytes": return "rendering";
                case "ParticleSystemCount":
                case "ParticleTotalCount":
                case "LightCount":
                case "AudioSourceCount": return "effects";
                default: return "dynamics";
            }
        }

        private VisualElement BuildPerformanceSummary(string rating,
            IReadOnlyList<AvatarInfoAnalysis.PerformanceMetric> metrics,
            AvatarPlayModePerformanceCache.Record record, string help)
        {
            var summary = new VisualElement();
            summary.AddToClassList("ee4v-avatar-info__performance-summary");
            ApplyPerformanceRatingStyle(summary, rating);
            var heading = new VisualElement();
            heading.AddToClassList("ee4v-avatar-info__performance-summary-heading");
            var title = new VisualElement();
            title.AddToClassList("ee4v-avatar-info__performance-summary-title");
            title.Add(UiTextFactory.Create(I18N.Get("workflow.overview.overallRating"),
                UiClassNames.SecondaryText));
            if (_hasAao || record?.AaoAttached == true)
            {
                var aao = UiTextFactory.Create(I18N.Get("workflow.overview.aaoAttached"),
                    UiClassNames.Badge, "ee4v-avatar-info__performance-aao");
                aao.tooltip = I18N.Get(record?.AaoAttached == true
                    ? "workflow.overview.aaoBuildTooltip" : "workflow.overview.aaoCurrentTooltip");
                title.Add(aao);
            }
            heading.Add(title);
            heading.Add(BuildPerformancePlatformSwitch());
            summary.Add(heading);
            var ratingText = UiTextFactory.Create(GetOverviewRatingLabel(rating),
                UiClassNames.SectionTitle);
            ratingText.SetFontSize(26);
            ratingText.SetColor(GetPerformanceRatingColor(rating));
            summary.Add(ratingText);
            if (!string.IsNullOrEmpty(help))
            {
                var description = UiTextFactory.Create(help, UiClassNames.SecondaryText,
                    "ee4v-avatar-info__performance-summary-help");
                description.SetWhiteSpace(WhiteSpace.Normal);
                summary.Add(description);
            }
            var scale = new VisualElement();
            scale.AddToClassList("ee4v-avatar-info__performance-scale");
            foreach (var level in new[] { "Excellent", "Good", "Medium", "Poor", "VeryPoor" })
            {
                var step = new VisualElement();
                step.AddToClassList("ee4v-avatar-info__performance-scale-step");
                step.tooltip = GetOverviewRatingLabel(level);
                step.style.backgroundColor = GetPerformanceRatingColor(level);
                step.EnableInClassList("ee4v-avatar-info__performance-scale-step--active",
                    rating == level);
                scale.Add(step);
            }
            summary.Add(scale);
            var warningCount = metrics.Count(metric => metric.Rating == "Medium" ||
                metric.Rating == "Poor" || metric.Rating == "VeryPoor");
            if (metrics.Count > 0)
            {
                summary.Add(UiTextFactory.Create(warningCount > 0
                    ? string.Format(I18N.Get("workflow.overview.metricsToReview"), warningCount)
                    : I18N.Get("workflow.overview.metricsWithinBudget"),
                    UiClassNames.SecondaryText));
            }
            return summary;
        }

        private static void AddPerformanceGroup(VisualElement content, string group,
            IEnumerable<AvatarInfoAnalysis.PerformanceMetric> metrics)
        {
            var entries = metrics.ToArray();
            if (entries.Length == 0) { return; }
            content.Add(UiTextFactory.Create(I18N.Get("workflow.overview.group." + group),
                UiClassNames.SectionTitle,
                "ee4v-avatar-info__overview-section-title"));
            var grid = new VisualElement();
            grid.AddToClassList("ee4v-avatar-info__performance-grid");
            foreach (var metric in entries)
            {
                var card = new VisualElement();
                card.AddToClassList("ee4v-avatar-info__performance-card");
                ApplyPerformanceRatingStyle(card, metric.Rating);
                card.Add(UiTextFactory.Create(
                    I18N.Get("workflow.overview.metric." + metric.Category),
                    UiClassNames.SecondaryText,
                    "ee4v-avatar-info__performance-card-label"));
                var valueRow = new VisualElement();
                valueRow.AddToClassList("ee4v-avatar-info__performance-card-values");
                var value = UiTextFactory.Create(metric.Value, UiClassNames.SectionTitle,
                    "ee4v-avatar-info__performance-card-value");
                value.SetFontSize(metric.Value.Length > 9 ? 17 : 21);
                value.SetWhiteSpace(WhiteSpace.Normal);
                valueRow.Add(value);
                var badge = UiTextFactory.Create(GetOverviewRatingLabel(metric.Rating),
                    UiClassNames.SecondaryText,
                    "ee4v-avatar-info__performance-card-rating");
                badge.SetColor(GetPerformanceRatingColor(metric.Rating));
                valueRow.Add(badge);
                card.Add(valueRow);
                if (metric.Amount.HasValue && metric.TargetLimit.HasValue)
                {
                    var track = new VisualElement();
                    track.AddToClassList("ee4v-avatar-info__performance-budget");
                    var fill = new VisualElement();
                    fill.AddToClassList("ee4v-avatar-info__performance-budget-fill");
                    var ratio = metric.TargetLimit.Value > 0
                        ? metric.Amount.Value / metric.TargetLimit.Value
                        : metric.Amount.Value > 0 ? 1 : 0;
                    fill.style.width = Length.Percent(Mathf.Clamp01((float)ratio) * 100f);
                    fill.style.backgroundColor = GetPerformanceRatingColor(metric.Rating);
                    track.Add(fill);
                    card.Add(track);
                    if (metric.Rating != "Excellent")
                    {
                        card.Add(UiTextFactory.Create(string.Format(
                            I18N.Get("workflow.overview.targetBudget"),
                            GetOverviewRatingLabel(metric.TargetRating), metric.TargetLimitLabel),
                            UiClassNames.SecondaryText,
                            "ee4v-avatar-info__performance-card-budget-label"));
                    }
                }
                grid.Add(card);
            }
            content.Add(grid);
        }

        private static Color GetPerformanceRatingColor(string rating)
        {
            switch (rating)
            {
                case "Excellent": return UiColorTokens.StatusPassedText;
                case "Good": return UiColorTokens.StatusPassedText;
                case "Medium": return UiColorTokens.StatusRunningText;
                case "Poor": return UiColorTokens.StatusFailedText;
                case "VeryPoor": return UiColorTokens.Error;
                default: return UiColorTokens.TextMuted;
            }
        }

        private static void ApplyPerformanceRatingStyle(VisualElement element, string rating)
        {
            element.EnableInClassList("ee4v-avatar-info__performance--warning",
                rating == "Medium" || rating == "Poor");
            element.EnableInClassList("ee4v-avatar-info__performance--critical",
                rating == "VeryPoor");
            element.style.borderLeftColor = GetPerformanceRatingColor(rating);
        }

        private static string GetOverviewRatingLabel(string rating)
        {
            switch (rating)
            {
                case "Excellent":
                case "Good":
                case "Medium":
                case "Poor":
                case "VeryPoor":
                    return I18N.Get("workflow.overview.rating." + rating);
                default:
                    return I18N.Get("workflow.overview.rating.Unknown");
            }
        }

        private void AddIdentityControls(VisualElement content, string avatarName, string blueprintId)
        {
            var name = new InputField(new InputFieldState(avatarName))
            {
                name = "overviewName", IsDelayed = true, IsReadOnly = _editing?.Editable != true
            };
            name.ValueChanged += value =>
            {
                var accepted = _editing?.Rename?.Invoke(value) ?? avatarName;
                name.SetValueWithoutNotify(accepted);
            };
            content.Add(new FormInput(I18N.Get("workflow.overview.name"), name));

            var choices = new List<AvatarInfoSdkAvatar> { new AvatarInfoSdkAvatar() };
            if (!string.IsNullOrEmpty(blueprintId)) choices.Add(new AvatarInfoSdkAvatar { Id = blueprintId });
            PrepareBlueprintLabels(choices);
            _blueprintPicker = UiTextFactory.CreatePopupField(string.Empty, choices, choices.Count - 1,
                FormatBlueprintSelection, FormatBlueprintSelection);
            _blueprintPicker.name = "overviewBlueprintSelection";
            _blueprintPicker.SetEnabled(false);
            _blueprintPicker.RegisterValueChangedCallback(evt => SelectBlueprint(evt.newValue));
            _fetchAvatars = new UiButton(I18N.Get("workflow.overview.fetchAvatars"), FetchOwnAvatars,
                variant: UiButtonVariant.Ghost) { name = "overviewFetchAvatars" };
            _fetchAvatars.SetEnabled(_editing?.Editable == true && _editing.SdkAvailable);
            content.Add(new FormInput(I18N.Get("workflow.overview.blueprintSelection"), _blueprintPicker, _fetchAvatars));
            _avatarListError = new MessagePanel(new MessagePanelState(string.Empty))
            {
                name = "overviewAvatarListError"
            };
            _avatarListError.style.display = DisplayStyle.None;
            content.Add(_avatarListError);
        }

        private string FormatBlueprintSelection(AvatarInfoSdkAvatar avatar) =>
            avatar != null && _blueprintLabels.TryGetValue(avatar, out var label) ? label : string.Empty;

        private void PrepareBlueprintLabels(IEnumerable<AvatarInfoSdkAvatar> avatars)
        {
            _blueprintLabels.Clear();
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var avatar in avatars)
            {
                // Unity's native menu interprets '/' as a submenu separator.
                var label = (string.IsNullOrEmpty(avatar.Id) ? I18N.Get("workflow.overview.newAvatar")
                    : string.IsNullOrWhiteSpace(avatar.Name) ? I18N.Get("workflow.overview.assignedAvatar")
                    : avatar.Name).Replace('/', '／');
                var unique = label;
                for (var suffix = 2; !used.Add(unique); suffix++) unique = $"{label} ({suffix})";
                _blueprintLabels[avatar] = unique;
            }
        }

        private void SelectBlueprint(AvatarInfoSdkAvatar avatar)
        {
            if (!_avatarListLoaded || _editing?.Editable != true || !_editing.SdkAvailable)
            {
                RestoreBlueprintSelection();
                return;
            }
            if ((avatar?.Id ?? string.Empty) == (_selectedBlueprintId ?? string.Empty)) return;
            try
            {
                if (_editing?.SelectBlueprint?.Invoke(avatar?.Id) != true)
                {
                    RestoreBlueprintSelection();
                    return;
                }
                _selectedBlueprintId = avatar?.Id;
                _fetchAvatars.tooltip = string.Empty;
            }
            catch (Exception exception)
            {
                RestoreBlueprintSelection();
                _fetchAvatars.tooltip = string.Format(I18N.Get("workflow.overview.avatarListFailed"), exception.Message);
            }
        }

        private void RestoreBlueprintSelection() => _blueprintPicker.SetValueWithoutNotify(
            _blueprintPicker.choices.First(value => (value.Id ?? string.Empty) == (_selectedBlueprintId ?? string.Empty)));

        private async void FetchOwnAvatars()
        {
            if (_avatarListRequest != null || _editing?.FetchAvatars == null) return;
            var request = new CancellationTokenSource();
            _avatarListRequest = request;
            _avatarListLoaded = false;
            _avatarListError.style.display = DisplayStyle.None;
            _blueprintPicker.SetEnabled(false);
            _fetchAvatars.SetEnabled(false);
            _fetchAvatars.SetLabel(I18N.Get("workflow.overview.fetchAvatarsLoading"));
            _fetchAvatars.tooltip = I18N.Get("workflow.overview.avatarListLoading");
            try
            {
                if (_editing.IsLoggedIn?.Invoke() != true)
                {
                    ShowAvatarListFailure(I18N.Get("workflow.overview.sdkLoginRequired"));
                    return;
                }
                var avatars = await _editing.FetchAvatars(request.Token);
                if (request.IsCancellationRequested || _avatarListRequest != request) return;
                var choices = new List<AvatarInfoSdkAvatar> { new AvatarInfoSdkAvatar() };
                choices.AddRange(avatars);
                if (!string.IsNullOrEmpty(_selectedBlueprintId) && !choices.Any(value => value.Id == _selectedBlueprintId))
                    choices.Insert(1, new AvatarInfoSdkAvatar { Id = _selectedBlueprintId });
                PrepareBlueprintLabels(choices);
                _blueprintPicker.choices = choices;
                RestoreBlueprintSelection();
                _avatarListLoaded = true;
                _blueprintPicker.SetEnabled(_editing.Editable && _editing.SdkAvailable);
                _fetchAvatars.tooltip = string.Format(I18N.Get("workflow.overview.avatarListLoaded"), avatars.Count);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (_avatarListRequest == request)
                    ShowAvatarListFailure(string.Format(I18N.Get("workflow.overview.avatarListFailed"), exception.Message));
            }
            finally
            {
                if (_avatarListRequest == request)
                {
                    _avatarListRequest = null;
                    _fetchAvatars.SetLabel(I18N.Get("workflow.overview.fetchAvatars"));
                    _fetchAvatars.SetEnabled(_editing.Editable && _editing.SdkAvailable);
                }
                request.Dispose();
            }
        }

        private void ShowAvatarListFailure(string reason)
        {
            _fetchAvatars.tooltip = reason;
            _avatarListError.SetState(new MessagePanelState(string.Empty,
                I18N.Get("workflow.overview.avatarListOpenControlPanel"), MessageSeverity.Warning));
            _avatarListError.style.display = DisplayStyle.Flex;
        }

        private void CancelAvatarListRequest()
        {
            _avatarListRequest?.Cancel();
            _avatarListRequest = null;
            _fetchAvatars?.SetLabel(I18N.Get("workflow.overview.fetchAvatars"));
            _fetchAvatars?.SetEnabled(_editing?.Editable == true && _editing.SdkAvailable);
        }

        private static string FormatSize(long? bytes) => bytes.HasValue
            ? $"{bytes.Value / (1024d * 1024d):N2} MB" : I18N.Get("workflow.overview.notCaptured");

        private static string FormatCapturedAt(DateTime? time) => time.HasValue
            ? time.Value.ToLocalTime().ToString("yyyy/MM/dd\nHH:mm:ss") : I18N.Get("workflow.overview.notCaptured");

        private static InfoCard CreateInformationCard(string labelKey, string value,
            string name, bool available, bool compact = false)
        {
            var card = new InfoCard(new InfoCardState(value, eyebrow: I18N.Get(labelKey))) { name = name };
            card.AddToClassList("ee4v-avatar-info__performance-card");
            card.AddToClassList("ee4v-avatar-info__information-card");
            card.EyebrowText.AddToClassList("ee4v-avatar-info__performance-card-label");
            card.EyebrowText.SetWhiteSpace(WhiteSpace.Normal);
            card.EyebrowText.SetColor(UiColorTokens.TextMuted);
            card.TitleText.SetWhiteSpace(WhiteSpace.Normal);
            card.TitleText.SetFontSize(compact ? 14 : 21);
            if (!available) card.TitleText.SetColor(UiColorTokens.TextMuted);
            ApplyPerformanceRatingStyle(card, null);
            return card;
        }

        private static VisualElement BuildParameterMemoryCard(AvatarInfoParameterMemory memory, bool built)
        {
            var grid = new VisualElement();
            grid.AddToClassList("ee4v-avatar-info__performance-grid");
            grid.AddToClassList("ee4v-avatar-info__information-grid");
            var card = CreateInformationCard(built ? "workflow.overview.parametersBuilt" : "workflow.overview.parametersCurrent",
                memory == null ? I18N.Get("workflow.overview.notCaptured") : $"{memory.Used:N0} / {memory.Limit:N0} bit",
                "overviewParameters", memory != null);
            card.AddToClassList("ee4v-avatar-info__parameter-card");
            if (memory != null && memory.Limit > 0)
            {
                Color color = memory.Used > memory.Limit ? UiColorTokens.Error : UiColorTokens.StatusPassedText;
                card.style.borderLeftColor = color;
                card.EnableInClassList("ee4v-avatar-info__performance--critical", memory.Used > memory.Limit);
                var track = new VisualElement();
                track.AddToClassList("ee4v-avatar-info__performance-budget");
                var fill = new VisualElement { name = "overviewParameterBudget" };
                fill.AddToClassList("ee4v-avatar-info__performance-budget-fill");
                fill.style.width = Length.Percent(Mathf.Clamp01((float)memory.Used / memory.Limit) * 100f);
                fill.style.backgroundColor = color;
                track.Add(fill);
                card.Body.Add(track);
            }
            grid.Add(card);
            return grid;
        }

    }
}
