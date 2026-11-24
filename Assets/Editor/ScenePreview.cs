using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;

namespace BarrelAscent.EditorTools
{
    /// <summary>
    /// Capturas de GameLevel para revisión (también en batchmode):
    ///  - Capture: vista de la cámara del juego → Docs/Screenshots/GameLevel.png
    ///  - CaptureFullLevel: nivel completo a 1 px de pantalla = 1 px de arte → Docs/Screenshots/GameLevel_Full.png
    /// -executeMethod BarrelAscent.EditorTools.ScenePreview.Capture | CaptureFullLevel
    /// No modifica la escena guardada.
    /// </summary>
    public static class ScenePreview
    {
        const string Dir = "Docs/Screenshots";

        [MenuItem("Barrel Ascent/Tools/Capture GameLevel Preview")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ProjectSetup.GameLevelScene, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[ScenePreview] No Main Camera"); return; }
            Render(cam, ProjectSetup.RefResolutionX * 2, ProjectSetup.RefResolutionY * 2, Path.Combine(Dir, "GameLevel.png"));
        }

        [MenuItem("Barrel Ascent/Tools/Capture Full Level")]
        public static void CaptureFullLevel()
        {
            EditorSceneManager.OpenScene(ProjectSetup.GameLevelScene, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[ScenePreview] No Main Camera"); return; }

            var layout = Phase3Builder.LevelLayout.Load(Phase3Builder.LayoutPath);
            var ppc = cam.GetComponent<PixelPerfectCamera>();
            if (ppc != null) ppc.enabled = false;

            float width = layout.Width, height = layout.Height;
            cam.orthographicSize = height * 0.5f;
            cam.transform.position = new Vector3(Phase3Builder.LevelOriginX + width * 0.5f, height * 0.5f, -10f);
            int ppu = ProjectSetup.PixelsPerUnit;
            Render(cam, (int)(width * ppu), (int)(height * ppu), Path.Combine(Dir, "GameLevel_Full.png"));
            // La escena no se guarda: los cambios de cámara se descartan.
        }

        static void Render(Camera cam, int w, int h, string path)
        {
            var rt = new RenderTexture(w, h, 24) { filterMode = FilterMode.Point };
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            cam.ResetAspect();
            RenderTexture.active = null;

            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log($"[ScenePreview] Saved {path}");
        }
    }
}
