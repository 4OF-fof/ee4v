using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Editor;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor.Validation;

namespace Ee4v.AvatarInfo
{
    [InitializeOnLoad]
    internal sealed class AvatarInfoSdkProvider : IAvatarInfoSdk
    {
        private static readonly AvatarInfoSdkProvider Instance = new AvatarInfoSdkProvider();
        private IReadOnlyList<AvatarInfoSdkAvatar> _avatars;
        private string _account;
        private DateTime _lastFetch;
        private DateTime _retryAfter;
        private Task<IReadOnlyList<AvatarInfoSdkAvatar>> _fetching;
        private IVRCSdkAvatarBuilderApi _builder;
        private string _buildKey;
        private bool _mobile;

        static AvatarInfoSdkProvider()
        {
            AvatarInfoSdk.Provider = Instance;
            VRCSdkControlPanel.OnSdkPanelEnable += Instance.AttachBuilder;
            EditorApplication.delayCall += Instance.AttachExistingBuilder;
            AssemblyReloadEvents.beforeAssemblyReload += Instance.Detach;
        }

        public bool IsLoggedIn => APIUser.IsLoggedIn && APIUser.CurrentUser != null;

        public AvatarInfoParameterMemory ReadParameterMemory(GameObject avatar)
        {
            var descriptor = avatar == null ? null : avatar.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) return null;
            var parameters = descriptor.expressionParameters;
            if (descriptor.customExpressions)
            {
                return parameters == null ? null : new AvatarInfoParameterMemory
                    { Used = parameters.CalcTotalCost(), Limit = VRCExpressionParameters.MAX_PARAMETER_COST };
            }
            var defaults = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try
            {
                defaults.parameters = VRCExpressionParametersEditor.GetDefaultParameters();
                return new AvatarInfoParameterMemory
                    { Used = defaults.CalcTotalCost(), Limit = VRCExpressionParameters.MAX_PARAMETER_COST };
            }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
        }

        public Task<IReadOnlyList<AvatarInfoSdkAvatar>> GetOwnAvatars(CancellationToken cancellationToken)
        {
            if (!IsLoggedIn) throw new InvalidOperationException("Log in using the official VRChat SDK.");
            var account = APIUser.CurrentUser.id;
            if (_account == account && _avatars != null && DateTime.UtcNow - _lastFetch < TimeSpan.FromMinutes(5))
                return Task.FromResult(_avatars);
            if (_account == account && _fetching != null) return _fetching;
            if (_account == account && DateTime.UtcNow < _retryAfter)
                throw new InvalidOperationException("Please wait a minute before requesting the avatar list again.");
            if (_account != account) _retryAfter = default;
            _account = account;
            _avatars = null;
            var operation = FetchAvatars(account, cancellationToken);
            _fetching = operation.IsCompleted ? null : operation;
            return operation;
        }

        private async Task<IReadOnlyList<AvatarInfoSdkAvatar>> FetchAvatars(string account, CancellationToken cancellationToken)
        {
            try
            {
                var avatars = new Dictionary<string, AvatarInfoSdkAvatar>(StringComparer.Ordinal);
                for (var offset = 0; ;)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsLoggedIn || APIUser.CurrentUser.id != account) throw new OperationCanceledException();
                    var completion = new TaskCompletionSource<List<ApiAvatar>>();
                    using (cancellationToken.Register(() => completion.TrySetCanceled()))
                    {
                        // Use the SDK's own authenticated read request; never handle credentials or upload content.
                        ApiAvatar.FetchList((items, _) => completion.TrySetResult(items.ToList()),
                            error => completion.TrySetException(new InvalidOperationException(error)),
                            ApiAvatar.Owner.Mine, ApiAvatar.ReleaseStatus.All, null, 20, offset,
                            ApiAvatar.SortHeading.None, ApiAvatar.SortOrder.Descending,
                            null, null, true, false, null, false);
                        var page = await completion.Task;
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!IsLoggedIn || APIUser.CurrentUser.id != account) throw new OperationCanceledException();
                        if (page.Count == 0) break;
                        var added = 0;
                        foreach (var avatar in page)
                        {
                            if (string.IsNullOrEmpty(avatar.id) || avatars.ContainsKey(avatar.id)) continue;
                            avatars.Add(avatar.id, new AvatarInfoSdkAvatar { Id = avatar.id, Name = avatar.name });
                            added++;
                        }
                        if (added == 0) break;
                        offset += page.Count;
                        await Task.Delay(300, cancellationToken);
                    }
                }
                if (!IsLoggedIn || APIUser.CurrentUser.id != account) throw new OperationCanceledException();
                _avatars = avatars.Values.OrderBy(avatar => avatar.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
                _lastFetch = DateTime.UtcNow;
                return _avatars;
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                if (_account == account) _retryAfter = DateTime.UtcNow.AddMinutes(1);
                throw;
            }
            finally { if (_account == account) _fetching = null; }
        }

        public void SetBlueprintId(GameObject avatar, string id)
        {
            if (avatar == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!string.IsNullOrEmpty(id) && (!IsLoggedIn || _account != APIUser.CurrentUser.id ||
                _avatars == null || !_avatars.Any(value => value.Id == id)))
                throw new InvalidOperationException("Select one of your avatars from the SDK list.");
            var pipeline = avatar.GetComponent<PipelineManager>();
            if (pipeline == null && string.IsNullOrEmpty(id)) return;
            if (pipeline == null) pipeline = Undo.AddComponent<PipelineManager>(avatar);
            if (pipeline.blueprintId == (id ?? string.Empty)) return;
            Undo.RecordObject(pipeline, "Change Avatar Blueprint ID");
            pipeline.blueprintId = id ?? string.Empty;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pipeline);
            EditorUtility.SetDirty(pipeline);
        }

        private void AttachExistingBuilder()
        {
            if (VRCSdkControlPanel.window != null) AttachBuilder(null, EventArgs.Empty);
        }

        private void AttachBuilder(object sender, EventArgs args)
        {
            UnsubscribeBuilder();
            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out _builder)) return;
            _builder.OnSdkBuildStart += OnBuildStart;
            _builder.OnSdkBuildSuccess += OnBuildSuccess;
            _builder.OnSdkBuildError += OnBuildEnd;
            _builder.OnSdkBuildFinish += OnBuildEnd;
        }

        private void OnBuildStart(object sender, object target)
        {
            _buildKey = AvatarBuildSizeCache.GetCaptureKey(target as GameObject);
            _mobile = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
                      EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
        }

        private void OnBuildSuccess(object sender, string bundlePath)
        {
            if (_buildKey == null) return;
            try
            {
                ValidationEditorHelpers.CheckIfUncompressedAssetBundleFileTooLarge(
                    VRC.ContentType.Avatar, out var bytes, _mobile);
                AvatarBuildSizeCache.Capture(_buildKey, _mobile, bundlePath, bytes > 0 ? (long?)bytes : null);
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { _buildKey = null; }
        }

        private void OnBuildEnd(object sender, string message) => _buildKey = null;

        private void UnsubscribeBuilder()
        {
            if (_builder == null) return;
            _builder.OnSdkBuildStart -= OnBuildStart;
            _builder.OnSdkBuildSuccess -= OnBuildSuccess;
            _builder.OnSdkBuildError -= OnBuildEnd;
            _builder.OnSdkBuildFinish -= OnBuildEnd;
            _builder = null;
            _buildKey = null;
        }

        private void Detach()
        {
            UnsubscribeBuilder();
            VRCSdkControlPanel.OnSdkPanelEnable -= AttachBuilder;
            EditorApplication.delayCall -= AttachExistingBuilder;
            AvatarInfoSdk.Provider = null;
        }
    }
}
