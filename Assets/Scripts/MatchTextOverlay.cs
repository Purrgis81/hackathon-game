
using System;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class MatchTextOverlay : IDisposable
{
    readonly Camera source;
    readonly Camera overlay;
    readonly CommandBuffer commands;
    static readonly int ResultAnimation = Shader.PropertyToID("_ResultAnimation");
    readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    GameObject current;
    MeshRenderer[] renderers;
    public bool Visible => overlay != null && overlay.enabled;

    public MatchTextOverlay(Camera source, Transform owner)
    {
        this.source = source;
        overlay = new GameObject("Match Text Camera").AddComponent<Camera>();
        overlay.transform.SetParent(owner);
        overlay.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
        overlay.enabled = false;
        overlay.orthographic = true;
        overlay.orthographicSize = 5;
        overlay.clearFlags = CameraClearFlags.Depth;
        overlay.cullingMask = 0;
        overlay.renderingPath = RenderingPath.Forward;
        overlay.allowHDR = false;
        overlay.allowMSAA = false;
        overlay.useOcclusionCulling = false;
        commands = new CommandBuffer { name = "Match Text" };
        overlay.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, commands);
    }

    public static GameObject Load(string name) => Resources.Load<GameObject>("MatchText/" + name);

    public void Show(GameObject prefab, float viewportHeight, float scale, Vector4 resultAnimation = default)
    {
        if (prefab == null || !source.isActiveAndEnabled)
        {
            Hide();
            return;
        }
        if (current != prefab)
        {
            current = prefab;
            renderers = prefab.GetComponentsInChildren<MeshRenderer>();
            Array.Sort(renderers, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
        }

        overlay.depth = source.depth + 1;
        overlay.targetDisplay = source.targetDisplay;
        overlay.targetTexture = source.targetTexture;
        overlay.rect = source.rect;
        overlay.aspect = source.aspect;

        float size = scale * Mathf.Min(1, source.aspect / 1.6f);
        float y = (viewportHeight - .5f) * (2 * overlay.orthographicSize);
        var placement = Matrix4x4.TRS(new Vector3(0, y, 0), Quaternion.identity, Vector3.one * size);
        commands.Clear();
        properties.SetVector(ResultAnimation, resultAnimation);
        foreach (var renderer in renderers)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var fromPrefab = prefab.transform.worldToLocalMatrix * renderer.localToWorldMatrix;
            commands.DrawMesh(mesh, placement * fromPrefab, renderer.sharedMaterial, 0, 0, properties);
        }
        overlay.enabled = true;
    }

    public void Hide() { if (overlay != null) overlay.enabled = false; }

    public void Dispose()
    {
        if (overlay != null)
        {
            overlay.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, commands);
            UnityEngine.Object.Destroy(overlay.gameObject);
        }
        commands.Dispose();
    }
}

