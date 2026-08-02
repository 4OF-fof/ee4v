#if EE4V_VRCSDK_AVATARS
using Ee4v.AvatarModify.Infrastructure.Unity;
using UnityEditor;
using VRC.SDK3.Avatars.Components;

namespace Ee4v.AvatarModify.Infrastructure.VRChat
{
    [InitializeOnLoad]
    internal static class VrchatAvatarDescriptorBridge
    {
        static VrchatAvatarDescriptorBridge()
        {
            UnityAvatarAssetGateway.HasAvatarDescriptor =
                avatar =>
                    avatar != null &&
                    avatar.GetComponent<VRCAvatarDescriptor>() !=
                    null;
        }
    }
}
#endif
