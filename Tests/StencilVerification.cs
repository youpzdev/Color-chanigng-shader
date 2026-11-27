using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class StencilVerification
{
    public static string Run()
    {
        var root = new GameObject("Stencil pixel verification");
        var previous = RenderTexture.active;
        var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
        Material actorMaterial = null;
        Material blockerMaterial = null;
        Material portalMaterial = null;
        Material maskMaterial = null;
        var outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StencilBuffer/XRayOutline.mat");
        var previousOutline = outlineMaterial.GetColor("_OutlineColor");
        try
        {
            outlineMaterial.SetColor("_OutlineColor", new Color(0, 0.9f, 1, 1));
            ShaderUtil.allowAsyncCompilation = false;
            root.hideFlags = HideFlags.HideAndDontSave;
            var camera = new GameObject("Verification Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(100, 1, 90);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 1.7f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30;
            camera.backgroundColor = Color.black;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = false;
            camera.enabled = false;
            camera.targetTexture = target;
            var data = camera.GetUniversalAdditionalCameraData();
            data.SetRenderer(1);
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            actorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            actorMaterial.SetColor("_BaseColor", new Color(0.85f, 0.4f, 0.05f));
            actorMaterial.EnableKeyword("_EMISSION");
            actorMaterial.SetColor("_EmissionColor", new Color(0.5f, 0.2f, 0.01f));
            blockerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            blockerMaterial.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f));
            var actor = Primitive(root.transform, PrimitiveType.Capsule, "Actor", new Vector3(100, 1, 100), Vector3.one, actorMaterial);
            actor.layer = LayerMask.NameToLayer("StencilActor");
            var blocker = Primitive(root.transform, PrimitiveType.Cube, "Blocker", new Vector3(100, 1, 98), new Vector3(4, 4, 0.4f), blockerMaterial);
            Render(camera, pixels, target);
            var full = Render(camera, pixels, target);
            var diagnostic = System.IO.Path.GetFullPath("../../../outputs/02-stencil-buffer/shots/xray-pixel-test.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(diagnostic));
            System.IO.File.WriteAllBytes(diagnostic, pixels.EncodeToPNG());
            int hiddenOutline = Count(full, IsOutline);
            Require(hiddenOutline > 80, "Fully hidden actor: expected outline, got " + hiddenOutline + " cyan pixels");
            Require(!IsOutline(full[128 * 256 + 128]), "Stencil mask leaked outline fill inside silhouette");
            blocker.SetActive(false);
            var visible = Render(camera, pixels, target);
            int visibleOutline = Count(visible, IsOutline);
            Require(visibleOutline == 0, "Visible actor has outline: " + visibleOutline + " pixels");
            Require(Count(visible, IsOrange) > 500, "Visible actor is missing from frame");
            blocker.SetActive(true);
            blocker.transform.position = new Vector3(100, 0.35f, 98);
            blocker.transform.localScale = new Vector3(4, 1.3f, 0.4f);
            var partial = Render(camera, pixels, target);
            int partialOutline = Count(partial, IsOutline);
            Require(partialOutline > 20 && partialOutline < hiddenOutline, "Partial occlusion: outline " + partialOutline + ", full " + hiddenOutline);
            Require(Count(partial, IsOrange) > 200, "Partial occlusion hid the visible half");
            for (int y = 140; y < 256; y++)
                for (int x = 0; x < 256; x++)
                    Require(!IsOutline(partial[y * 256 + x]), "Visible upper half has outline at " + x + "," + y);
            actor.SetActive(false);
            blocker.SetActive(false);
            var afterActor = Render(camera, pixels, target);
            Require(Count(afterActor, IsOutline) == 0, "Outline persists after actor is disabled");
            portalMaterial = new Material(Shader.Find("Learning/Stencil/PortalRoom"));
            portalMaterial.SetColor("_BaseColor", Color.black);
            portalMaterial.SetColor("_EmissionColor", new Color(1, 0.01f, 0.65f));
            maskMaterial = new Material(Shader.Find("Learning/Stencil/PortalMask"));
            var portal = Primitive(root.transform, PrimitiveType.Quad, "Mask", new Vector3(100, 1, 99), new Vector3(1.4f, 2, 1), maskMaterial);
            Primitive(root.transform, PrimitiveType.Cube, "Room contents", new Vector3(100, 1, 101), new Vector3(4, 3, 0.3f), portalMaterial);
            Render(camera, pixels, target);
            var portalFrame = Render(camera, pixels, target);
            int portalPixels = Count(portalFrame, IsPortal);
            Require(portalPixels > 1000, "Portal contains no second-room pixels: " + portalPixels);
            AssertOutsideMask(camera, portal.transform, portalFrame);
            portal.SetActive(false);
            int withoutMask = Count(Render(camera, pixels, target), IsPortal);
            Require(withoutMask == 0, "Second room visible without portal mask: " + withoutMask + " pixels");
            portal.SetActive(true);
            blocker.transform.position = new Vector3(100, 1, 98);
            blocker.transform.localScale = new Vector3(0.25f, 4, 0.4f);
            blocker.SetActive(true);
            var foreground = Render(camera, pixels, target);
            Require(!IsPortal(foreground[128 * 256 + 128]), "Portal renders over a foreground object");
            Require(Count(foreground, IsPortal) > 500, "Foreground object erased entire portal");
            blocker.SetActive(false);
            camera.transform.position += Vector3.right * 0.7f;
            var shifted = Render(camera, pixels, target);
            Require(Count(shifted, IsPortal) > 500, "Portal disappeared after sideways camera movement");
            AssertOutsideMask(camera, portal.transform, shifted);
            portal.transform.rotation = Quaternion.Euler(0, 180, 0);
            Require(Count(Render(camera, pixels, target), IsPortal) == 0, "Portal is visible from its back side");
            foreach (string name in new[] { "Learning/Stencil/XRayOutline", "Learning/Stencil/PortalMask", "Learning/Stencil/PortalRoom" })
                Require(!ShaderUtil.ShaderHasError(Shader.Find(name)), "Shader compilation failed: " + name);

            return "PASS: X-ray hidden=" + hiddenOutline + ", visible=" + visibleOutline + ", partial=" + partialOutline
                + "; no silhouette fill, no visible-half outline, no stale outline; portal=" + portalPixels
                + ", no mask=" + withoutMask + "; outside mask, foreground depth, shifted camera, back face";
        }
        finally
        {
            RenderTexture.active = previous;
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
            outlineMaterial.SetColor("_OutlineColor", previousOutline);
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
            foreach (var material in new[] { actorMaterial, blockerMaterial, portalMaterial, maskMaterial })
                if (material) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    private static Color32[] Render(Camera camera, Texture2D pixels, RenderTexture target)
    {
        camera.Render();
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        pixels.Apply();

        return pixels.GetPixels32();
    }

    private static GameObject Primitive(Transform root, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        var result = GameObject.CreatePrimitive(type);
        result.name = name;
        result.transform.SetParent(root, false);
        result.transform.position = position;
        result.transform.localScale = scale;
        result.GetComponent<Renderer>().sharedMaterial = material;

        return result;
    }

    private static void AssertOutsideMask(Camera camera, Transform portal, Color32[] pixels)
    {
        var min = camera.WorldToViewportPoint(portal.TransformPoint(new Vector3(-0.5f, -0.5f, 0)));
        var max = camera.WorldToViewportPoint(portal.TransformPoint(new Vector3(0.5f, 0.5f, 0)));
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
                if (x < min.x * 256 - 2 || x > max.x * 256 + 2 || y < min.y * 256 - 2 || y > max.y * 256 + 2)
                    Require(!IsPortal(pixels[y * 256 + x]), "Second-room pixel outside portal at " + x + "," + y);
    }

    private static bool IsOutline(Color32 value) { return value.g > 170 && value.b > 170 && value.r < 180 && value.b > value.r * 1.3f; }
    private static bool IsOrange(Color32 value) { return value.r > 150 && value.r > value.g && value.b < 120; }
    private static bool IsPortal(Color32 value) { return value.r > 180 && value.b > 140 && value.g < 80; }
    private static int Count(Color32[] pixels, Func<Color32, bool> predicate)
    {
        int result = 0;
        foreach (var pixel in pixels) if (predicate(pixel)) result++;

        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
