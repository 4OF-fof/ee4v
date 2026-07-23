using System;
using UnityEngine;

namespace Ee4v.Core.Injector
{
    public static class InjectorApi
    {
        public static IDisposable Register(
            InjectionRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            var registry = CoreInjector.Current.Registry;
            registry.Register(registration);
            return new InjectionRegistrationLease(
                registry,
                registration);
        }

        public static bool Unregister(
            InjectionRegistration registration)
        {
            return CoreInjector.Current.Registry.Unregister(registration);
        }

        public static void Repaint(InjectionChannel channel)
        {
            CoreInjector.Current.Presenter.Repaint(channel);
        }

        internal static void DrawHierarchyItem(
            int instanceId,
            Rect selectionRect)
        {
            CoreInjector.Current.Presenter.DrawHierarchyItem(
                instanceId,
                selectionRect);
        }

        internal static void DrawProjectItem(
            string guid,
            Rect selectionRect)
        {
            CoreInjector.Current.Presenter.DrawProjectItem(
                guid,
                selectionRect);
        }

        internal static void UpdateVisualHosts()
        {
            CoreInjector.Current.Presenter.UpdateVisualHosts();
        }

        internal static void ResetForTests()
        {
            CoreInjector.Current.ResetForTests();
        }
    }
}
