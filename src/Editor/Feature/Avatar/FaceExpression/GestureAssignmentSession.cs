using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal static class GestureAssignmentSession
    {
        private static readonly Dictionary<GestureCombination, FaceExpressionAssignment>
            Assignments = new Dictionary<GestureCombination, FaceExpressionAssignment>();
        private static readonly List<FaceExpressionMenuEntry> Entries =
            new List<FaceExpressionMenuEntry>();
        private static readonly HashSet<FaceGesture> SyncedLeftGestures =
            new HashSet<FaceGesture>();
        private static readonly HashSet<FaceGesture> SyncedRightGestures =
            new HashSet<FaceGesture>();
        private static GestureCombination _selectedCombination =
            new GestureCombination(FaceGesture.Neutral, FaceGesture.Neutral);
        private static FaceExpressionMenuEntry _selectedMenuEntry;

        internal static event Action Changed;

        internal static IReadOnlyList<FaceExpressionMenuEntry> MenuEntries => Entries;
        internal static GestureCombination SelectedCombination => _selectedCombination;
        internal static FaceExpressionMenuEntry SelectedMenuEntry => _selectedMenuEntry;
        internal static bool IsMenuSelection => _selectedMenuEntry != null;
        internal static bool IsSelectedLeftSynced =>
            !IsMenuSelection && SyncedLeftGestures.Contains(_selectedCombination.Left);
        internal static bool IsSelectedRightSynced =>
            !IsMenuSelection && SyncedRightGestures.Contains(_selectedCombination.Right);
        internal static string SelectedMenuName =>
            _selectedMenuEntry?.Name ?? SelectedAssignment.MenuName;

        internal static FaceExpressionAssignment SelectedAssignment =>
            _selectedMenuEntry?.Assignment ?? GetAssignment(_selectedCombination);

        internal static void SetConfiguration(FaceExpressionConfiguration configuration)
        {
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
            Changed?.Invoke();
        }

        internal static FaceExpressionConfiguration CreateConfiguration()
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

        internal static FaceExpressionAssignment GetAssignment(
            GestureCombination combination)
        {
            return Assignments.TryGetValue(combination, out var assignment)
                ? assignment
                : FaceExpressionAssignment.Default;
        }

        internal static void Select(GestureCombination combination)
        {
            _selectedCombination = combination;
            _selectedMenuEntry = null;
            Changed?.Invoke();
        }

        internal static void Select(FaceExpressionMenuEntry entry)
        {
            if (entry == null || !Entries.Contains(entry))
            {
                return;
            }

            _selectedMenuEntry = entry;
            Changed?.Invoke();
        }

        internal static void SetClip(
            GestureCombination combination,
            AnimationClip clip)
        {
            var current = GetAssignment(combination);
            SetAssignment(combination, new FaceExpressionAssignment(
                clip,
                current.EnableBlink,
                current.FixMouth,
                current.MenuName));
            _selectedCombination = combination;
            _selectedMenuEntry = null;
            Changed?.Invoke();
        }

        internal static void SetClip(
            FaceExpressionMenuEntry entry,
            AnimationClip clip)
        {
            if (entry == null || !Entries.Contains(entry))
            {
                return;
            }

            entry.Assignment = new FaceExpressionAssignment(
                clip,
                entry.Assignment.EnableBlink,
                entry.Assignment.FixMouth,
                entry.Assignment.MenuName);
            _selectedMenuEntry = entry;
            Changed?.Invoke();
        }

        internal static void UpdateSelection(bool enableBlink, bool fixMouth)
        {
            var current = SelectedAssignment;
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

            Changed?.Invoke();
        }

        internal static void SetSelectedMenuName(string name)
        {
            if (_selectedMenuEntry != null)
            {
                _selectedMenuEntry.Name = name ?? string.Empty;
                Changed?.Invoke();
                return;
            }

            var current = GetAssignment(_selectedCombination);
            SetAssignment(_selectedCombination, new FaceExpressionAssignment(
                current.Clip,
                current.EnableBlink,
                current.FixMouth,
                name));
            Changed?.Invoke();
        }

        internal static void SetSelectedLeftSynchronized(bool synchronized)
        {
            if (_selectedMenuEntry != null)
            {
                return;
            }

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

            Changed?.Invoke();
        }

        internal static void SetSelectedRightSynchronized(bool synchronized)
        {
            if (_selectedMenuEntry != null)
            {
                return;
            }

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

            Changed?.Invoke();
        }

        internal static void ResetSynchronization()
        {
            SyncedLeftGestures.Clear();
            SyncedRightGestures.Clear();
        }

        private static void SetAssignment(
            GestureCombination combination,
            FaceExpressionAssignment assignment)
        {
            Assignments[combination] = assignment;
            SynchronizeAssignment(combination, assignment);
        }

        private static void SynchronizeAssignment(
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

        internal static void AddMenuExpression()
        {
            var entry = new FaceExpressionMenuEntry(
                string.Empty,
                FaceExpressionAssignment.Default);
            Entries.Add(entry);
            _selectedMenuEntry = entry;
            Changed?.Invoke();
        }

        internal static void RemoveSelectedMenuExpression()
        {
            if (_selectedMenuEntry == null || !Entries.Remove(_selectedMenuEntry))
            {
                return;
            }

            _selectedMenuEntry = null;
            Changed?.Invoke();
        }
    }
}
