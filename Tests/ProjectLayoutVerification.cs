using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectLayoutVerification
{
    public static string Run()
    {
        if (Application.isPlaying) throw new Exception("Layout verification requires Edit Mode");
        if (!EditorSceneManager.SaveOpenScenes()) throw new Exception("Could not save open scenes");
        string previous = SceneManager.GetActiveScene().path;
        var report = new List<string>();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path);
                int objects = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        objects++;
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                            throw new Exception(path + ": missing script on " + transform.name);
                        if (transform.GetComponent<TMPro.TMP_Text>() || transform.GetComponent<UnityEngine.UI.Text>())
                            throw new Exception(path + ": screen text remains on " + transform.name);
                        var renderer = transform.GetComponent<Renderer>();
                        if (renderer)
                            foreach (var material in renderer.sharedMaterials)
                                if (!material || !material.shader || ShaderUtil.ShaderHasError(material.shader))
                                    throw new Exception(path + ": invalid material on " + transform.name);
                        var mesh = transform.GetComponent<MeshFilter>();
                        if (mesh && !mesh.sharedMesh) throw new Exception(path + ": missing mesh on " + transform.name);
                    }
                report.Add(scene.name + ": " + objects + " objects, no missing scripts/materials/meshes or screen text");
            }
            foreach (var scene in EditorBuildSettings.scenes)
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path)) throw new Exception("Build scene missing: " + scene.path);
        }
        finally
        {
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        }

        return "PASS: " + string.Join("; ", report);
    }
}
