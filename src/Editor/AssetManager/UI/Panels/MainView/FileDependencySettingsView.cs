using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class FileDependencyOption
    {
        internal FileDependencyOption(
            string itemId,
            string itemName,
            string fileId,
            string fileName,
            string extension = null)
        {
            ItemId = itemId ?? string.Empty;
            ItemName = itemName ?? string.Empty;
            FileId = fileId ?? string.Empty;
            FileName = fileName ?? string.Empty;
            Extension = FileExtensionUtility.Normalize(
                extension ?? fileName);
        }

        internal string ItemId { get; }
        internal string ItemName { get; }
        internal string FileId { get; }
        internal string FileName { get; }
        internal string Extension { get; }
    }

    internal sealed class FileDependencySettingsState
    {
        internal FileDependencySettingsState(
            string currentItemId,
            IReadOnlyList<FileDependencyOption> options,
            IReadOnlyList<string> dependencyFileIds,
            Action<IReadOnlyList<string>> save)
        {
            CurrentItemId = currentItemId ?? string.Empty;
            Options = options ??
                Array.Empty<FileDependencyOption>();
            DependencyFileIds = dependencyFileIds ??
                Array.Empty<string>();
            Save = save ??
                throw new ArgumentNullException(nameof(save));
        }

        internal string CurrentItemId { get; }

        internal IReadOnlyList<FileDependencyOption> Options
        {
            get;
        }

        internal IReadOnlyList<string> DependencyFileIds
        {
            get;
        }

        internal Action<IReadOnlyList<string>> Save { get; }
    }

    internal static class FileDependencySettingsPresenter
    {
        internal static FileDependencySettingsState CreateState(
            IAssetManager assetManager,
            string fileId)
        {
            if (assetManager == null)
            {
                throw new ArgumentNullException(
                    nameof(assetManager));
            }

            var items = assetManager.SearchItems(
                    new AssetItemQuery
                    {
                        Lifecycle =
                            AssetFileLifecycle.Active,
                        Limit = int.MaxValue
                    })
                .Items ?? Array.Empty<AssetItem>();
            var currentItem = items
                .Where(item => item != null)
                .FirstOrDefault(item =>
                    (item.Files ??
                     Array.Empty<AssetFileSummary>())
                    .Any(file =>
                        file != null &&
                        string.Equals(
                            file.Id,
                            fileId,
                            StringComparison.Ordinal)));
            var options = items
                .Where(item => item != null)
                .SelectMany(item =>
                    (item.Files ??
                     Array.Empty<AssetFileSummary>())
                    .Where(file =>
                        file != null &&
                        !string.Equals(
                            file.Id,
                            fileId,
                            StringComparison.Ordinal))
                    .Select(file =>
                        new FileDependencyOption(
                            item.Id,
                            item.Name,
                            file.Id,
                            file.FileName,
                            file.Extension)))
                .OrderBy(option => option.ItemName)
                .ThenBy(option => option.FileName)
                .ThenBy(option => option.FileId)
                .ToArray();
            var dependencies = assetManager
                .GetFileDependencies(fileId)
                .Where(dependency => dependency != null)
                .Select(dependency =>
                    dependency.DependencyFileId)
                .ToArray();
            return new FileDependencySettingsState(
                currentItem == null
                    ? string.Empty
                    : currentItem.Id,
                options,
                dependencies,
                selected =>
                    assetManager.SetFileDependencies(
                        fileId,
                        selected));
        }
    }

    internal sealed class FileDependencyTreeNode
    {
        internal FileDependencyTreeNode(
            string name,
            string fileId = null,
            string extension = null)
        {
            Name = name ?? string.Empty;
            FileId = fileId ?? string.Empty;
            Extension = extension ?? string.Empty;
        }

        internal string Name { get; }
        internal string FileId { get; }
        internal string Extension { get; }
        internal bool IsFile =>
            !string.IsNullOrWhiteSpace(FileId);
    }

    internal sealed class FileDependencyTreeRow :
        VisualElement
    {
        private const string GroupClassName =
            "ee4v-file-dependency-settings__row--group";
        private readonly Toggle _toggle;
        private readonly UiTextElement _name;
        private readonly UiTextElement _meta;
        private readonly Action<FileDependencyTreeNode, bool>
            _changed;
        private FileDependencyTreeNode _node;
        private bool _binding;

        internal FileDependencyTreeRow(
            Action<FileDependencyTreeNode, bool> changed)
        {
            _changed = changed ??
                throw new ArgumentNullException(
                    nameof(changed));
            AddToClassList(
                "ee4v-file-dependency-settings__row");

            _toggle = new Toggle
            {
                label = string.Empty
            };
            _toggle.RegisterValueChangedCallback(
                OnToggleChanged);
            Add(_toggle);

            _name = UiTextFactory.Create(
                string.Empty,
                UiClassNames.NavigationItemLabel);
            _name.style.flexGrow = 1f;
            _name.pickingMode = PickingMode.Ignore;
            Add(_name);

            _meta = UiTextFactory.Create(
                string.Empty,
                "ee4v-file-dependency-settings__meta",
                UiClassNames.ContextMenuShortcut);
            _meta.pickingMode = PickingMode.Ignore;
            Add(_meta);

            RegisterCallback<PointerDownEvent>(
                OnPointerDown);
        }

        internal Toggle Toggle => _toggle;

        internal void Bind(
            FileDependencyTreeNode node,
            bool selected)
        {
            _node = node;
            var isFile = node != null && node.IsFile;
            EnableInClassList(GroupClassName, !isFile);
            _toggle.style.display = isFile
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _binding = true;
            _toggle.SetValueWithoutNotify(
                isFile && selected);
            _binding = false;
            _name.SetText(node == null
                ? string.Empty
                : node.Name);
            _meta.SetText(isFile
                ? node.Extension.ToUpperInvariant()
                : string.Empty);
        }

        private void OnToggleChanged(
            ChangeEvent<bool> evt)
        {
            if (!_binding &&
                _node != null &&
                _node.IsFile)
            {
                _changed(_node, evt.newValue);
            }
        }

        private void OnPointerDown(
            PointerDownEvent evt)
        {
            var target = evt.target as VisualElement;
            if (evt.button != 0 ||
                _node == null ||
                !_node.IsFile ||
                (target != null &&
                 _toggle.Contains(target)))
            {
                return;
            }

            _toggle.value = !_toggle.value;
            evt.StopPropagation();
        }
    }

    internal sealed class FileDependencySettingsView :
        VisualElement
    {
        private const string RootClassName =
            "ee4v-file-dependency-settings";
        private const string ErrorClassName =
            "ee4v-file-dependency-settings__error";
        private const string CurrentSectionClassName =
            "ee4v-file-dependency-settings__section--current";
        private const string OtherSectionClassName =
            "ee4v-file-dependency-settings__section--other";
        private readonly VisualElement _currentSection;
        private readonly VisualElement _currentRows;
        private readonly UiTextElement _currentEmpty;
        private readonly SearchableTreeView<
            FileDependencyTreeNode> _tree;
        private readonly UiTextElement _error;
        private FileDependencySettingsState _state;
        private HashSet<string> _selected =
            new HashSet<string>(StringComparer.Ordinal);

        internal FileDependencySettingsView()
        {
            AddToClassList(RootClassName);
            var title = UiTextFactory.Create(
                I18N.Get(
                    "assetManager.fileDependencies.title"),
                "ee4v-file-dependency-settings__title",
                UiClassNames.SectionTitle);
            title.style.flexShrink = 0f;
            Add(title);
            var instruction = UiTextFactory.Create(
                I18N.Get(
                    "assetManager.fileDependencies.instruction"),
                "ee4v-file-dependency-settings__instruction",
                UiClassNames.InfoCardDescription);
            instruction.style.flexShrink = 0f;
            Add(instruction);

            _currentSection = CreateSection(
                CurrentSectionClassName,
                "assetManager.fileDependencies.sameItem");
            _currentRows = new VisualElement();
            _currentRows.AddToClassList(
                "ee4v-file-dependency-settings__current-rows");
            _currentSection.Add(_currentRows);
            _currentEmpty = UiTextFactory.Create(
                I18N.Get(
                    "assetManager.fileDependencies.sameItemEmpty"),
                "ee4v-file-dependency-settings__current-empty",
                UiClassNames.InfoCardDescription);
            _currentSection.Add(_currentEmpty);
            Add(_currentSection);

            var otherSection = CreateSection(
                OtherSectionClassName,
                "assetManager.fileDependencies.otherItems");

            var searchTooltip =
                I18N.GetForScope(
                    "UI",
                    "ui.search.tooltip");
            var clearTooltip =
                I18N.GetForScope(
                    "UI",
                    "ui.clear.tooltip");
            _tree = new SearchableTreeView<
                FileDependencyTreeNode>(
                () => new FileDependencyTreeRow(
                    OnSelectionChanged),
                BindTreeRow,
                emptyText: I18N.Get(
                    "assetManager.fileDependencies.empty"),
                searchPlaceholder: I18N.Get(
                    "assetManager.fileDependencies.searchPlaceholder"),
                selectionType: SelectionType.None,
                searchTooltip: searchTooltip,
                clearTooltip: clearTooltip,
                searchIconState:
                    IconState.FromFluentIcon(
                        UiFluentIcon.Search,
                        UiSizeTokens.Size14,
                        searchTooltip),
                clearIconState:
                    IconState.FromFluentIcon(
                        UiFluentIcon.Dismiss,
                        UiSizeTokens.Size10,
                        clearTooltip));
            _tree.AddToClassList(
                "ee4v-file-dependency-settings__tree");
            _tree.SetViewDataKey(
                "ee4v-asset-manager-file-dependency-tree");
            otherSection.Add(_tree);
            Add(otherSection);

            _error = UiTextFactory.Create(
                string.Empty,
                ErrorClassName,
                UiClassNames.FormError);
            Add(_error);
            SetState(null);
        }

        internal void SetState(
            FileDependencySettingsState state)
        {
            _state = state;
            _selected = new HashSet<string>(
                state == null
                    ? Array.Empty<string>()
                    : state.DependencyFileIds,
                StringComparer.Ordinal);
            style.display = state == null
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _error.SetText(string.Empty);
            RefreshCurrentRows();
            _tree.SetItems(
                state == null
                    ? null
                    : CreateTreeItems(
                        state.Options.Where(option =>
                            option != null &&
                            !string.Equals(
                                option.ItemId,
                                state.CurrentItemId,
                                StringComparison.Ordinal))
                            .ToArray()));
        }

        internal void SetError(Exception exception)
        {
            _state = null;
            _selected.Clear();
            style.display = DisplayStyle.Flex;
            _currentSection.style.display =
                DisplayStyle.None;
            _currentRows.Clear();
            _tree.SetItems(null);
            _error.SetText(
                AssetManagerUiErrorMessage.Format(
                    exception));
        }

        private void BindTreeRow(
            VisualElement element,
            FileDependencyTreeNode node)
        {
            var row = element as FileDependencyTreeRow;
            row?.Bind(
                node,
                node != null &&
                _selected.Contains(node.FileId));
        }

        private void OnSelectionChanged(
            FileDependencyTreeNode node,
            bool selected)
        {
            if (_state == null ||
                node == null ||
                !node.IsFile)
            {
                return;
            }

            var candidate = new HashSet<string>(
                _selected,
                StringComparer.Ordinal);
            if (selected)
            {
                candidate.Add(node.FileId);
            }
            else
            {
                candidate.Remove(node.FileId);
            }

            try
            {
                _state.Save(candidate.ToArray());
                _selected = candidate;
                _error.SetText(string.Empty);
            }
            catch (Exception exception)
            {
                _error.SetText(
                    AssetManagerUiErrorMessage.Format(
                        exception));
            }

            _tree.RefreshItems();
            RefreshCurrentRows();
        }

        private static VisualElement CreateSection(
            string modifierClassName,
            string titleKey)
        {
            var section = new VisualElement();
            section.AddToClassList(
                "ee4v-file-dependency-settings__section");
            section.AddToClassList(modifierClassName);
            section.Add(UiTextFactory.Create(
                I18N.Get(titleKey),
                UiClassNames.SectionTitle));
            return section;
        }

        private void RefreshCurrentRows()
        {
            _currentRows.Clear();
            if (_state == null ||
                string.IsNullOrWhiteSpace(
                    _state.CurrentItemId))
            {
                _currentSection.style.display =
                    DisplayStyle.None;
                return;
            }

            _currentSection.style.display =
                DisplayStyle.Flex;
            var options = _state.Options
                .Where(option =>
                    option != null &&
                    string.Equals(
                        option.ItemId,
                        _state.CurrentItemId,
                        StringComparison.Ordinal))
                .OrderBy(option => option.FileName)
                .ThenBy(option => option.FileId)
                .ToArray();
            _currentEmpty.style.display =
                options.Length == 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i];
                var node = new FileDependencyTreeNode(
                    option.FileName,
                    option.FileId,
                    option.Extension);
                var row = new FileDependencyTreeRow(
                    OnSelectionChanged);
                row.Bind(
                    node,
                    _selected.Contains(node.FileId));
                _currentRows.Add(row);
            }
        }

        private static IReadOnlyList<
            SearchableTreeItemData<FileDependencyTreeNode>>
            CreateTreeItems(
                IReadOnlyList<FileDependencyOption> options)
        {
            var result = new List<
                SearchableTreeItemData<
                    FileDependencyTreeNode>>();
            var nextId = 1;
            foreach (var itemGroup in
                     (options ??
                      Array.Empty<FileDependencyOption>())
                     .Where(option => option != null)
                     .GroupBy(option => new
                     {
                         option.ItemId,
                         option.ItemName
                     })
                     .OrderBy(group =>
                         group.Key.ItemName)
                     .ThenBy(group =>
                         group.Key.ItemId))
            {
                var children = itemGroup
                    .OrderBy(option => option.FileName)
                    .ThenBy(option => option.FileId)
                    .Select(option =>
                        new SearchableTreeItemData<
                            FileDependencyTreeNode>(
                            nextId++,
                            new FileDependencyTreeNode(
                                option.FileName,
                                option.FileId,
                                option.Extension),
                            itemGroup.Key.ItemName + " " +
                            option.FileName + " " +
                            option.Extension,
                            option.FileName))
                    .ToArray();
                result.Add(
                    new SearchableTreeItemData<
                        FileDependencyTreeNode>(
                        nextId++,
                        new FileDependencyTreeNode(
                            itemGroup.Key.ItemName),
                        itemGroup.Key.ItemName,
                        itemGroup.Key.ItemName + " · " +
                        itemGroup.Key.ItemId,
                        children));
            }

            return result;
        }
    }
}
