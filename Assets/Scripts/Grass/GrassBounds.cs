using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class GrassBounds : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh mesh;
    private Matrix4x4 worldToLocal;
    private float strength = -1;
    private float height = -1;

    private void OnEnable()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        mesh = null;
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        var material = meshRenderer.sharedMaterial;
        var currentMesh = meshFilter.sharedMesh;
        if (!material || !currentMesh) return;
        float wind = Mathf.Abs(material.GetFloat("_WindStrength"));
        float bladeHeight = Mathf.Max(material.GetFloat("_BladeHeight"), 0.001f);
        var inverse = transform.worldToLocalMatrix;
        if (mesh == currentMesh && strength == wind && height == bladeHeight && worldToLocal == inverse) return;
        mesh = currentMesh;
        strength = wind;
        height = bladeHeight;
        worldToLocal = inverse;
        float horizontal = 1.25f * wind;
        var padding = new Vector3(horizontal, horizontal * horizontal / (2 * bladeHeight), horizontal);
        var local = new Vector3(
            Vector3.Dot(new Vector3(Mathf.Abs(inverse.m00), Mathf.Abs(inverse.m01), Mathf.Abs(inverse.m02)), padding),
            Vector3.Dot(new Vector3(Mathf.Abs(inverse.m10), Mathf.Abs(inverse.m11), Mathf.Abs(inverse.m12)), padding),
            Vector3.Dot(new Vector3(Mathf.Abs(inverse.m20), Mathf.Abs(inverse.m21), Mathf.Abs(inverse.m22)), padding));
        var bounds = mesh.bounds;
        bounds.Expand(local * 2);
        meshRenderer.localBounds = bounds;
    }

    private void OnDisable()
    {
        if (meshRenderer) meshRenderer.ResetLocalBounds();
    }
}
