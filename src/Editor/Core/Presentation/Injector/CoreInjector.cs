using System;

namespace Ee4v.Core.Injector
{
    internal sealed class CoreInjector
    {
        private static readonly CoreInjector Instance = new CoreInjector();

        private CoreInjector()
        {
            Registry = new InjectionRegistry();
            Presenter = new InjectionPresenter(Registry);
        }

        public static CoreInjector Current => Instance;

        public IInjectionRegistry Registry { get; }

        public InjectionPresenter Presenter { get; }

        public void ResetForTests()
        {
            Registry.Clear();
            Presenter.ResetState();
        }
    }

    internal sealed class InjectionRegistrationLease : IDisposable
    {
        private IInjectionRegistry _registry;
        private IInjectionRegistration _registration;

        public InjectionRegistrationLease(
            IInjectionRegistry registry,
            IInjectionRegistration registration)
        {
            _registry = registry;
            _registration = registration;
        }

        public void Dispose()
        {
            var registry = _registry;
            var registration = _registration;
            _registry = null;
            _registration = null;
            registry?.Unregister(registration);
        }
    }
}
