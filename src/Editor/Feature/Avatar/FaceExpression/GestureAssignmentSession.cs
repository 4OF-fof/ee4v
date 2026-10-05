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

        internal event Action Changed;
        internal event Action ConfigurationChanged;
        private GestureAssignmentUndoState _undoState;
        private int _revision;
        private int _nextRevision;
        private readonly bool _enableUndo;
        internal GestureAssignmentSession(bool enableUndo = false) { _enableUndo = enableUndo; }
        internal bool IsInitialConfiguration => _revision == 0;

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
        internal bool IsMenuSelection => _selectedMenuEntry != null;
        internal bool IsSelectedLeftSynced =>
            !IsMenuSelection && SyncedLeftGestures.Contains(_selectedCombination.Left);
        internal bool IsSelectedRightSynced =>
            !IsMenuSelection && SyncedRightGestures.Contains(_selectedCombination.Right);
        internal string SelectedMenuName =>
            _selectedMenuEntry?.Name ?? SelectedAssignment.MenuName;

        internal FaceExpressionAssignment SelectedAssignment =>
            _selectedMenuEntry?.Assignment ?? GetAssignment(_selectedCombination);

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
            _selectedCombination = combination;
            _selectedMenuEntry = null;
            Changed?.Invoke();
        }

        internal void Select(FaceExpressionMenuEntry entry)
        {
            if (entry == null || !Entries.Contains(entry))
            {
                return;
            }

            _selectedMenuEntry = entry;
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
            NotifyConfigurationChanged();
        }

        internal void UpdateSelection(bool enableBlink, bool fixMouth)
        {
            var current = SelectedAssignment;
            if (current.EnableBlink == enableBlink && current.FixMouth == fixMouth) { return; }
            RecordChange();
            var assignment = new FaceExpressionAssignment(
                current.Clip,
                enableBlink,
                fixMouth,
                current.MenuName);
            if (_selectedMenuEntry != null)
            {
                _selectedMenuEntry.Assignment = assignment;
            }
            else
            {
                SetAssignment(_selectedCombination, assignment);
            }

            NotifyConfigurationChanged();
        }

        internal void SetSelectedMenuName(string name)
        {
            name = name ?? string.Empty;
            if (SelectedMenuName == name) { return; }
            RecordChange();
            if (_selectedMenuEntry != null)
            {
                _selectedMenuEntry.Name = name ?? string.Empty;
                NotifyConfigurationChanged();
                return;
            }

            var current = GetAssignment(_selectedCombination);
            SetAssignment(_selectedCombination, new FaceExpressionAssignment(
                current.Clip,
                current.EnableBlink,
                current.FixMouth,
                name));
            NotifyConfigurationChanged();
        }

        internal void SetSelectedLeftSynchronized(bool synchronized)
        {
            if (_selectedMenuEntry != null)
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
            if (_selectedMenuEntry != null)
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
            RecordChange();
            var entry = new FaceExpressionMenuEntry(
                string.Empty,
                FaceExpressionAssignment.Default);
            Entries.Add(entry);
            _selectedMenuEntry = entry;
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
            NotifyConfigurationChanged();
        }
    }

}
