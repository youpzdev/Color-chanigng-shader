using UnityEngine;
using UnityEngine.Rendering.Universal;

public class XRayOutlineFeature : ScriptableRendererFeature
{
    [SerializeField] private LayerMask actorLayers;
    [SerializeField] private Material effectMaterial;

    private RenderObjectsPass maskPass;
    private RenderObjectsPass outlinePass;
    private RenderObjectsPass cleanupPass;
    private bool reportedMissingMaterial;

    public override void Create()
    {
        var cameraSettings = new RenderObjects.CustomCameraSettings();
        maskPass = CreatePass("X-ray silhouette mask", 0, cameraSettings);
        outlinePass = CreatePass("X-ray hidden outline", 1, cameraSettings);
        cleanupPass = CreatePass("X-ray stencil cleanup", 2, cameraSettings);
        reportedMissingMaterial = false;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            return;

        if (!effectMaterial)
        {
            if (!reportedMissingMaterial)
            {
                Debug.LogError("X-ray: не назначен effectMaterial в XRayOutlineFeature", this);
                reportedMissingMaterial = true;
            }

            return;
        }

        renderer.EnqueuePass(maskPass);
        renderer.EnqueuePass(outlinePass);
        renderer.EnqueuePass(cleanupPass);
    }

    private RenderObjectsPass CreatePass(string passName, int materialPass, RenderObjects.CustomCameraSettings cameraSettings)
    {
        var pass = new RenderObjectsPass(passName, RenderPassEvent.AfterRenderingOpaques, null,
            RenderQueueType.Opaque, actorLayers.value, cameraSettings);
        pass.overrideMaterial = effectMaterial;
        pass.overrideMaterialPassIndex = materialPass;

        return pass;
    }
}
