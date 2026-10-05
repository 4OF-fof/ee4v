using System;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    [Serializable]
    internal sealed class GestureAssignmentUndoEntry
    {
        public FaceGesture left;
        public FaceGesture right;
        public AnimationClip clip;
        public bool blink;
        public bool mouth;
        public string name;
        public string menuName;
        internal FaceExpressionAssignment Assignment => new FaceExpressionAssignment(clip, blink, mouth, menuName);
    }

    internal sealed class GestureAssignmentUndoState : ScriptableObject
    {
        public int revision;
        public GestureAssignmentUndoEntry[] assignments = Array.Empty<GestureAssignmentUndoEntry>();
        public GestureAssignmentUndoEntry[] menu = Array.Empty<GestureAssignmentUndoEntry>();
        public FaceGesture[] left = Array.Empty<FaceGesture>();
        public FaceGesture[] right = Array.Empty<FaceGesture>();
    }
}
