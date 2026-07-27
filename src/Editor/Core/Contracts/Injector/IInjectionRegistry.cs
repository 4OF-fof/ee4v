using System;
using System.Collections.Generic;

namespace Ee4v.Core.Injector
{
    public interface IInjectionRegistration
    {
        string Id { get; }

        InjectionChannel Channel { get; }

        int Priority { get; }

        bool IsEnabled();
    }

    public sealed class InjectionRegistryChangedEventArgs : EventArgs
    {
        public InjectionRegistryChangedEventArgs(InjectionChannel channel)
        {
            Channel = channel;
        }

        public InjectionChannel Channel { get; }
    }

    public interface IInjectionRegistry
    {
        event EventHandler<InjectionRegistryChangedEventArgs> Changed;

        void Register(IInjectionRegistration registration);

        bool Unregister(IInjectionRegistration registration);

        IReadOnlyList<IInjectionRegistration> GetRegistrations(
            InjectionChannel channel);

        void Clear();
    }
}
