using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class FileTreeDetailView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-file-detail";
        private readonly VisualElement _contentHost;
        private readonly VisualElement _actions;
        private readonly UiButton _dependencyButton;
        private readonly FileDependencySettingsView
            _dependencySettings;
        private readonly VisualElement _dependencyOverlay;

        public FileTreeDetailView()
        {
            AddToClassList(RootClassName);
            _actions = new VisualElement();
            _actions.AddToClassList(
                RootClassName + "__actions");
            _dependencyButton = new UiButton(
                new UiButtonState(
                    I18N.Get(
                        "assetManager.fileDependencies.open"),
                    iconState:
                    IconState.FromFluentIcon(
                        UiFluentIcon.Link,
                        UiSizeTokens.Size14),
                    variant: UiButtonVariant.Ghost,
                    size: UiButtonSize.Compact),
                ShowDependencySelection);
            _dependencyButton.AddToClassList(
                RootClassName +
                "__dependency-button");
            _actions.Add(_dependencyButton);
            Add(_actions);
            _actions.style.display =
                DisplayStyle.None;

            _contentHost = new VisualElement();
            _contentHost.AddToClassList(
                RootClassName + "__content");
            Add(_contentHost);

            _dependencyOverlay = new VisualElement();
            _dependencyOverlay.AddToClassList(
                RootClassName +
                "__dependency-overlay");
            var scrim = new VisualElement();
            scrim.AddToClassList(
                RootClassName +
                "__dependency-scrim");
            scrim.RegisterCallback<PointerDownEvent>(
                _ => CloseDependencySelection());
            _dependencyOverlay.Add(scrim);

            var selection = new VisualElement();
            selection.AddToClassList(
                RootClassName +
                "__dependency-selection");
            var selectionActions =
                new VisualElement();
            selectionActions.AddToClassList(
                RootClassName +
                "__dependency-selection-actions");
            selectionActions.Add(
                new UiButton(
                    new UiButtonState(
                        I18N.Get(
                            "assetManager.fileDependencies.close"),
                        variant:
                        UiButtonVariant.Ghost,
                        size:
                        UiButtonSize.Compact),
                    CloseDependencySelection));
            selection.Add(selectionActions);
            _dependencySettings =
                new FileDependencySettingsView();
            selection.Add(_dependencySettings);
            _dependencyOverlay.Add(selection);
            Add(_dependencyOverlay);
            CloseDependencySelection();
        }

        public void SetState(FileTreeDetailState state)
        {
            CloseDependencySelection();
            _contentHost.Clear();
            if (state == null)
            {
                _dependencySettings.SetState(null);
                _actions.style.display =
                    DisplayStyle.None;
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
                _actions.style.display =
                    DisplayStyle.None;
                return;
            }

            _actions.style.display =
                DisplayStyle.Flex;
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

        private void ShowDependencySelection()
        {
            _dependencyOverlay.style.display =
                DisplayStyle.Flex;
            _dependencyOverlay.BringToFront();
        }

        internal void ShowDependencySelection(
            FileDependencySettingsState state)
        {
            _dependencySettings.SetState(state);
            ShowDependencySelection();
        }

        private void CloseDependencySelection()
        {
            _dependencyOverlay.style.display =
                DisplayStyle.None;
        }
    }
}
