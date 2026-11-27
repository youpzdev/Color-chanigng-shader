using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public static class GrassVerification
{

    public static async Task<string> Run()
    {
        if (!Application.isPlaying) throw new Exception("Grass verification requires Play Mode");
        var shader = Shader.Find("Custom/URP/StylizedGrass");
        Require(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Grass shader failed compilation");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Grass/GardenGrass.mat");
        int normalPass = material.FindPass("DepthNormals");
        Require(normalPass >= 0, "Missing DepthNormals pass");
        string tag = shader.FindPassTagValue(0, normalPass, new ShaderTagId("LightMode")).name;
        Require(tag == "DepthNormalsOnly", "Deferred normal prepass: expected DepthNormalsOnly, got " + tag);
        Require(material.FindPass("ShadowCaster") >= 0 && material.FindPass("DepthOnly") >= 0, "Missing shadow or depth pass");
        var patches = GameObject.Find("GardenGrass").GetComponentsInChildren<MeshFilter>();
        Require(patches.Length > 0, "No grass patches in scene");
        int vertices = 0;
        int blades = 0;
        foreach (var patch in patches)
        {
            var mesh = patch.sharedMesh;
            var points = mesh.vertices;
            var uv = mesh.uv;
            var colors = mesh.colors;
            Require(points.Length == uv.Length && points.Length == colors.Length, "Mesh streams differ: " + mesh.name);
            Require(points.Length % 14 == 0, "Expected 14 vertices per blade: " + mesh.name);
            for (int start = 0; start < points.Length; start += 14)
            {
                Require(uv[start].y == 0 && uv[start + 12].y == 1, "Root/tip UV differs: " + mesh.name);
                for (int i = 0; i < 14; i++)
                    Require(colors[start + i] == colors[start], "Variation changes within a blade: " + mesh.name);
                var position = patch.transform.TransformPoint(points[start]);
                Require(Mathf.Abs(position.x) >= 6 || Mathf.Abs(position.z) >= 6, "Grass intrudes onto courtyard: " + position);
            }
            vertices += points.Length;
            blades += points.Length / 14;
        }
        CheckBounds(patches[0].sharedMesh, material);
        const string probePath = "Assets/Shaders/Grass/GrassVerificationProbe.shader";
        Material probe = null;
        RenderTexture target = null;
        Texture2D pixels = null;
        bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
        try
        {
            string source = File.ReadAllText("Assets/Shaders/Grass/StylizedGrass.shader");
            int begin = source.IndexOf("HLSLINCLUDE", StringComparison.Ordinal) + "HLSLINCLUDE".Length;
            int end = source.IndexOf("ENDHLSL", begin, StringComparison.Ordinal);
            Require(begin >= "HLSLINCLUDE".Length && end > begin, "Missing shared shader functions");
            string common = source.Substring(begin, end - begin);
            string probeSource = "Shader \"Hidden/GrassVerification\" { SubShader { Pass { Cull Off ZWrite Off ZTest Always HLSLPROGRAM\n"
                + "#pragma target 3.5\n#pragma vertex ProbeVert\n#pragma fragment ProbeFrag\n" + common
                + "\nfloat4 _ProbePosition; float _ProbeHeight;\n"
                + "float4 ProbeVert(uint id:SV_VertexID):SV_POSITION { return GetFullScreenTriangleVertexPosition(id); }\n"
                + "float4 ProbeFrag():SV_Target { float3 p=_ProbePosition.xyz; float3 n=float3(0,0,-1); GrassDeform(p,n,_ProbeHeight); return float4(p,1); }\n"
                + "ENDHLSL } } }";
            File.WriteAllText(probePath, probeSource.Replace("\r\n", "\n"));
            AssetDatabase.ImportAsset(probePath, ImportAssetOptions.ForceSynchronousImport);
            ShaderUtil.allowAsyncCompilation = false;
            probe = new Material(AssetDatabase.LoadAssetAtPath<Shader>(probePath));
            target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create();
            pixels = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            probe.SetVector("_WindDirection", new Vector4(1, 0.4f, 0, 0));
            probe.SetFloat("_WindScale", 0.9f);
            probe.SetFloat("_WindSpeed", 1.2f);
            probe.SetFloat("_BladeHeight", 0.35f);
            probe.SetFloat("_WindStrength", 0.08f);
            var point = new Vector3(0.3f, 0.35f, 0.7f);
            var root = Sample(probe, target, pixels, point, 0);
            Require(Vector3.Distance(root, point) < 0.00001f, "Wind moves root: " + root + ", expected " + point);
            var tipA = Sample(probe, target, pixels, point, 1);
            int frame = Time.frameCount;
            await Task.Delay(350);
            Require(Time.frameCount > frame, "Play Mode frames did not advance");
            var tipB = Sample(probe, target, pixels, point, 1);
            Require(Vector3.Distance(tipA, tipB) > 0.0001f, "Wind did not move tip over time");
            var offsetA = tipB - point;
            var distant = new Vector3(11, 0.35f, 9);
            var offsetB = Sample(probe, target, pixels, distant, 1) - distant;
            Require(Vector3.Distance(offsetA, offsetB) > 0.001f, "Distant blades move synchronously");
            probe.SetFloat("_WindStrength", 0);
            var stillA = Sample(probe, target, pixels, point, 1);
            await Task.Delay(100);
            var stillB = Sample(probe, target, pixels, point, 1);
            Require(Vector3.Distance(stillA, point) < 0.00001f && Vector3.Distance(stillB, point) < 0.00001f, "Zero wind deforms the blade");
            Require(!ShaderUtil.ShaderHasError(probe.shader), "GPU probe failed compilation");

            return "PASS: " + patches.Length + " patches, " + blades + " blades, " + vertices
                + " vertices; UV, color, paths; rotated/scaled bounds; GPU roots, moving tips, spatial phase, zero wind; deferred normals";
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
            if (probe) UnityEngine.Object.DestroyImmediate(probe);
            if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
            AssetDatabase.DeleteAsset(probePath);
        }
    }

    private static Vector3 Sample(Material material, RenderTexture target, Texture2D pixels, Vector3 point, float height)
    {
        var previous = RenderTexture.active;
        try
        {
            material.SetVector("_ProbePosition", point);
            material.SetFloat("_ProbeHeight", height);
            Graphics.SetRenderTarget(target);
            GL.Clear(false, true, Color.clear);
            Require(material.SetPass(0), "Could not select GPU probe pass");
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 3);
            pixels.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
            pixels.Apply();
            var value = pixels.GetPixel(0, 0);

            return new Vector3(value.r, value.g, value.b);
        }
        finally { RenderTexture.active = previous; }
    }

    private static void CheckBounds(Mesh mesh, Material material)
    {
        var sample = new GameObject("GrassBoundsVerification");
        try
        {
            sample.hideFlags = HideFlags.HideAndDontSave;
            sample.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = sample.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var bounds = sample.AddComponent<GrassBounds>();
            var rotations = new[] { Quaternion.identity, Quaternion.Euler(35, 47, 18), Quaternion.Euler(0, 90, 0) };
            foreach (var rotation in rotations)
            {
                sample.transform.rotation = rotation;
                sample.transform.localScale = new Vector3(0.3f, 1.8f, 2.5f);
                bounds.Refresh();
                var points = mesh.vertices;
                foreach (var point in points.Where((p, index) => index % 13 == 0))
                    foreach (var shift in new[] { new Vector3(0.08f, -0.009f, 0.03f), new Vector3(-0.08f, -0.009f, -0.03f) })
                    {
                        var localPoint = point + sample.transform.InverseTransformVector(shift);
                        Require(renderer.localBounds.Contains(localPoint), "Wind escapes bounds for rotation " + rotation.eulerAngles + ": " + localPoint);
                    }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(sample); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
