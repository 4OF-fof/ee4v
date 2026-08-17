using System;
using System.Collections.Generic;

namespace Ee4v.Core.Hierarchy
{
    public static class HierarchyObjectVisibilityApi
    {
        private static IHierarchyObjectVisibilityService _service;

        public static IDisposable Register(
            IHierarchyObjectVisibilityService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _service = service;
            return new Registration(service);
        }

        public static int HideFromHierarchy(
            IReadOnlyCollection<int> instanceIds,
            string undoOperationName)
        {
            return _service?.HideFromHierarchy(
                instanceIds,
                undoOperationName) ?? 0;
        }

        public static int RevealInHierarchy(
            IReadOnlyCollection<int> instanceIds,
            string undoOperationName)
        {
            return _service?.RevealInHierarchy(
                instanceIds,
                undoOperationName) ?? 0;
        }

        private sealed class Registration : IDisposable
        {
            private IHierarchyObjectVisibilityService _registeredService;

            public Registration(
                IHierarchyObjectVisibilityService registeredService)
            {
                _registeredService = registeredService;
            }

            public void Dispose()
            {
                if (ReferenceEquals(
                        _service,
                        _registeredService))
                {
                    _service = null;
                }

                _registeredService = null;
            }
        }
    }
}
