using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BarrelAscent.EditorTools
{
    /// <summary>
    /// Renderiza la Main Camera de GameLevel a Docs/Screenshots/GameLevel.png (640x360 x2).
    /// Útil para revisar el aspecto en batchmode: -executeMethod BarrelAscent.EditorTools.ScenePreview.Capture
    /// </summary>
    public static class ScenePreview
    {
        [MenuItem("Barrel Ascent/Tools/Capture GameLevel Preview")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ProjectSetup.GameLevelScene, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[ScenePreview] No Main Camera"); return; }

            int w = ProjectSetup.RefResolutionX * 2, h = ProjectSetup.RefResolutionY * 2;
            var rt = new RenderTexture(w, h, 24) { filterMode = FilterMode.Point };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;

            Directory.CreateDirectory("Docs/Screenshots");
            File.WriteAllBytes("Docs/Screenshots/GameLevel.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log("[ScenePreview] Saved Docs/Screenshots/GameLevel.png");
        }
    }
}
