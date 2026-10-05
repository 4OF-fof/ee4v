using System;
using System.Linq;
using System.Reflection;
using BlackStartX.GestureManager;
using BlackStartX.GestureManager.Editor.Modules.Vrc3;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Ee4v.AssetManager.Simulation
{
    public static class GestureManagerIntegration
    {
        internal static GestureManager FindManager(GameObject avatar)
        {
            if (avatar == null || !avatar.scene.IsValid()) { return null; }
            var managers = avatar.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GestureManager>(true)).ToArray();
            return managers.FirstOrDefault(manager =>
                       manager.Module?.Avatar == avatar ||
                       manager.settings?.favourite != null &&
                       manager.settings.favourite.gameObject == avatar)
                   ?? (managers.Length == 1 ? managers[0] : null);
        }

        internal static ModuleVrc3 Connect(GameObject avatar, GestureManager manager)
        {
            if (manager.Module is ModuleVrc3 connected && connected.Avatar == avatar)
            {
                return connected;
            }
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) { throw new InvalidOperationException("Avatar Descriptor is unavailable."); }
            if (GestureManager.ControlledAvatars.ContainsKey(avatar))
            {
                throw new InvalidOperationException("The avatar is already controlled by another GestureManager.");
            }
            var module = new ModuleVrc3(descriptor);
            if (!module.IsValidDesc())
            {
                throw new InvalidOperationException(string.Join("\n", module.GetErrors()));
            }
            manager.SetModule(module);
            return manager.Module as ModuleVrc3;
        }

        internal static GameObject GetVisibleAvatar(ModuleVrc3 module)
        {
            if (module == null) { return null; }
            var dummy = typeof(ModuleVrc3).GetField("DummyMode", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(module);
            var avatar = dummy?.GetType().GetField("Avatar", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(dummy) as GameObject;
            return avatar != null ? avatar : module.Avatar;
        }
    }
}
