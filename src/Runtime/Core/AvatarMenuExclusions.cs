using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Ee4v.Core.AvatarEvaluation
{
    [Serializable]
    public sealed class AvatarMenuExclusion
    {
        public string Id;
        public Object Source;
        public int SourceIndex = -1;
        public VRCExpressionsMenu.Control Control;
        public string DisplayName;
    }

    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class AvatarMenuExclusions : MonoBehaviour, IEditorOnly
    {
        public List<AvatarMenuExclusion> Entries = new List<AvatarMenuExclusion>();
    }
}
