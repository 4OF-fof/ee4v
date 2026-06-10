using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class MainToolbar : VisualElement
    {
        private const string RootClassName = "ee4v-ui-main-toolbar";
        private const string ContentClassName = "ee4v-ui-main-toolbar__content";

        public MainToolbar(MainView mainView = null)
        {
            AddToClassList(RootClassName);

            Content = new VisualElement();
            Content.AddToClassList(ContentClassName);
            Add(Content);

            if (mainView != null)
            {
                var historyNavigation = new HistoryNavigation();
                historyNavigation.BackClicked += mainView.GoBack;
                historyNavigation.ForwardClicked += mainView.GoForward;
                historyNavigation.BreadcrumbClicked += mainView.GoToBreadcrumb;
                mainView.History.Changed += historyNavigation.SetState;
                historyNavigation.SetState(mainView.History.State);
                Content.Add(historyNavigation);
            }
        }

        public VisualElement Content { get; }
    }
}
