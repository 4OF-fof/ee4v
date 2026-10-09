using System;
using UnityEngine;

namespace Ee4v.Core.AvatarEvaluation
{
    /// <summary>A sampling frame on the owned rig. Paths refer to the original avatar hierarchy.</summary>
    public sealed class AvatarAnimationFrame
    {
        private readonly Action<AnimationClip, float> _sample;
        private readonly Func<string, Transform> _resolve;
        public GameObject Root { get; }

        internal AvatarAnimationFrame(GameObject root, Action<AnimationClip, float> sample, Func<string, Transform> resolve)
        {
            Root = root;
            _sample = sample;
            _resolve = resolve;
        }

        public void Sample(AnimationClip clip, float time) => _sample(clip, time);
        public Transform ResolveTransform(string path) => _resolve(path);
    }
}
