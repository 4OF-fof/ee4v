using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure
{
    internal static class AssetVariantPreviewRenderer
    {
        internal static byte[] Render(GameObject prefab)
        {
            if (prefab == null) { throw new InvalidOperationException("The Variant Prefab is missing."); }
            var utility = new PreviewRenderUtility();
            var previousTarget = RenderTexture.active;
            GameObject instance = null;
            Texture2D image = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                utility.AddSingleGO(instance);
                instance.SetActive(true);
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    behaviour.enabled = false;
                }
                var renderers = instance.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy &&
                        (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)).ToArray();
                var bounds = new Bounds(instance.transform.position + instance.transform.up * 0.5f, Vector3.one);
                var hasBounds = false;
                foreach (var renderer in renderers)
                {
                    var mesh = renderer is SkinnedMeshRenderer ? new Mesh() :
                        renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) { continue; }
                    try
                    {
                        if (renderer is SkinnedMeshRenderer skinned)
                        {
                            skinned.BakeMesh(mesh);
                            mesh.RecalculateBounds();
                        }
                        var local = mesh.bounds;
                        for (var corner = 0; corner < 8; corner++)
                        {
                            var point = renderer.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                                new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1,
                                    (corner & 4) == 0 ? -1 : 1)));
                            if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                            else { bounds.Encapsulate(point); }
                        }
                    }
                    finally
                    {
                        if (renderer is SkinnedMeshRenderer) { UnityEngine.Object.DestroyImmediate(mesh); }
                    }
                }
                var forward = instance.transform.forward.normalized;
                var camera = utility.camera;
                camera.transform.rotation = Quaternion.LookRotation(-forward, instance.transform.up);
                var cameraBounds = new Bounds();
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1,
                            (corner & 4) == 0 ? -1 : 1));
                    var relative = Quaternion.Inverse(camera.transform.rotation) * (point - bounds.center);
                    if (corner == 0) { cameraBounds = new Bounds(relative, Vector3.zero); }
                    else { cameraBounds.Encapsulate(relative); }
                }
                utility.cameraFieldOfView = 30f;
                camera.fieldOfView = 30f;
                var halfView = Mathf.Max(0.05f, cameraBounds.extents.x, cameraBounds.extents.y);
                var distance = cameraBounds.extents.z + halfView / Mathf.Tan(15f * Mathf.Deg2Rad) * 1.12f;
                camera.transform.position = bounds.center + forward * distance;
                camera.nearClipPlane = Mathf.Max(0.001f, distance - cameraBounds.extents.z - halfView);
                camera.farClipPlane = distance + Mathf.Max(10f, bounds.size.magnitude * 2f);
                camera.clearFlags = CameraClearFlags.Color;
                camera.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f);
                utility.ambientColor = new Color(0.45f, 0.45f, 0.45f, 1f);
                utility.lights[0].intensity = 1.1f;
                utility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                utility.lights[1].intensity = 0.7f;
                utility.lights[1].transform.rotation = Quaternion.Euler(340f, 215f, 0f);
                utility.BeginStaticPreview(new Rect(0, 0, 512, 512));
                utility.Render(true);
                image = utility.EndStaticPreview();
                return image.EncodeToPNG();
            }
            finally
            {
                if (image != null) { UnityEngine.Object.DestroyImmediate(image); }
                utility.Cleanup();
                RenderTexture.active = previousTarget;
                if (instance != null) { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }
    }
}
