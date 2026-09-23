using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    public sealed class FaceExpressionSequenceData : ScriptableObject
    {
        [SerializeField]
        private List<FaceExpressionPoseSource> _poseSources =
            new List<FaceExpressionPoseSource>();

        internal List<FaceExpressionPoseSource> PoseSources =>
            _poseSources ?? (_poseSources = new List<FaceExpressionPoseSource>());
    }

    [Serializable]
    internal sealed class FaceExpressionPoseSource
    {
        [SerializeField]
        private float _time;

        [SerializeField]
        private AnimationClip _clip;

        [SerializeField]
        private string _name;

        internal FaceExpressionPoseSource(
            float time,
            AnimationClip clip,
            string name = null)
        {
            _time = time;
            _clip = clip;
            _name = name ?? string.Empty;
        }

        internal float Time
        {
            get => _time;
            set => _time = value;
        }

        internal AnimationClip Clip
        {
            get => _clip;
            set => _clip = value;
        }

        internal string Name
        {
            get => _name ?? string.Empty;
            set => _name = value ?? string.Empty;
        }
    }
}
