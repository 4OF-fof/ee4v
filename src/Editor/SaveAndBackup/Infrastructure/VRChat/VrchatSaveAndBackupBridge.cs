#if EE4V_VRCSDK_AVATARS
using System;
using Ee4v.SaveAndBackup.Composition;
using UnityEditor;
using UnityEngine;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor;

namespace Ee4v.SaveAndBackup.Infrastructure.VRChat
{
    [InitializeOnLoad]
    internal static class VrchatSaveAndBackupBridge
    {
        private static IVRCSdkAvatarBuilderApi _builder;

        static VrchatSaveAndBackupBridge()
        {
            VRCSdkControlPanel.OnSdkPanelEnable -=
                OnPanelEnabled;
            VRCSdkControlPanel.OnSdkPanelEnable +=
                OnPanelEnabled;
            AttachBuilder();
        }

        private static void OnPanelEnabled(
            object sender,
            EventArgs args)
        {
            AttachBuilder();
        }

        private static void AttachBuilder()
        {
            if (!VRCSdkControlPanel.TryGetBuilder<
                    IVRCSdkAvatarBuilderApi>(
                    out var builder))
            {
                return;
            }

            if (_builder != null)
            {
                _builder.OnSdkBuildStart -= OnBuildStarted;
                _builder.OnSdkBuildSuccess -=
                    OnBuildSucceeded;
                _builder.OnSdkBuildError -= OnBuildFailed;
                _builder.OnSdkUploadSuccess -=
                    OnUploadSucceeded;
                _builder.OnSdkUploadError -= OnUploadFailed;
            }

            _builder = builder;
            _builder.OnSdkBuildStart += OnBuildStarted;
            _builder.OnSdkBuildSuccess += OnBuildSucceeded;
            _builder.OnSdkBuildError += OnBuildFailed;
            _builder.OnSdkUploadSuccess += OnUploadSucceeded;
            _builder.OnSdkUploadError += OnUploadFailed;
        }

        private static void OnBuildStarted(
            object sender,
            object target)
        {
            SaveAndBackupBuildEventSink.BuildStarted(
                target as GameObject);
        }

        private static void OnBuildSucceeded(
            object sender,
            string outputPath)
        {
            SaveAndBackupBuildEventSink.BuildSucceeded(
                outputPath);
        }

        private static void OnBuildFailed(
            object sender,
            string error)
        {
            SaveAndBackupBuildEventSink.BuildFailed();
        }

        private static void OnUploadSucceeded(
            object sender,
            string externalId)
        {
            SaveAndBackupBuildEventSink.UploadSucceeded(
                externalId);
        }

        private static void OnUploadFailed(
            object sender,
            string error)
        {
            SaveAndBackupBuildEventSink.UploadFailed();
        }
    }
}
#endif
