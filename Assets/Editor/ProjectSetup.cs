using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

namespace BarrelAscent.EditorTools
{
    /// <summary>
    /// Fase 1: crea las escenas base, configura Build Settings y las acciones de entrada.
    /// Se puede ejecutar desde el menú "Barrel Ascent/Setup/..." o en batchmode con
    /// -executeMethod BarrelAscent.EditorTools.ProjectSetup.RunPhase1
    /// Es idempotente: no sobrescribe escenas que ya existen.
    /// </summary>
    public static class ProjectSetup
    {
        public const string MainMenuScene = "Assets/Scenes/MainMenu/MainMenu.unity";
        public const string GameLevelScene = "Assets/Scenes/GameLevel/GameLevel.unity";
        public const string InputActionsPath = "Assets/Settings/BarrelAscentControls.inputactions";

        public const int PixelsPerUnit = 32;
        public const int RefResolutionX = 640;
        public const int RefResolutionY = 360;

        static readonly Color BackgroundDarkBlue = new Color32(0x18, 0x2B, 0x38, 0xFF);

        [MenuItem("Barrel Ascent/Setup/Run Phase 1 Setup")]
        public static void RunPhase1()
        {
            ConfigureInputActions();
            CreateSceneIfMissing(MainMenuScene);
            CreateSceneIfMissing(GameLevelScene);
            ConfigureBuildSettings();
            DeleteSampleScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Phase 1 setup complete.");
        }

        static void ConfigureInputActions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                Debug.LogError($"[ProjectSetup] Input actions not found at {InputActionsPath}");
                return;
            }
            // Asset centralizado como "Project-wide Actions": accesible con InputSystem.actions.
            InputSystem.actions = asset;
        }

        static void CreateSceneIfMissing(string path)
        {
            if (File.Exists(path))
            {
                Debug.Log($"[ProjectSetup] Scene already exists, skipped: {path}");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[ProjectSetup] Created scene {path}");
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = RefResolutionY / (2f * PixelsPerUnit); // 5.625 con 640x360
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundDarkBlue;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            go.AddComponent<AudioListener>();

            var ppc = go.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = PixelsPerUnit;
            ppc.refResolutionX = RefResolutionX;
            ppc.refResolutionY = RefResolutionY;
            ppc.upscaleRT = false;
            ppc.pixelSnapping = true;
            ppc.cropFrameX = false;
            ppc.cropFrameY = false;
            ppc.stretchFill = false;
        }

        static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScene, true),
                new EditorBuildSettingsScene(GameLevelScene, true),
            }.ToArray();
        }

        static void DeleteSampleScene()
        {
            const string sample = "Assets/Scenes/SampleScene.unity";
            if (File.Exists(sample))
                AssetDatabase.DeleteAsset(sample);
        }
    }
}
