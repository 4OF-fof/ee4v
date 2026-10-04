using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Ee4v.AvatarInfo
{
    public sealed class AvatarInfoParameterMemory
    {
        public int Used;
        public int Limit;
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
    }
}
