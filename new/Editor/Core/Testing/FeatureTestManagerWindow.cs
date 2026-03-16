using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Testing
{
    internal sealed class FeatureTestManagerWindow : EditorWindow
    {
        private static FeatureTestRunnerService _runnerService;

        private readonly List<FeatureTestDescriptor> _descriptors = new List<FeatureTestDescriptor>();
        private Vector2 _scrollPosition;
        private string _loadError;

        [MenuItem("Debug/ee4v Test Manager")]
        private static void ShowWindow()
        {
            var window = GetWindow<FeatureTestManagerWindow>();
            window.titleContent = new GUIContent(I18N.Get("testing.window.title"));
            window.minSize = new Vector2(640f, 280f);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureRunnerService();
            RefreshDescriptors();
        }

        private void OnDisable()
        {
            if (_runnerService != null)
            {
                _runnerService.Changed -= Repaint;
            }
        }

        private void OnInspectorUpdate()
        {
            EnsureRunnerService();
            if (_runnerService != null && _runnerService.IsRunInProgress)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            EnsureRunnerService();
            DrawToolbar();

            if (!string.IsNullOrWhiteSpace(_loadError))
            {
                EditorGUILayout.HelpBox(_loadError, MessageType.Error);
            }

            if (_descriptors.Count == 0)
            {
                EditorGUILayout.HelpBox(I18N.Get("testing.window.noSuites"), MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var descriptor in _descriptors)
            {
                DrawDescriptor(descriptor);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(I18N.Get("testing.window.refresh"), EditorStyles.toolbarButton))
                {
                    RefreshDescriptors();
                }

                using (new EditorGUI.DisabledScope(_runnerService == null || _runnerService.IsRunInProgress || _descriptors.Count == 0))
                {
                    if (GUILayout.Button(I18N.Get("testing.window.runAll"), EditorStyles.toolbarButton))
                    {
                        TryRunAll();
                    }
                }

                GUILayout.FlexibleSpace();

                var status = _runnerService != null
                    ? _runnerService.GetProgressSummary()
                    : I18N.Get("testing.window.idle");
                GUILayout.Label(status, EditorStyles.miniLabel);
            }
        }

        private void DrawDescriptor(FeatureTestDescriptor descriptor)
        {
            var record = _runnerService != null
                ? _runnerService.GetRecord(descriptor.FeatureScope)
                : null;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(descriptor.DisplayName, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(_runnerService == null || _runnerService.IsRunInProgress))
                    {
                        if (GUILayout.Button(I18N.Get("testing.window.run"), GUILayout.Width(72f)))
                        {
                            TryRun(descriptor);
                        }
                    }
                }

                EditorGUILayout.LabelField("Scope: " + descriptor.FeatureScope, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Assembly: " + descriptor.AssemblyName, EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(descriptor.Description))
                {
                    EditorGUILayout.LabelField(descriptor.Description, EditorStyles.wordWrappedMiniLabel);
                }

                if (record != null)
                {
                    EditorGUILayout.Space(3f);
                    EditorGUILayout.LabelField(
                        I18N.Get("testing.window.lastResult") + ": " + FormatStatus(record),
                        EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(FormatCounts(record), EditorStyles.miniLabel);

                    if (!string.IsNullOrWhiteSpace(record.Message))
                    {
                        EditorGUILayout.HelpBox(
                            record.Message,
                            record.Status == FeatureTestRunStatus.Failed ? MessageType.Error : MessageType.Info);
                    }
                }
            }

            EditorGUILayout.Space(4f);
        }

        private void RefreshDescriptors()
        {
            EnsureRunnerService();
            _descriptors.Clear();
            _loadError = null;

            try
            {
                _descriptors.AddRange(FeatureTestRegistry.Refresh());
            }
            catch (Exception exception)
            {
                _loadError = exception.Message;
            }

            Repaint();
        }

        private void TryRun(FeatureTestDescriptor descriptor)
        {
            EnsureRunnerService();
            if (_runnerService == null)
            {
                return;
            }

            if (!_runnerService.TryRun(descriptor, out var errorMessage))
            {
                EditorUtility.DisplayDialog(I18N.Get("testing.window.title"), errorMessage, "OK");
            }
        }

        private void TryRunAll()
        {
            EnsureRunnerService();
            if (_runnerService == null)
            {
                return;
            }

            if (!_runnerService.TryRunAll(_descriptors, out var errorMessage))
            {
                EditorUtility.DisplayDialog(I18N.Get("testing.window.title"), errorMessage, "OK");
            }
        }

        private static string FormatStatus(FeatureTestRunRecord record)
        {
            switch (record.Status)
            {
                case FeatureTestRunStatus.Running:
                    return I18N.Get("testing.status.running");
                case FeatureTestRunStatus.Passed:
                    return I18N.Get("testing.status.passed");
                case FeatureTestRunStatus.Failed:
                    return I18N.Get("testing.status.failed");
                case FeatureTestRunStatus.Skipped:
                    return I18N.Get("testing.status.skipped");
                case FeatureTestRunStatus.Inconclusive:
                    return I18N.Get("testing.status.inconclusive");
                case FeatureTestRunStatus.NotRun:
                default:
                    return I18N.Get("testing.status.notRun");
            }
        }

        private static string FormatCounts(FeatureTestRunRecord record)
        {
            if (record.Status == FeatureTestRunStatus.NotRun)
            {
                return I18N.Get("testing.window.notRunYet");
            }

            return string.Format(
                "Pass {0}  Fail {1}  Skip {2}  Inc {3}  {4:0.00}s",
                record.PassCount,
                record.FailCount,
                record.SkipCount,
                record.InconclusiveCount,
                record.DurationSeconds);
        }

        private static void EnsureRunnerService()
        {
            if (_runnerService != null)
            {
                return;
            }

            _runnerService = new FeatureTestRunnerService();
            _runnerService.Changed -= RepaintAllOpenWindows;
            _runnerService.Changed += RepaintAllOpenWindows;
        }

        private static void RepaintAllOpenWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<FeatureTestManagerWindow>();
            foreach (var window in windows)
            {
                window.Repaint();
            }
        }
    }
}
