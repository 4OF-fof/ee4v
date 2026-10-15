using System;
using System.Collections.Generic;
using System.Linq;
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
            return new[] { new UiStory("avatar-info", "Domain/AvatarInfo", "AvatarInfoView",
                "名前編集、サンプル一覧からのID選択、装着警告、パラメーター使用量、手動測定、ツリーマップ・Prefab別カード・ボーンとPhysBoneの親子ツリー・比較バー・Constraintの種類別表示と依存グラフ、取得日時を確認できます。SDKへの通信は行いません。",
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
                ParameterMemory = SampleParameterMemory(false),
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
                    SampleParameterMemory(true),
                    available ? new AvatarBuildSizeCache.Record
                    {
                        Mobile = mobile, DownloadBytes = (mobile ? 12 : 38) * 1024L * 1024,
                        UncompressedBytes = (mobile ? 35 : 110) * 1024L * 1024, CapturedAt = DateTime.Now
                    } : null, () => { available = true; Render(); }, true));
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

        private static AvatarInfoParameterMemory SampleParameterMemory(bool estimated)
        {
            return new AvatarInfoParameterMemory
            {
                Used = estimated ? 96 : 120, Limit = 256, ItemsEstimated = estimated,
                Items = new[]
                {
                    new AvatarInfoParameterItemUsage { Name = "Sample Outfit", Path = "Sample Outfit [2]", Used = 64 },
                    new AvatarInfoParameterItemUsage { Name = "Sample Gimmick", Path = "Sample Gimmick [3]", Used = 24 },
                    new AvatarInfoParameterItemUsage { Path = string.Empty, Used = 24 },
                    new AvatarInfoParameterItemUsage { Path = null, Used = 8 }
                }
            };
        }

        private static AvatarInfoAnalysis.PerformanceReport SampleReport(
            string rating, string metricRating, string value, double amount, double limit)
        {
            var report = new AvatarInfoAnalysis.PerformanceReport
            {
                Rating = rating,
                Metrics = new[]
                {
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "PolyCount", Value = value, Rating = metricRating,
                        Amount = amount, TargetRating = "Good", TargetLimit = limit,
                        TargetLimitLabel = limit.ToString("N0"), DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.Mesh,
                        Details = new[]
                        {
                            new AvatarInfoAnalysis.PerformanceDetail
                            { Name = "Body", Path = "Sample Avatar[0]/Body[0]", DisplayPath = "Sample Avatar/Body", Amount = 40000, Value = "40,000", ItemPath = "" },
                            new AvatarInfoAnalysis.PerformanceDetail
                            { Name = "Outfit", Path = "Sample Avatar[0]/Sample Outfit[2]/Outfit[0]", DisplayPath = "Sample Avatar/Sample Outfit/Outfit", Amount = 22000, Value = "22,000", ItemName = "Sample Outfit", ItemPath = "Sample Outfit[2]" },
                            new AvatarInfoAnalysis.PerformanceDetail
                            { Name = "Hair", Path = "Sample Avatar[0]/Hair[1]", DisplayPath = "Sample Avatar/Hair", Amount = 12000, Value = "12,000", ItemPath = "" },
                            new AvatarInfoAnalysis.PerformanceDetail
                            { Name = "Accessory", Path = "Sample Avatar[0]/Sample Outfit[2]/Accessory[1]", DisplayPath = "Sample Avatar/Sample Outfit/Accessory", Amount = 1000, Value = "1,000", ItemName = "Sample Outfit", ItemPath = "Sample Outfit[2]" }
                        }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "SkinnedMeshCount", Value = "3", Rating = "Excellent",
                        Amount = 3, TargetRating = "Excellent", TargetLimit = 6, TargetLimitLabel = "6",
                        DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.Renderer,
                        Details = new[]
                        {
                            new AvatarInfoAnalysis.PerformanceDetail
                            { Name = "Body", Path = "Sample Avatar[0]/Body[0]", Amount = 1, Value = "1", ItemPath = "" },
                            new AvatarInfoAnalysis.PerformanceDetail
                            { Name = "Hair", Path = "Sample Avatar[0]/Hair[1]", Amount = 1, Value = "1", ItemPath = "" },
                            new AvatarInfoAnalysis.PerformanceDetail
                            { Name = "Outfit", Path = "Sample Avatar[0]/Sample Outfit[2]/Outfit[0]", Amount = 1, Value = "1", ItemName = "Sample Outfit", ItemPath = "Sample Outfit[2]" }
                        }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "MaterialCount", Value = "10", Rating = "Good", Amount = 10,
                        DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.Renderer,
                        Details = new[]
                        {
                            new AvatarInfoAnalysis.PerformanceDetail
                            { Name = "Body", Path = "Sample Avatar[0]/Body[0] / SkinnedMeshRenderer[0]", Amount = 4, Value = "4", ItemPath = "" },
                            new AvatarInfoAnalysis.PerformanceDetail
                            { Name = "Hair", Path = "Sample Avatar[0]/Hair[1] / SkinnedMeshRenderer[0]", Amount = 2, Value = "2", ItemPath = "" },
                            new AvatarInfoAnalysis.PerformanceDetail
                            { Name = "Outfit", Path = "Sample Avatar[0]/Sample Outfit[2]/Outfit[0] / SkinnedMeshRenderer[0]", Amount = 4, Value = "4", ItemName = "Sample Outfit", ItemPath = "Sample Outfit[2]" }
                        }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "BoneCount", Value = "90", Rating = "Excellent", Amount = 90,
                        DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.Prefab,
                        Details = new[]
                        {
                            SampleBonePrefab("Sample Avatar", "", 64),
                            SampleBonePrefab("Sample Outfit", "Sample Outfit[2]", 18),
                            SampleBonePrefab("Sample Gimmick", "Sample Gimmick[3]", 8)
                        }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "TextureMegabytes", Value = "36.00 MB", Rating = "Excellent", Amount = 36,
                        DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.Texture,
                        Details = new[]
                        {
                            new AvatarInfoAnalysis.PerformanceDetail
                            {
                                Name = "Body Texture", Path = "Sample Textures/Body Texture", Amount = 24, Value = "24.00 MB",
                                Children = new[]
                                {
                                    new AvatarInfoAnalysis.PerformanceDetail { Name = "Body", Path = "Sample Avatar[0]/Body[0]" },
                                    new AvatarInfoAnalysis.PerformanceDetail { Name = "Hair", Path = "Sample Avatar[0]/Hair[1]" }
                                }
                            },
                            new AvatarInfoAnalysis.PerformanceDetail
                            {
                                Name = "Outfit Texture", Path = "Sample Textures/Outfit Texture", Amount = 12, Value = "12.00 MB",
                                Children = new[] { new AvatarInfoAnalysis.PerformanceDetail
                                    { Name = "Outfit", Path = "Sample Avatar[0]/Sample Outfit[2]/Outfit[0]" } }
                            }
                        }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "PhysBoneComponentCount", Value = "8", Rating = "Medium",
                        Amount = 8, TargetRating = "Good", TargetLimit = 4, TargetLimitLabel = "4",
                        DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.Prefab,
                        Details = new[] { SamplePhysBonePrefab("Sample Avatar", "", 5, 1),
                            SamplePhysBonePrefab("Sample Outfit", "Sample Outfit[2]", 3, 1) }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "PhysBoneTransformCount", Value = "128", Rating = "Medium", Amount = 128,
                        DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.Prefab,
                        Details = new[] { SamplePhysBonePrefab("Sample Avatar", "", 5, 16),
                            SamplePhysBonePrefab("Sample Outfit", "Sample Outfit[2]", 3, 16) }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "PhysBoneCollisionCheckCount", Value = "256", Rating = "Medium", Amount = 256,
                        DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.Prefab,
                        Details = new[] { SamplePhysBonePrefab("Sample Avatar", "", 5, 32),
                            SamplePhysBonePrefab("Sample Outfit", "Sample Outfit[2]", 3, 32) }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "PhysBoneColliderCount", Value = "2", Rating = "Excellent", Amount = 2,
                        DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.Collider,
                        Details = new[]
                        {
                            new AvatarInfoAnalysis.PerformanceDetail
                            {
                                Name = "Head Collider", Path = "Sample Avatar[0]/Head[0] / VRCPhysBoneCollider[0]",
                                ItemPath = "", Amount = 1, Value = "1",
                                Children = new[]
                                {
                                    new AvatarInfoAnalysis.PerformanceDetail { Name = "Hair Root 0", Path = "Sample Avatar[0]/PhysBones[1]/Hair Root 0[0] / VRCPhysBone[0]", Amount = 1, Value = "1" },
                                    new AvatarInfoAnalysis.PerformanceDetail { Name = "Hair Root 1", Path = "Sample Avatar[0]/PhysBones[1]/Hair Root 1[0] / VRCPhysBone[0]", Amount = 1, Value = "1" }
                                }
                            },
                            new AvatarInfoAnalysis.PerformanceDetail
                            {
                                Name = "Chest Collider", Path = "Sample Avatar[0]/Chest[0] / VRCPhysBoneCollider[0]",
                                ItemPath = "", Amount = 1, Value = "1",
                                Children = new[] { new AvatarInfoAnalysis.PerformanceDetail { Name = "Hair Root 0", Path = "Sample Avatar[0]/PhysBones[1]/Hair Root 0[0] / VRCPhysBone[0]", Amount = 1, Value = "1" } }
                            }
                        }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "ConstraintsCount", Value = "5", Rating = "Excellent",
                        Amount = 5, DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.Constraint,
                        Details = new[]
                        {
                            SampleConstraint("Accessory", "VRCParentConstraint", "Head", "Chest"),
                            SampleConstraint("Ornament", "VRCParentConstraint", "Accessory"),
                            SampleConstraint("Pendant", "VRCParentConstraint", "Ornament"),
                            SampleConstraint("Hair", "RotationConstraint", "Head"),
                            SampleConstraint("Outfit", "VRCPositionConstraint")
                        }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "ConstraintDepth", Value = "3", Rating = "Excellent",
                        Amount = 3, DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.ConstraintPath,
                        Details = new[]
                        {
                            SampleConstraintPath("Head", "Accessory", "Ornament", "Pendant"),
                            SampleConstraintPath("Head", "Accessory", "Earring"),
                            SampleConstraintPath("Chest", "Outfit")
                        }
                    },
                    new AvatarInfoAnalysis.PerformanceMetric
                    {
                        Category = "ParticleSystemCount", Value = "0", Rating = "Excellent",
                        Amount = 0, TargetRating = "Excellent", TargetLimit = 0, TargetLimitLabel = "0",
                        DetailUnit = AvatarInfoAnalysis.PerformanceDetailUnit.ParticleSystem,
                        Details = Array.Empty<AvatarInfoAnalysis.PerformanceDetail>()
                    }
                }
            };
            var depth = report.Metrics.First(metric => metric.Category == "ConstraintDepth");
            depth.ConstraintGraph = depth.Details.SelectMany(detail => detail.Children)
                .GroupBy(detail => detail.Path).Select(group => group.First()).ToArray();
            return report;
        }

        private static AvatarInfoAnalysis.PerformanceDetail SampleConstraintPath(params string[] nodes)
        {
            var chain = new List<AvatarInfoAnalysis.PerformanceDetail>();
            for (var index = 1; index < nodes.Length; index++)
            {
                var detail = SampleConstraint(nodes[index], "VRCParentConstraint", nodes[index - 1]);
                if (index > 1)
                    detail.Constraint.Sources[0].Path = chain[index - 2].Constraint.Target.Path;
                detail.Constraint.Depth = index;
                detail.Constraint.Dependencies = index > 1
                    ? new[] { chain[index - 2].Path } : Array.Empty<string>();
                chain.Add(detail);
            }
            return new AvatarInfoAnalysis.PerformanceDetail
            {
                Name = string.Join(" → ", nodes), Path = string.Join("\n→ ", chain.Select(detail => detail.Path)),
                Amount = chain.Count, Value = chain.Count.ToString(), Children = chain.ToArray()
            };
        }

        private static AvatarInfoAnalysis.PerformanceDetail SampleConstraint(string target, string type,
            params string[] sources) => new AvatarInfoAnalysis.PerformanceDetail
        {
            Name = target, Path = "Sample Avatar[0]/" +
                (type.StartsWith("VRC", StringComparison.Ordinal) ? "Constraints[2]/" : "Sample Outfit[1]/") +
                target + "[0] / " + type + "[0]",
            ItemPath = "", Amount = 1, Value = "1",
            Constraint = new AvatarInfoAnalysis.PerformanceConstraintRelation
            {
                Type = type,
                Target = new AvatarInfoAnalysis.PerformanceConstraintEndpoint
                    { Name = target, Path = "Sample Avatar[0]/Sample Outfit[1]/" + target + "[0]" },
                Sources = sources.Select(source => new AvatarInfoAnalysis.PerformanceConstraintEndpoint
                {
                    Name = source, Path = "Sample Avatar[0]/Armature[0]/Hips[0]/Spine[0]/" + source + "[0]",
                    Weight = 1d / sources.Length
                }).ToArray()
            }
        };

        private static AvatarInfoAnalysis.PerformanceDetail SamplePhysBonePrefab(string name, string path,
            int count, int amount)
        {
            var prefix = "Sample Avatar[0]/" + (path.Length == 0 ? string.Empty : path + "/");
            var parentPath = string.Empty;
            var nodes = new List<AvatarInfoAnalysis.PerformanceDetail>();
            foreach (var bone in new[] { "Armature", "Hips", "Spine", "Chest", "Neck", "Head" })
            {
                var nodePath = (parentPath.Length == 0 ? prefix : parentPath + "/") + bone + "[0]";
                nodes.Add(new AvatarInfoAnalysis.PerformanceDetail
                    { Name = bone, Path = nodePath, ParentPath = parentPath, Amount = 0 });
                parentPath = nodePath;
            }
            for (var index = 0; index < count; index += 2)
            {
                var boneName = (path.Length == 0 ? "Hair" : "Ribbon") + " Root " + index / 2;
                var components = Enumerable.Range(index, Math.Min(2, count - index)).Select(component =>
                    new AvatarInfoAnalysis.PerformanceDetail
                    {
                        Name = boneName, Path = prefix + "PhysBones[1]/" + boneName + "[0] / VRCPhysBone[" + component + "]",
                        Amount = amount, Value = amount.ToString("N0")
                    }).ToArray();
                nodes.Add(new AvatarInfoAnalysis.PerformanceDetail
                {
                    Name = boneName, Path = parentPath + "/" + boneName + "[" + index / 2 + "]",
                    ParentPath = parentPath, Amount = components.Sum(component => component.Amount), Children = components
                });
            }
            return new AvatarInfoAnalysis.PerformanceDetail
            {
                Name = name, Path = path, ItemName = name, ItemPath = path,
                Amount = count * amount, Value = (count * amount).ToString("N0"), Children = nodes
            };
        }

        private static AvatarInfoAnalysis.PerformanceDetail SampleBonePrefab(string name, string path, int count)
        {
            var rootPath = "Sample Avatar[0]/" + (path.Length == 0 ? "Armature[0]" : path + "/Armature[0]");
            var nodes = new List<AvatarInfoAnalysis.PerformanceDetail>
            {
                new AvatarInfoAnalysis.PerformanceDetail { Name = "Armature", Path = rootPath, ParentPath = "", Amount = 0 }
            };
            var bodyNames = new[] { "Hips", "Spine", "Chest", "Neck", "Head", "LeftUpperArm", "LeftLowerArm",
                "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperLeg", "LeftLowerLeg",
                "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot" };
            var bodyParents = new[] { -1, 0, 1, 2, 3, 2, 5, 6, 2, 8, 9, 0, 11, 12, 0, 14, 15 };
            for (var index = 0; index < count; index++)
            {
                var isBody = path.Length == 0;
                var boneName = isBody && index < bodyNames.Length ? bodyNames[index] : index == 0
                    ? "Root" : (isBody ? "Hair " : "Chain ") + index.ToString("D2");
                var parentIndex = isBody && index < bodyParents.Length ? bodyParents[index] : isBody
                    ? (index - bodyNames.Length) % 6 == 0 ? 4 : index - 1
                    : index == 0 ? -1 : (index - 1) % 6 == 0 ? 0 : index - 1;
                var parentPath = parentIndex < 0 ? rootPath : nodes[parentIndex + 1].Path;
                nodes.Add(new AvatarInfoAnalysis.PerformanceDetail
                {
                    Name = boneName, Path = parentPath + "/" + boneName + "[" + index + "]",
                    ParentPath = parentPath, Amount = 1, Value = "1"
                });
            }
            return new AvatarInfoAnalysis.PerformanceDetail
            {
                Name = name, Path = path, ItemName = name, ItemPath = path,
                Amount = count, Value = count.ToString("N0"),
                Children = nodes
            };
        }

    }
}
