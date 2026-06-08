using System.Linq;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private void EnsureStories()
        {
            EnsureCatalogRegistrations();
            if (_stories.Count > 0)
            {
                return;
            }

            for (var i = 0; i < RegisteredStories.Count; i++)
            {
                var registration = RegisteredStories[i];
                _stories.Add(new StoryDefinition(
                    registration.Id,
                    registration.Group,
                    registration.Title,
                    registration.Description,
                    registration.Details,
                    registration.Dependencies,
                    registration.Implementation,
                    parent => registration.Build(this, parent)));
            }

            if (_selectedStory == null && _stories.Count > 0)
            {
                _selectedStory = _stories
                    .OrderBy(story => story, StoryDefinitionGroupComparer.Instance)
                    .FirstOrDefault();
            }
        }

        private sealed class UiCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 0; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/common.uss");
            }
        }

        private sealed class CatalogWindowStyleRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 1000; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Catalog/catalog-window.uss");
            }
        }
    }
}
