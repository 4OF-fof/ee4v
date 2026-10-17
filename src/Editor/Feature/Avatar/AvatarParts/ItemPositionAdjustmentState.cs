using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarParts
{
    internal sealed class ItemPositionAdjustmentState : ScriptableSingleton<ItemPositionAdjustmentState>
    {
        [Serializable]
        internal sealed class Baseline
        {
            public Transform Target;
            public Vector3 Position;
            public Vector3 Scale;
        }

        [SerializeField] private List<Baseline> _baselines = new List<Baseline>();

        internal Baseline Get(Transform target, Vector3 position, Vector3 scale)
        {
            _baselines.RemoveAll(entry => entry.Target == null);
            var baseline = _baselines.Find(entry => entry.Target == target);
            if (baseline != null) return baseline;
            baseline = new Baseline { Target = target, Position = position, Scale = scale };
            _baselines.Add(baseline);
            return baseline;
        }

        internal void Set(Baseline baseline, Vector3 position, Vector3 scale)
        {
            if (baseline.Position == position && baseline.Scale == scale) return;
            Undo.RecordObject(this, I18N.Get("workflow.position.undo"));
            baseline.Position = position;
            baseline.Scale = scale;
        }
    }
}
