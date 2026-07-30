using Ee4v.AssetManager.Contracts;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class FileTreeDetailView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-file-detail";
        private readonly VisualElement _contentHost;
        private readonly FileDependencySettingsView
            _dependencySettings;

        public FileTreeDetailView()
        {
            AddToClassList(RootClassName);
            _contentHost = new VisualElement();
            _contentHost.style.flexGrow = 1f;
            Add(_contentHost);
            _dependencySettings =
                new FileDependencySettingsView();
            Add(_dependencySettings);
        }

        public void SetState(FileTreeDetailState state)
        {
            _contentHost.Clear();
            if (state == null)
            {
                _dependencySettings.SetState(null);
                return;
            }

            var presentation =
                FileTreeDetailContentCatalog.Resolve(
                    state.Extension);
            _contentHost.Add(
                presentation.CreateContent(state));
            IAssetManager assetManager;
            if (string.IsNullOrWhiteSpace(
                    state.AssetFileId) ||
                !AssetManagerUiDependencies
                    .TryGetAssetManager(
                        out assetManager))
            {
                _dependencySettings.SetState(null);
                return;
            }

            try
            {
                _dependencySettings.SetState(
                    FileDependencySettingsPresenter
                        .CreateState(
                            assetManager,
                            state.AssetFileId));
            }
            catch (System.Exception exception)
            {
                _dependencySettings.SetError(exception);
            }
        }
    }
}
