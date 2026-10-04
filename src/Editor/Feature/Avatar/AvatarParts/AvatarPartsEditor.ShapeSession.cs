using System;
using Ee4v.Core.I18n;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private void BeginBodyScaleDrag()
        {
            if (_bodyScaleDragging)
            {
                return;
            }

            _bodyScaleDragging = true;
            ClearPendingBodySizeChange();
        }

        public void EndBodyScaleDrag(bool rebuildOnFailure = true)
        {
            if (!_bodyScaleDragging &&
                _pendingBodySizeChange == PendingBodySizeChange.None)
            {
                return;
            }

            var pendingChange = _pendingBodySizeChange;
            var targetPaths = _pendingBodyScaleTargetPaths;
            var multipliers = _pendingBodyScaleMultipliers;
            var updateAvatarViewPosition =
                _pendingBodyScaleUpdatesViewPosition;
            var rendererPath = _pendingBodyBlendShapeRendererPath;
            var shapeName = _pendingBodyBlendShapeName;
            var weight = _pendingBodyBlendShapeWeight;
            var blendShapeGroup = _pendingBodyBlendShapeGroup;
            var individualBlendShape = _pendingIndividualBlendShape;
            ClearPendingBodySizeChange();
            _bodyScaleDragging = false;
            if (pendingChange == PendingBodySizeChange.Scale)
            {
                ApplyBodyScale(
                    targetPaths,
                    multipliers,
                    updateAvatarViewPosition,
                    rebuildOnFailure);
            }
            else if (pendingChange == PendingBodySizeChange.BlendShape)
            {
                if (individualBlendShape != null)
                {
                    ApplyIndividualBodyBlendShape(
                        blendShapeGroup,
                        individualBlendShape,
                        weight,
                        rebuildOnFailure);
                }
                else if (blendShapeGroup != null)
                {
                    ApplyBodyBlendShapeGroup(
                        blendShapeGroup,
                        weight,
                        rebuildOnFailure);
                }
                else
                {
                    ApplyBodyBlendShape(
                        rendererPath,
                        shapeName,
                        weight,
                        rebuildOnFailure);
                }
            }
            _context.Preview?.FlushUpdates(
                recalculateBounds:
                pendingChange == PendingBodySizeChange.Scale);
            if (pendingChange == PendingBodySizeChange.None)
            {
                SaveBodyScalePrefab(rebuildOnFailure);
            }
        }

        public void ClearPendingBodySizeChange()
        {
            _pendingBodySizeChange = PendingBodySizeChange.None;
            _pendingBodyScaleTargetPaths = null;
            _pendingBodyScaleUpdatesViewPosition = false;
            _pendingBodyBlendShapeRendererPath = null;
            _pendingBodyBlendShapeName = null;
            _pendingBodyBlendShapeGroup = null;
            _pendingIndividualBlendShape = null;
        }

        public void SaveBodyScalePrefab(bool rebuildOnFailure = true)
        {
            if (!_bodyScaleDirty || !_context.Edits.CanEditPrefab())
            {
                return;
            }
            _bodyScaleDirty = false;
            _context.Edits.WorkingSceneDirty = true;
            _context.Edits.Changed();
            _context.Feedback = string.Empty;
        }

        public void ReportBodyScaleFailure(
            Exception exception,
            bool rebuildControls)
        {
            Debug.LogException(exception);
            _context.Feedback = I18N.Get(
                "workflow.appearance.sizeSaveFailed");
            _context.FeedbackType = HelpBoxMessageType.Error;
            if (rebuildControls && _context.UiRoot.panel != null)
            {
                _context.UiRoot.schedule.Execute(() =>
                    _context.Host.ShowParts());
            }
        }

    }
}
