using System;
using UnityEngine.UIElements;

namespace Ee4v.Core.Injector
{
    public abstract class InjectionRegistration
    {
        protected InjectionRegistration(string id, InjectionChannel channel, int priority, Func<bool> isEnabled)
        {
            Id = id;
            Channel = channel;
            Priority = priority;
            IsEnabled = isEnabled;
        }

        public string Id { get; }

        public InjectionChannel Channel { get; }

        public int Priority { get; }

        public Func<bool> IsEnabled { get; }
    }

    public sealed class ItemInjectionRegistration : InjectionRegistration
    {
        public ItemInjectionRegistration(
            string id,
            InjectionChannel channel,
            Action<ItemInjectionContext> draw,
            int priority = 0,
            Func<bool> isEnabled = null)
            : base(id, channel, priority, isEnabled)
        {
            Draw = draw;
        }

        public Action<ItemInjectionContext> Draw { get; }
    }

    public sealed class VisualElementInjectionRegistration : InjectionRegistration
    {
        public VisualElementInjectionRegistration(
            string id,
            InjectionChannel channel,
            Func<VisualHostContext, VisualElement> createElement,
            int priority = 0,
            Func<bool> isEnabled = null)
            : base(id, channel, priority, isEnabled)
        {
            CreateElement = createElement;
        }

        public Func<VisualHostContext, VisualElement> CreateElement { get; }
    }
}
