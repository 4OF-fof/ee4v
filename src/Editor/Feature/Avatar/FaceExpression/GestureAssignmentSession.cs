using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class GestureAssignmentSession
    {
        internal static GestureAssignmentSession Active { get; private set; } = new GestureAssignmentSession();
        internal static event Action ActiveChanged;

        internal void Activate()
        {
            if (ReferenceEquals(Active, this)) { return; }
            Active = this;
            ActiveChanged?.Invoke();
        }

        internal void Deactivate()
        {
            if (!ReferenceEquals(Active, this)) { return; }
            Active = new GestureAssignmentSession();
            ActiveChanged?.Invoke();
        }

        private readonly Dictionary<GestureCombination, FaceExpressionAssignment>
            Assignments = new Dictionary<GestureCombination, FaceExpressionAssignment>();
        private readonly List<FaceExpressionMenuEntry> Entries =
            new List<FaceExpressionMenuEntry>();
        private readonly HashSet<FaceGesture> SyncedLeftGestures =
            new HashSet<FaceGesture>();
        private readonly HashSet<FaceGesture> SyncedRightGestures =
            new HashSet<FaceGesture>();
        private GestureCombination _selectedCombination =
            new GestureCombination(FaceGesture.Neutral, FaceGesture.Neutral);
        private FaceExpressionMenuEntry _selectedMenuEntry;
        private bool _hasSelection;
        private bool _selectAllLeft;
        private bool _selectAllRight;

        internal event Action Changed;
        internal event Action ConfigurationChanged;
        private GestureAssignmentUndoState _undoState;
        private int _revision;
        private int _nextRevision;
        private readonly bool _enableUndo;
        internal GestureAssignmentSession(bool enableUndo = false) { _enableUndo = enableUndo; }
        internal bool IsInitialConfiguration => _revision == 0;
        internal int Revision => _revision;

        private void RecordChange()
        {
            if (!_enableUndo) { return; }
            if (_undoState == null)
            {
                _undoState = ScriptableObject.CreateInstance<GestureAssignmentUndoState>();
                _undoState.hideFlags = HideFlags.HideAndDontSave;
                SaveUndoState();
            }
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(_undoState, "Change Face Expression Assignment");
        }

        private void NotifyConfigurationChanged()
        {
            _revision = ++_nextRevision;
            SaveUndoState();
            Changed?.Invoke();
            ConfigurationChanged?.Invoke();
        }

        private void SaveUndoState()
        {
            if (_undoState == null) { return; }
            _undoState.revision = _revision;
            _undoState.assignments = Assignments.Select(pair => new GestureAssignmentUndoEntry
            {
                left = pair.Key.Left, right = pair.Key.Right, clip = pair.Value.Clip,
                blink = pair.Value.EnableBlink, mouth = pair.Value.FixMouth, menuName = pair.Value.MenuName
            }).ToArray();
            _undoState.menu = Entries.Select(entry => new GestureAssignmentUndoEntry
            {
                clip = entry.Assignment.Clip, blink = entry.Assignment.EnableBlink,
                mouth = entry.Assignment.FixMouth, name = entry.Name, menuName = entry.Assignment.MenuName
            }).ToArray();
            _undoState.left = SyncedLeftGestures.ToArray();
            _undoState.right = SyncedRightGestures.ToArray();
            EditorUtility.SetDirty(_undoState);
        }

        internal bool RestoreUndo()
        {
            if (_undoState == null || _undoState.revision == _revision) { return false; }
            var selectedIndex = Entries.IndexOf(_selectedMenuEntry);
            var wasMenuSelection = IsMenuSelection;
            _revision = _undoState.revision;
            Assignments.Clear();
            foreach (var entry in _undoState.assignments)
            {
                Assignments[new GestureCombination(entry.left, entry.right)] = entry.Assignment;
            }
            Entries.Clear();
            foreach (var entry in _undoState.menu)
            {
                Entries.Add(new FaceExpressionMenuEntry(entry.name, entry.Assignment));
            }
            _selectedMenuEntry = selectedIndex >= 0 && selectedIndex < Entries.Count ? Entries[selectedIndex] : null;
            if (wasMenuSelection && _selectedMenuEntry == null) { _hasSelection = false; }
            SyncedLeftGestures.Clear();
            SyncedLeftGestures.UnionWith(_undoState.left);
            SyncedRightGestures.Clear();
            SyncedRightGestures.UnionWith(_undoState.right);
            Changed?.Invoke();
            return true;
        }

        internal void Dispose()
        {
            Deactivate();
            if (_undoState != null)
            {
                Undo.ClearUndo(_undoState);
                UnityEngine.Object.DestroyImmediate(_undoState);
                _undoState = null;
            }
        }

        internal IReadOnlyList<FaceExpressionMenuEntry> MenuEntries => Entries;
        internal GestureCombination SelectedCombination => _selectedCombination;
        internal FaceExpressionMenuEntry SelectedMenuEntry => _selectedMenuEntry;
        internal bool HasSelection => _hasSelection;
        internal bool IsMenuSelection => _hasSelection && _selectedMenuEntry != null;
        internal bool IsAllLeftSelected => HasSelection && !IsMenuSelection && _selectAllLeft;
        internal bool IsAllRightSelected => HasSelection && !IsMenuSelection && _selectAllRight;
        internal int SelectedCombinationCount => HasSelection && !IsMenuSelection
            ? (_selectAllLeft ? Enum.GetValues(typeof(FaceGesture)).Length : 1) *
              (_selectAllRight ? Enum.GetValues(typeof(FaceGesture)).Length : 1)
            : 0;
        internal bool HasSelectedClip => HasSelection && (IsMenuSelection
            ? SelectedAssignment.Clip != null
            : GetSelectedCombinations().Any(combination => GetAssignment(combination).Clip != null));
        internal IEnumerable<AnimationClip> SelectedClips => !HasSelection
            ? Enumerable.Empty<AnimationClip>()
            : IsMenuSelection
                ? new[] { SelectedAssignment.Clip }.Where(clip => clip != null)
                : GetSelectedCombinations().Select(combination => GetAssignment(combination).Clip)
                    .Where(clip => clip != null).Distinct();
        internal bool IsSelectedLeftSynced =>
            HasSelection && !IsMenuSelection && SyncedLeftGestures.Contains(_selectedCombination.Left);
        internal bool IsSelectedRightSynced =>
            HasSelection && !IsMenuSelection && SyncedRightGestures.Contains(_selectedCombination.Right);
        internal string SelectedMenuName =>
            _selectedMenuEntry?.Name ?? SelectedAssignment.MenuName;

        internal FaceExpressionAssignment SelectedAssignment =>
            HasSelection ? _selectedMenuEntry?.Assignment ?? GetAssignment(_selectedCombination) : FaceExpressionAssignment.Default;

        internal void SetConfiguration(FaceExpressionConfiguration configuration)
        {
            if (_undoState != null) { Undo.ClearUndo(_undoState); }
            _revision = 0;
            Assignments.Clear();
            var source = configuration?.Assignments;
            foreach (FaceGesture left in Enum.GetValues(typeof(FaceGesture)))
            {
                foreach (FaceGesture right in Enum.GetValues(typeof(FaceGesture)))
                {
                    var combination = new GestureCombination(left, right);
                    Assignments[combination] = source != null &&
                                               source.TryGetValue(combination, out var assignment)
                        ? assignment
                        : FaceExpressionAssignment.Default;
                }
            }

            Entries.Clear();
            if (configuration?.MenuEntries != null)
            {
                for (var index = 0; index < configuration.MenuEntries.Count; index++)
                {
                    var entry = configuration.MenuEntries[index];
                    Entries.Add(new FaceExpressionMenuEntry(entry.Name, entry.Assignment));
                }
            }

            _selectedMenuEntry = null;
            _hasSelection = false;
            _selectAllLeft = false;
            _selectAllRight = false;
            SaveUndoState();
            Changed?.Invoke();
        }

        internal FaceExpressionConfiguration CreateConfiguration()
        {
            var entries = new FaceExpressionMenuEntry[Entries.Count];
            for (var index = 0; index < Entries.Count; index++)
            {
                entries[index] = new FaceExpressionMenuEntry(
                    Entries[index].Name,
                    Entries[index].Assignment);
            }

            return new FaceExpressionConfiguration(
                new Dictionary<GestureCombination, FaceExpressionAssignment>(Assignments),
                entries);
        }

        internal FaceExpressionAssignment GetAssignment(
            GestureCombination combination)
        {
            return Assignments.TryGetValue(combination, out var assignment)
                ? assignment
                : FaceExpressionAssignment.Default;
        }

        internal void Select(GestureCombination combination)
        {
            _hasSelection = true;
            _selectedCombination = combination;
            _selectedMenuEntry = null;
            _selectAllLeft = false;
            _selectAllRight = false;
            Changed?.Invoke();
        }

        internal void SelectHand(bool left, FaceGesture? gesture)
        {
            if (left)
            {
                _selectAllLeft = !gesture.HasValue;
                if (gesture.HasValue)
                {
                    _selectedCombination = new GestureCombination(gesture.Value, _selectedCombination.Right);
                }
            }
            else
            {
                _selectAllRight = !gesture.HasValue;
                if (gesture.HasValue)
                {
                    _selectedCombination = new GestureCombination(_selectedCombination.Left, gesture.Value);
                }
            }
            _hasSelection = true;
            _selectedMenuEntry = null;
            Changed?.Invoke();
        }

        internal bool IsCombinationSelected(GestureCombination combination)
        {
            return HasSelection && !IsMenuSelection &&
                   (_selectAllLeft || combination.Left == _selectedCombination.Left) &&
                   (_selectAllRight || combination.Right == _selectedCombination.Right);
        }

        private IEnumerable<GestureCombination> GetSelectedCombinations()
        {
            foreach (FaceGesture left in Enum.GetValues(typeof(FaceGesture)))
            {
                if (!_selectAllLeft && left != _selectedCombination.Left) { continue; }
                foreach (FaceGesture right in Enum.GetValues(typeof(FaceGesture)))
                {
                    if (!_selectAllRight && right != _selectedCombination.Right) { continue; }
                    yield return new GestureCombination(left, right);
                }
            }
        }

        internal bool HasMixedSelection<T>(Func<FaceExpressionAssignment, T> value)
        {
            if (!HasSelection || IsMenuSelection) { return false; }
            var selected = value(SelectedAssignment);
            return GetSelectedCombinations().Any(combination =>
                !EqualityComparer<T>.Default.Equals(selected, value(GetAssignment(combination))));
        }

        internal void Select(FaceExpressionMenuEntry entry)
        {
            if (entry == null || !Entries.Contains(entry))
            {
                return;
            }

            _hasSelection = true;
            _selectedMenuEntry = entry;
            _selectAllLeft = false;
            _selectAllRight = false;
            Changed?.Invoke();
        }

        internal void ClearSelection()
        {
            if (!HasSelection) { return; }
            _hasSelection = false;
            _selectedMenuEntry = null;
            _selectAllLeft = false;
            _selectAllRight = false;
            Changed?.Invoke();
        }

        internal void SetClip(
            GestureCombination combination,
            AnimationClip clip)
        {
            var current = GetAssignment(combination);
            if (current.Clip == clip) { Select(combination); return; }
            RecordChange();
            SetAssignment(combination, new FaceExpressionAssignment(
                clip,
                current.EnableBlink,
                current.FixMouth,
                current.MenuName));
            _selectedCombination = combination;
            _selectedMenuEntry = null;
            _hasSelection = true;
            _selectAllLeft = false;
            _selectAllRight = false;
            NotifyConfigurationChanged();
        }

        internal void SetClip(
            FaceExpressionMenuEntry entry,
            AnimationClip clip)
        {
            if (entry == null || !Entries.Contains(entry))
            {
                return;
            }

            if (entry.Assignment.Clip == clip) { Select(entry); return; }
            RecordChange();
            entry.Assignment = new FaceExpressionAssignment(
                clip,
                entry.Assignment.EnableBlink,
                entry.Assignment.FixMouth,
                entry.Assignment.MenuName);
            _selectedMenuEntry = entry;
            _hasSelection = true;
            _selectAllLeft = false;
            _selectAllRight = false;
            NotifyConfigurationChanged();
        }

        internal void UpdateSelection(bool enableBlink, bool fixMouth)
        {
            UpdateSelectedAssignments(current => new FaceExpressionAssignment(
                current.Clip, enableBlink, fixMouth, current.MenuName));
        }

        internal void SetSelectedClip(AnimationClip clip)
        {
            UpdateSelectedAssignments(current => new FaceExpressionAssignment(
                clip, current.EnableBlink, current.FixMouth, current.MenuName));
        }

        internal void SetSelectedExpression(AnimationClip clip, string menuName)
        {
            if (!HasSelection) { return; }
            if (IsMenuSelection)
            {
                menuName = menuName ?? _selectedMenuEntry.Name;
                var current = _selectedMenuEntry.Assignment;
                if (current.Clip == clip && _selectedMenuEntry.Name == menuName) { return; }
                RecordChange();
                _selectedMenuEntry.Name = menuName;
                _selectedMenuEntry.Assignment = new FaceExpressionAssignment(
                    clip, current.EnableBlink, current.FixMouth, current.MenuName);
                NotifyConfigurationChanged();
                return;
            }
            UpdateSelectedAssignments(current => new FaceExpressionAssignment(
                clip, current.EnableBlink, current.FixMouth, menuName ?? current.MenuName));
        }

        internal void SetSelectedBlink(bool enableBlink)
        {
            UpdateSelectedAssignments(current => new FaceExpressionAssignment(
                current.Clip, enableBlink, current.FixMouth, current.MenuName));
        }

        internal void SetSelectedMouth(bool fixMouth)
        {
            UpdateSelectedAssignments(current => new FaceExpressionAssignment(
                current.Clip, current.EnableBlink, fixMouth, current.MenuName));
        }

        private void UpdateSelectedAssignments(Func<FaceExpressionAssignment, FaceExpressionAssignment> update)
        {
            if (!HasSelection) { return; }
            if (IsMenuSelection)
            {
                var assignment = update(_selectedMenuEntry.Assignment);
                if (AssignmentEquals(assignment, _selectedMenuEntry.Assignment)) { return; }
                RecordChange();
                _selectedMenuEntry.Assignment = assignment;
            }
            else
            {
                var changes = GetSelectedCombinations()
                    .Select(combination => new KeyValuePair<GestureCombination, FaceExpressionAssignment>(
                        combination, update(GetAssignment(combination))))
                    .Where(pair => !AssignmentEquals(pair.Value, GetAssignment(pair.Key)))
                    .ToArray();
                if (changes.Length == 0) { return; }
                RecordChange();
                foreach (var change in changes) { SetAssignment(change.Key, change.Value); }
            }
            NotifyConfigurationChanged();
        }

        private static bool AssignmentEquals(FaceExpressionAssignment left, FaceExpressionAssignment right)
        {
            return left.Clip == right.Clip && left.EnableBlink == right.EnableBlink &&
                   left.FixMouth == right.FixMouth && left.MenuName == right.MenuName;
        }

        internal void SetSelectedMenuName(string name)
        {
            if (!HasSelection) { return; }
            name = name ?? string.Empty;
            if (_selectedMenuEntry != null)
            {
                if (SelectedMenuName == name) { return; }
                RecordChange();
                _selectedMenuEntry.Name = name ?? string.Empty;
                NotifyConfigurationChanged();
                return;
            }

            UpdateSelectedAssignments(current => new FaceExpressionAssignment(
                current.Clip, current.EnableBlink, current.FixMouth, name));
        }

        internal void SetSelectedLeftSynchronized(bool synchronized)
        {
            if (!HasSelection || _selectedMenuEntry != null)
            {
                return;
            }

            if (IsSelectedLeftSynced == synchronized) { return; }
            RecordChange();
            if (synchronized)
            {
                SyncedLeftGestures.Add(_selectedCombination.Left);
                SynchronizeAssignment(
                    _selectedCombination,
                    SelectedAssignment);
            }
            else
            {
                SyncedLeftGestures.Remove(_selectedCombination.Left);
            }

            NotifyConfigurationChanged();
        }

        internal void SetSelectedRightSynchronized(bool synchronized)
        {
            if (!HasSelection || _selectedMenuEntry != null)
            {
                return;
            }

            if (IsSelectedRightSynced == synchronized) { return; }
            RecordChange();
            if (synchronized)
            {
                SyncedRightGestures.Add(_selectedCombination.Right);
                SynchronizeAssignment(
                    _selectedCombination,
                    SelectedAssignment);
            }
            else
            {
                SyncedRightGestures.Remove(_selectedCombination.Right);
            }

            NotifyConfigurationChanged();
        }

        internal void ResetSynchronization()
        {
            SyncedLeftGestures.Clear();
            SyncedRightGestures.Clear();
        }

        private void SetAssignment(
            GestureCombination combination,
            FaceExpressionAssignment assignment)
        {
            Assignments[combination] = assignment;
            SynchronizeAssignment(combination, assignment);
        }

        private void SynchronizeAssignment(
            GestureCombination source,
            FaceExpressionAssignment assignment)
        {
            var pending = new Queue<GestureCombination>();
            var visited = new HashSet<GestureCombination>();
            pending.Enqueue(source);
            while (pending.Count > 0)
            {
                var combination = pending.Dequeue();
                if (!visited.Add(combination))
                {
                    continue;
                }

                Assignments[combination] = assignment;
                if (SyncedLeftGestures.Contains(combination.Left))
                {
                    foreach (FaceGesture right in Enum.GetValues(typeof(FaceGesture)))
                    {
                        pending.Enqueue(new GestureCombination(
                            combination.Left,
                            right));
                    }
                }

                if (SyncedRightGestures.Contains(combination.Right))
                {
                    foreach (FaceGesture left in Enum.GetValues(typeof(FaceGesture)))
                    {
                        pending.Enqueue(new GestureCombination(
                            left,
                            combination.Right));
                    }
                }
            }
        }

        internal void AddMenuExpression()
        {
            AddMenuExpression(null, string.Empty);
        }

        internal void AddMenuExpression(AnimationClip clip, string name, bool enableBlink = true, bool fixMouth = false)
        {
            RecordChange();
            var entry = new FaceExpressionMenuEntry(
                name ?? string.Empty,
                new FaceExpressionAssignment(clip, enableBlink, fixMouth));
            Entries.Add(entry);
            _selectedMenuEntry = entry;
            _hasSelection = true;
            _selectAllLeft = false;
            _selectAllRight = false;
            NotifyConfigurationChanged();
        }

        internal void RemoveSelectedMenuExpression()
        {
            if (_selectedMenuEntry == null || !Entries.Contains(_selectedMenuEntry))
            {
                return;
            }

            RecordChange();
            Entries.Remove(_selectedMenuEntry);
            _selectedMenuEntry = null;
            _hasSelection = false;
            NotifyConfigurationChanged();
        }
    }

}
