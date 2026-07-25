using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HiddenObjects
{
    [FilePath(
        "UserSettings/ee4v.hidden-object-restore-states.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class HiddenObjectRestoreStateStore
        : ScriptableSingleton<HiddenObjectRestoreStateStore>,
          IHiddenObjectRestoreStateStore
    {
        [SerializeField]
        private List<SerializedRestoreState> _states =
            new List<SerializedRestoreState>();

        public HiddenObjectRestoreState Get(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            for (var i = 0; i < _states.Count; i++)
            {
                var state = _states[i];
                if (state != null &&
                    string.Equals(
                        state.objectId,
                        objectId,
                        StringComparison.Ordinal))
                {
                    return new HiddenObjectRestoreState(
                        state.objectId,
                        state.activeSelf,
                        state.tag);
                }
            }

            return null;
        }

        public void Put(HiddenObjectRestoreState state)
        {
            if (state == null ||
                string.IsNullOrEmpty(state.ObjectId))
            {
                return;
            }

            var serialized = new SerializedRestoreState
            {
                objectId = state.ObjectId,
                activeSelf = state.ActiveSelf,
                tag = state.Tag
            };
            for (var i = 0; i < _states.Count; i++)
            {
                var current = _states[i];
                if (current != null &&
                    string.Equals(
                        current.objectId,
                        state.ObjectId,
                        StringComparison.Ordinal))
                {
                    _states[i] = serialized;
                    return;
                }
            }

            _states.Add(serialized);
        }

        public void Save()
        {
            Save(true);
        }

        [Serializable]
        private sealed class SerializedRestoreState
        {
            public string objectId;
            public bool activeSelf;
            public string tag;
        }
    }
}
