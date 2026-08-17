using System;
using UnityEngine.UIElements;

namespace Ee4v.Core.Injector
{
    public abstract class InjectionRegistration : IInjectionRegistration
    {
        protected InjectionRegistration(
            string id,
            InjectionChannel channel,
            int priority,
            Func<bool> isEnabled)
        {
            Id = id;
            Channel = channel;
            Priority = priority;
            IsEnabledPredicate = isEnabled;
        }

        public string Id { get; }

        public InjectionChannel Channel { get; }

        public int Priority { get; }

        public Func<bool> IsEnabledPredicate { get; }

        public bool IsEnabled()
        {
            return IsEnabledPredicate == null || IsEnabledPredicate();
        }
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
            Draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        public Action<ItemInjectionContext> Draw { get; }
    }

    public sealed class VisualElementInjectionRegistration
        : InjectionRegistration
    {
        public VisualElementInjectionRegistration(
            string id,
            InjectionChannel channel,
            Func<VisualHostContext, VisualElement> createElement,
            int priority = 0,
            Func<bool> isEnabled = null)
            : base(id, channel, priority, isEnabled)
        {
            CreateElement = createElement ??
                throw new ArgumentNullException(nameof(createElement));
        }

        public Func<VisualHostContext, VisualElement> CreateElement { get; }
    }
}
