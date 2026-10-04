using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Ee4v.AvatarInfo
{
    public sealed class AvatarInfoParameterMemory
    {
        public int Used;
        public int Limit;
        public AvatarInfoParameterSource[] Parameters;
        public AvatarInfoParameterItemUsage[] Items;
        public bool ItemsEstimated;

        public void SetItemUsage(IReadOnlyList<AvatarInfoParameterSource> sources, bool estimated)
        {
            if (sources == null || Parameters == null) return;
            ItemsEstimated = estimated;
            var usage = estimated ? sources : Parameters.Select(parameter =>
            {
                var source = sources.FirstOrDefault(value => value.Name == parameter.Name);
                return new AvatarInfoParameterSource
                {
                    Name = parameter.Name, Used = parameter.Used,
                    ItemName = source?.ItemName, ItemPath = source?.ItemPath
                };
            });
            Items = usage.Where(parameter => parameter.Used > 0)
                .GroupBy(parameter => parameter.ItemPath)
                .Select(group => new AvatarInfoParameterItemUsage
                {
                    Name = group.First().ItemName, Path = group.Key,
                    Used = group.Sum(parameter => parameter.Used)
                }).OrderByDescending(item => item.Used).ThenBy(item => item.Name).ToArray();
        }
    }

    public sealed class AvatarInfoParameterSource
    {
        public string Name;
        public int Used;
        public string ItemName;
        public string ItemPath;
    }

    public sealed class AvatarInfoParameterItemUsage
    {
        public string Name;
        public string Path;
        public int Used;
    }

    public sealed class AvatarInfoSdkAvatar
    {
        public string Id;
        public string Name;
    }

    public interface IAvatarInfoSdk
    {
        bool IsLoggedIn { get; }
        AvatarInfoParameterMemory ReadParameterMemory(GameObject avatar);
        Task<IReadOnlyList<AvatarInfoSdkAvatar>> GetOwnAvatars(CancellationToken cancellationToken);
        void SetBlueprintId(GameObject avatar, string id);
    }

    public static class AvatarInfoSdk
    {
        public static IAvatarInfoSdk Provider { get; set; }
        public static Func<GameObject, AvatarInfoParameterSource[]> ReadParameterSources { get; set; }
    }
}
