using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public class ActionBar : VisualElement
    {
        public ActionBar()
        {
            AddToClassList("ee4v-ui-action-bar");
            Leading = new VisualElement();
            Leading.AddToClassList("ee4v-ui-action-bar__leading");
            Center = new VisualElement();
            Center.AddToClassList("ee4v-ui-action-bar__center");
            Actions = new VisualElement();
            Actions.AddToClassList("ee4v-ui-action-bar__actions");
            hierarchy.Add(Leading);
            hierarchy.Add(Center);
            hierarchy.Add(Actions);
        }

        public VisualElement Leading { get; }
        public VisualElement Center { get; }
        public VisualElement Actions { get; }

        public override VisualElement contentContainer => Leading;
    }
}
