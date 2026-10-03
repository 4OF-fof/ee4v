using System;
using System.Collections.Generic;
using System.Linq;
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

        public AvatarInfoView(string avatarName,
            IReadOnlyList<GameObject> warnings,
            AvatarPlayModePerformanceCache.Record record,
            bool mobile, bool hasAao, Action<bool> platformChanged,
            Action<GameObject> openAttachmentSettings)
        {
            UiComposition.Prepare(this, "Editor/Feature/Avatar/AvatarInfo/UI/avatar-info.uss");
            UiComposition.Prepare(this, "Editor/Feature/Avatar/AvatarInfo/UI/avatar-info.uss");
            _mobile = mobile;
            _hasAao = hasAao;
            _platformChanged = platformChanged;
            var content = this;
            content.AddToClassList("ee4v-avatar-info__controls-content");
            content.AddToClassList("ee4v-avatar-info__overview");
            var header = new VisualElement();
            header.AddToClassList("ee4v-avatar-info__overview-header");
            header.Add(UiTextFactory.Create(I18N.Get("workflow.overview.avatar"),
                UiClassNames.SectionTitle));
            content.Add(header);
            AddOverviewFact(content, I18N.Get("workflow.overview.name"),
                avatarName);

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
                warning.Add(UiTextFactory.CreateHelpBox(string.Format(
                    I18N.Get("workflow.overview.attachmentWarning"), prefab.name),
                    HelpBoxMessageType.Warning));
                warning.Add(new UiButton(I18N.Get("workflow.overview.openAttachmentSettings"),
                    () => openAttachmentSettings?.Invoke(prefab)));
                content.Add(warning);
            }

            content.Add(UiTextFactory.Create(I18N.Get("workflow.overview.performance"),
                UiClassNames.SectionTitle,
                "ee4v-avatar-info__overview-section-title"));
            var report = mobile ? record?.Mobile : record?.Desktop;
            var metrics = report?.Metrics ?? Array.Empty<AvatarInfoAnalysis.PerformanceMetric>();
            content.Add(BuildPerformanceSummary(report?.Rating, metrics, record));
            if (report == null)
            {
                content.Add(UiTextFactory.CreateHelpBox(
                    record?.Error != null
                        ? string.Format(I18N.Get("workflow.overview.performanceFailed"), record.Error)
                        : I18N.Get(record == null ? "workflow.overview.playModeRequired"
                            : "workflow.overview.playModeNotCaptured"), HelpBoxMessageType.Info));
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
            AvatarPlayModePerformanceCache.Record record)
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

        private static void AddOverviewFact(VisualElement content,
            string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-avatar-info__overview-fact");
            row.Add(UiTextFactory.Create(label, UiClassNames.SecondaryText,
                "ee4v-avatar-info__overview-fact-label"));
            var valueText = UiTextFactory.Create(value, UiClassNames.FormLabel,
                "ee4v-avatar-info__overview-fact-value");
            valueText.SetWhiteSpace(WhiteSpace.Normal);
            row.Add(valueText);
            content.Add(row);
        }

    }
}
