using System;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.AvatarEvaluation
{
    public sealed class AvatarPreviewSession : IDisposable
    {
        private PreviewSession _parent;
        private PreviewSession _session;
        private Camera _camera;
        private GameObject _root;
        private IRenderFilter _filter;
        private bool _inherits;
        private bool _disposed;

        public bool IsConnected => _session != null;

        public bool Connect(Camera camera, GameObject root, IRenderFilter filter, bool inheritPluginPreview = true)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AvatarPreviewSession));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (root == null) throw new ArgumentNullException(nameof(root));
            var enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
            var parent = enabled && inheritPluginPreview ? PreviewSession.Current : null;
            if (ReferenceEquals(_parent, parent) && (_session != null) == enabled &&
                _camera == camera && _root == root && ReferenceEquals(_filter, filter) && _inherits == inheritPluginPreview)
                return false;
            Disconnect();
            _camera = camera;
            _root = root;
            _filter = filter;
            _inherits = inheritPluginPreview;
            _parent = parent;
            if (!enabled) return true;
            try
            {
                _session = parent != null ? parent.Fork("ee4v Avatar Preview") : new PreviewSession();
                if (inheritPluginPreview)
                {
                    _session.HiddenRenderers = context => context.GetComponentsByType<Renderer>()
                        .Where(renderer => renderer != null && !renderer.transform.IsChildOf(root.transform)).ToImmutableHashSet();
                    if (filter != null)
                        _session.AddMutator(new SequencePoint { DebugString = "ee4v preview overrides" }, filter);
                }
                _session.OverrideCamera(camera);
                return true;
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        private void Disconnect()
        {
            _session?.Dispose();
            _session = null;
            _parent = null;
            if (_camera != null) PreviewSession.ClearCameraOverride(_camera);
            _camera = null;
            _root = null;
            _filter = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
        }
    }
}
