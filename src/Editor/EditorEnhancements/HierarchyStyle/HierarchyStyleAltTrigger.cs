namespace Ee4v.HierarchyStyle
{
    internal sealed class HierarchyStyleAltTrigger
    {
        private int _triggeredInstanceId;

        public bool TryActivate(
            int instanceId,
            bool altPressed,
            bool pointerInside)
        {
            if (!altPressed)
            {
                _triggeredInstanceId = 0;
                return false;
            }

            if (!pointerInside ||
                instanceId == 0 ||
                _triggeredInstanceId == instanceId)
            {
                return false;
            }

            _triggeredInstanceId = instanceId;
            return true;
        }
    }
}
