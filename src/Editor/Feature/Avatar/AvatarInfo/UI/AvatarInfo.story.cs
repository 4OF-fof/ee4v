using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
                "名前編集、サンプル一覧からのID選択、装着警告とビルド結果を確認できます。SDKへの通信は行いません。",
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
                CapturedAt = DateTime.Now,
                ParameterMemory = new AvatarInfoParameterMemory { Used = 120, Limit = 256 },
                Desktop = SampleReport("Good", "Good", "75,000", 75000, 70000),
                Mobile = SampleReport("VeryPoor", "VeryPoor", "75,000", 75000, 20000)
            };
            var mobile = false;
            var available = true;
            var avatarName = "Sample Avatar";
            var blueprintId = "avtr_00000000-0000-0000-0000-000000000000";
            var editing = new AvatarInfoEditOptions
            {
                Editable = true, SdkAvailable = true, IsLoggedIn = () => true,
                Rename = value => avatarName = string.IsNullOrWhiteSpace(value) ? avatarName : value.Trim(),
                SelectBlueprint = value => { blueprintId = value; return true; },
                FetchAvatars = _ => Task.FromResult<IReadOnlyList<AvatarInfoSdkAvatar>>(new[]
                {
                    new AvatarInfoSdkAvatar { Id = "avtr_00000000-0000-0000-0000-000000000000", Name = "Sample Avatar" },
                    new AvatarInfoSdkAvatar { Id = "avtr_11111111-1111-1111-1111-111111111111", Name = "Another Avatar" }
                })
            };
            var host = new VisualElement();
            void Render()
            {
                host.Clear();
                host.Add(new AvatarInfoView(avatarName, blueprintId,
                    Array.Empty<GameObject>(),
                    available ? record : null, mobile, false,
                    value => { mobile = value; Render(); }, _ => { }, editing,
                    new AvatarInfoParameterMemory { Used = 96, Limit = 256 },
                    available ? new AvatarBuildSizeCache.Record
                    {
                        Mobile = mobile, DownloadBytes = (mobile ? 12 : 38) * 1024L * 1024,
                        UncompressedBytes = (mobile ? 35 : 110) * 1024L * 1024, CapturedAt = DateTime.Now
                    } : null));
            }
            parent.Add(new UiButton("ビルド結果の有無を切り替え", () =>
            {
                available = !available;
                Render();
            }));
            parent.Add(host);
            parent.Insert(1, new UiButton("Blueprint IDの有無を切り替え", () =>
            {
                blueprintId = string.IsNullOrEmpty(blueprintId)
                    ? "avtr_00000000-0000-0000-0000-000000000000" : null;
                Render();
            }));
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
