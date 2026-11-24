using System;
using System.IO;
using System.Linq;
using BarrelAscent.EditorTools.Art;
using BarrelAscent.Environment;
using BarrelAscent.Managers;
using BarrelAscent.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static BarrelAscent.EditorTools.Phase2Builder;
using Object = UnityEngine.Object;

namespace BarrelAscent.EditorTools
{
    /// <summary>
    /// Fase 5: coleccionables, meta, puntos de reaparición y GameManager.
    ///  1. Procesa los coleccionables de ArtSource/04_Collectibles y dibuja la bandera de meta (placeholder).
    ///  2. Crea los prefabs Collectible y Goal.
    ///  3. Reconstruye las fases 3–4 y coloca coleccionables, meta y reapariciones en GameLevel.
    ///  4. Añade el GameManager y conecta todas las referencias.
    /// Menú: Barrel Ascent/Setup/Build Phase 5 (Game Rules).
    /// Batchmode: -executeMethod BarrelAscent.EditorTools.Phase5Builder.Build
    /// </summary>
    public static class Phase5Builder
    {
        const string ArtDir = "ArtSource/04_Collectibles";
        static readonly string[] CollectibleArt = { "collectible_gear", "collectible_component", "collectible_capsule" };
        public const string CollectibleSpriteDir = "Assets/Sprites/Objects/Collectibles";
        public const string GoalSpritePath = PlaceholderDir + "/PH_GoalFlag.png";
        public const string CollectiblePrefabPath = "Assets/Prefabs/Collectibles/Collectible.prefab";
        public const string GoalPrefabPath = "Assets/Prefabs/Environment/Goal.prefab";
        const int CollectibleSpritePx = 20;

        [MenuItem("Barrel Ascent/Setup/Build Phase 5 (Game Rules)")]
        public static void Build()
        {
            ImportCollectibleArt();
            CreateGoalSprite();
            CreateCollectiblePrefab();
            CreateGoalPrefab();
            AssetDatabase.SaveAssets();

            Phase4Builder.Build(); // nivel, jugador, barriles y LevelManager
            WireScene();
            Debug.Log("[Phase5Builder] Phase 5 build complete.");
        }

        static string CollectibleSpritePath(string name) => $"{CollectibleSpriteDir}/{name}.png";

        // ---------------- Arte ----------------

        static void ImportCollectibleArt()
        {
            foreach (var name in CollectibleArt)
            {
                var sheet = SpriteExtractor.Load(Path.Combine(ArtDir, name + ".png"), minGap: 10000);
                var f = sheet.frames[0];
                float scale = CollectibleSpritePx / (float)Mathf.Max(f.width, f.height);
                // Contorno amarillo de 1 px (Fase 7): el "componente" gris apenas se veía sobre el fondo de la zona 3.
                var sprite = SpriteExtractor.AddOutline(SpriteExtractor.Downscale(sheet, f, scale), Yellow);
                SpriteExtractor.SavePng(sprite, CollectibleSpritePath(name));
            }
            AssetDatabase.Refresh();
            foreach (var name in CollectibleArt) ConfigureSprite(CollectibleSpritePath(name), SpriteAlignment.Center);
        }

        // Bandera de meta 32x64: mástil gris y bandera a cuadros amarillo/rojo de advertencia.
        static void CreateGoalSprite()
        {
            var t = new Texture2D(32, 64, TextureFormat.RGBA32, false);
            var clear = new Color32[32 * 64];
            t.SetPixels32(clear);
            Rect(t, 6, 0, 8, 63, MetalGray);    // mástil
            Rect(t, 6, 0, 6, 63, LightGray);
            Rect(t, 4, 0, 11, 2, Steel);        // base
            Rect(t, 5, 61, 9, 63, Yellow);      // remate
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                    Rect(t, 9 + x * 4, 38 + y * 4, 12 + x * 4, 41 + y * 4, (x + y) % 2 == 0 ? Yellow : Hex(0xB83B36));
            Rect(t, 9, 37, 28, 37, Shadow);     // sombra inferior de la bandera
            WritePng(GoalSpritePath, Finish(t));
            AssetDatabase.Refresh();
            ConfigureSprite(GoalSpritePath, SpriteAlignment.BottomCenter);
        }

        // ---------------- Prefabs ----------------

        static void CreateCollectiblePrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CollectiblePrefabPath));
            var root = new GameObject("Collectible");
            var circle = root.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.35f;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CollectibleSpritePath(CollectibleArt[0]));
            sr.sortingOrder = 4;
            var c = root.AddComponent<Collectible>();
            var so = new SerializedObject(c);
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CollectiblePrefabPath);
            Object.DestroyImmediate(root);
        }

        static void CreateGoalPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GoalPrefabPath));
            var root = new GameObject("Goal");
            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1f, 2f);
            box.offset = new Vector2(0f, 1f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GoalSpritePath);
            sr.sortingOrder = 3;
            root.AddComponent<Goal>();
            PrefabUtility.SaveAsPrefabAsset(root, GoalPrefabPath);
            Object.DestroyImmediate(root);
        }

        // ---------------- Escena ----------------

        static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.GameLevelScene, OpenSceneMode.Single);
            var layout = Phase3Builder.LevelLayout.Load(Phase3Builder.LayoutPath);
            var markers = GameObject.Find("Level/Markers")?.transform
                          ?? throw new InvalidOperationException("Falta Level/Markers (ejecuta la Fase 3).");

            // Coleccionables en cada marcador, alternando los tres sprites.
            var collectiblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectiblePrefabPath);
            var sprites = CollectibleArt.Select(n => AssetDatabase.LoadAssetAtPath<Sprite>(CollectibleSpritePath(n))).ToArray();
            var collectibleMarkers = markers.Find("Collectibles");
            int i = 0;
            foreach (Transform marker in collectibleMarkers)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(collectiblePrefab, marker);
                go.transform.localPosition = Vector3.zero;
                var sr = go.GetComponentInChildren<SpriteRenderer>();
                sr.sprite = sprites[i++ % sprites.Length];
                PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
            }

            // Meta.
            var goalMarker = markers.Find("Goal");
            var goal = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GoalPrefabPath), goalMarker);
            goal.transform.localPosition = Vector3.zero;

            // Reapariciones: inicio para la zona 1; para las demás, lo alto de la primera escalera de la zona.
            float[] starts = layout.ZoneStartsY();
            var runs = layout.LadderRuns();
            var respawns = new Transform[starts.Length];
            for (int z = 0; z < starts.Length; z++)
            {
                Vector3 pos;
                if (z == 0) pos = markers.Find("PlayerSpawn").position;
                else
                {
                    var run = runs.Where(r => r.topY >= starts[z]).OrderBy(r => r.topY).First();
                    pos = new Vector3(Phase3Builder.LevelOriginX + run.column + 0.5f, run.topY, 0f);
                }
                var rp = new GameObject($"Respawn_Z{z + 1}");
                rp.transform.SetParent(markers, false);
                rp.transform.position = pos;
                respawns[z] = rp.transform;
            }

            var lm = Object.FindFirstObjectByType<LevelManager>();
            var lso = new SerializedObject(lm);
            var rpProp = lso.FindProperty("respawnPoints");
            rpProp.arraySize = respawns.Length;
            for (int z = 0; z < respawns.Length; z++) rpProp.GetArrayElementAtIndex(z).objectReferenceValue = respawns[z];
            lso.ApplyModifiedPropertiesWithoutUndo();

            // GameManager.
            var managers = lm.transform.parent;
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(managers, false);
            gmGo.transform.SetSiblingIndex(0);
            var gm = gmGo.AddComponent<GameManager>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var gso = new SerializedObject(gm);
            gso.FindProperty("player").objectReferenceValue = player;
            gso.FindProperty("playerCollision").objectReferenceValue = player.GetComponent<PlayerCollision>();
            gso.FindProperty("levelManager").objectReferenceValue = lm;
            gso.FindProperty("cameraFollow").objectReferenceValue = Object.FindFirstObjectByType<CameraFollow>();
            gso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
