using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.Core.Background
{
    internal sealed class BackgroundStatusOverlayHost : VisualElement
    {
        private const string HostClassName = "ee4v-ui-status-overlay-host";
        private readonly StatusOverlay _overlay;

        public BackgroundStatusOverlayHost()
        {
            name = BackgroundStatusOverlay.HostElementName;
            AddToClassList(HostClassName);
            pickingMode = PickingMode.Ignore;
            _overlay = new StatusOverlay();
            Add(_overlay);
            schedule.Execute(Refresh).Every(100);
            Refresh();
        }

        internal void Refresh()
        {
            var task = CoreBackgroundActivities.Current.GetState();
            _overlay.SetState(new StatusOverlayState(
                task.IsActive,
                task.Message));
        }
    }

}
