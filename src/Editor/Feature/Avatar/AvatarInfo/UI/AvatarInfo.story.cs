using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarInfo
{
    internal sealed class AvatarInfoStoryProvider : IUiStoryProvider
    {
        public int Order => 204;
        public IReadOnlyList<UiStory> GetStories()
        {
            return new[] { new UiStory("avatar-info", "Domain/AvatarInfo/Displays", "AvatarInfoView",
                "装着警告とビルド結果を表示します。PC／Questの切り替えを確認できます。",
                "AvatarInfoの詳細表示です。", BuildOverview,
                usageLocations: new[] { "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs",
                    "Editor/Feature/Avatar/AvatarInfo/UI/AvatarInfoWindow.cs" },
                styleSheetPaths: new[] { "Editor/Feature/Avatar/AvatarInfo/UI/avatar-info.uss" }) };
        }
        private static void BuildOverview(VisualElement parent)
        {
            var record = new AvatarPlayModePerformanceCache.Record
            {
                AaoAttached = true,
                Desktop = SampleReport("Good", "Good", "75,000", 75000, 70000),
                Mobile = SampleReport("VeryPoor", "VeryPoor", "75,000", 75000, 20000)
            };
            var mobile = false;
            var available = true;
            var host = new VisualElement();
            void Render()
            {
                host.Clear();
                host.Add(new AvatarInfoView("Sample Avatar", Array.Empty<GameObject>(),
                    available ? record : null, mobile, false,
                    value => { mobile = value; Render(); }, _ => { }));
            }
            parent.Add(new UiButton("ビルド結果の有無を切り替え", () =>
            {
                available = !available;
                Render();
            }));
            parent.Add(host);
            Render();
        }

        private static AvatarInfoAnalysis.PerformanceReport SampleReport(
            string rating, string metricRating, string value, double amount, double limit)
        {
            return new AvatarInfoAnalysis.PerformanceReport
            {
                Rating = rating,
                Metrics = new[]
                {
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "PolyCount", Value = value, Rating = metricRating,
                        Amount = amount, TargetRating = "Good", TargetLimit = limit,
                        TargetLimitLabel = limit.ToString("N0")
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "PhysBoneComponentCount", Value = "8", Rating = "Medium",
                        Amount = 8, TargetRating = "Good", TargetLimit = 4, TargetLimitLabel = "4"
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "ParticleSystemCount", Value = "0", Rating = "Excellent",
                        Amount = 0, TargetRating = "Excellent", TargetLimit = 0, TargetLimitLabel = "0"
                    }
                }
            };
        }

    }
}
