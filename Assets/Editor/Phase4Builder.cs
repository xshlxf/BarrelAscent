using System;
using System.IO;
using System.Linq;
using BarrelAscent.EditorTools.Art;
using BarrelAscent.Enemies;
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
    /// Barriles:
    ///  1. Procesa el arte del barril de ArtSource/03_Enemies → Assets/Sprites/Objects/Barrel.
    ///  2. Crea ZoneConfig (Zone1..3) con los valores de balance iniciales.
    ///  3. Crea el prefab Barrel y añade PlayerCollision al prefab Player.
    ///  4. Reglas de colisión entre capas (barriles no empujan al jugador ni entre sí).
    ///  5. Reconstruye el nivel (Phase3Builder) y conecta un BarrelSpawner por zona y el LevelManager.
    /// Menú: Barrel Ascent/Setup/Build Phase 4 (Barrels).
    /// Batchmode: -executeMethod BarrelAscent.EditorTools.Phase4Builder.Build
    /// </summary>
    public static class Phase4Builder
    {
        const string ArtDir = "ArtSource/03_Enemies";
        public const string BarrelSpriteDir = "Assets/Sprites/Objects/Barrel";
        public const string BarrelRollSprite = BarrelSpriteDir + "/barrel_roll.png";
        public const string BarrelFallSprite = BarrelSpriteDir + "/barrel_fall.png";
        public const string BarrelDestroyedSprite = BarrelSpriteDir + "/barrel_destroyed.png";
        public const string BarrelPrefabPath = "Assets/Prefabs/Barrels/Barrel.prefab";
        public const string ZoneConfigDir = "Assets/Settings/Zones";

        public const float BarrelRadius = 0.4f;     // 0.8 u de diámetro ≈ 26 px
        const int BarrelSpritePx = 28;              // tamaño del sprite rodando (algo mayor que el collider)

        // Valores iniciales del documento maestro (sección 11).
        static readonly (string name, float speed, float interval)[] ZoneDefaults =
        {
            ("Zona 1 · Base industrial", 2f, 4f),
            ("Zona 2 · Maquinaria", 2.75f, 3f),
            ("Zona 3 · La cima", 3.5f, 2f),
        };

        [MenuItem("Barrel Ascent/Setup/Build Phase 4 (Barrels)")]
        public static void Build()
        {
            EnsureLayers();
            ConfigureLayerCollisions();
            ImportBarrelArt();
            CreateZoneConfigs();
            CreateBarrelPrefab();
            AddPlayerCollisionToPrefab();
            AssetDatabase.SaveAssets();

            Phase3Builder.Build();   // nivel + jugador actualizados
            WireScene();
            Debug.Log("[Phase4Builder] Phase 4 build complete.");
        }

        // ---------------- Física ----------------

        static void ConfigureLayerCollisions()
        {
            int barrel = Layer(BarrelLayer), player = Layer(PlayerLayer), ladder = Layer(LadderLayer);
            Physics2D.IgnoreLayerCollision(barrel, barrel, true);  // los barriles se atraviesan entre sí
            Physics2D.IgnoreLayerCollision(barrel, player, true);  // el daño se detecta por consulta, sin empujes
            Physics2D.IgnoreLayerCollision(barrel, ladder, true);
            AssetDatabase.SaveAssets();
        }

        // ---------------- Arte ----------------

        public static void ImportBarrelArt()
        {
            // Tira de 4 vistas; la 3.ª (índice 2) es la vista frontal redonda, la que mejor rueda.
            var roll = SpriteExtractor.Load(Path.Combine(ArtDir, "barrel_rolling_LeftToRight.png"));
            if (roll.frames.Count < 3)
                throw new InvalidOperationException($"barrel_rolling: se esperaban 4 fotogramas, hay {roll.frames.Count}.");
            var rollFrame = roll.frames[2];
            float scale = BarrelSpritePx / (float)Mathf.Max(rollFrame.width, rollFrame.height);
            SpriteExtractor.SavePng(SpriteExtractor.Downscale(roll, rollFrame, scale), BarrelRollSprite);

            // Caída y destrucción: misma escala de "píxel" que el barril rodando para mantener coherencia.
            var fall = SpriteExtractor.Load(Path.Combine(ArtDir, "barrel_falling.png"), minGap: 10000);
            SpriteExtractor.SavePng(SpriteExtractor.Downscale(fall, fall.frames[0], scale), BarrelFallSprite);

            var broken = SpriteExtractor.Load(Path.Combine(ArtDir, "barrel_destroyed.png"), minGap: 10000);
            SpriteExtractor.SavePng(SpriteExtractor.Downscale(broken, broken.frames[0], scale), BarrelDestroyedSprite);

            AssetDatabase.Refresh();
            foreach (var p in new[] { BarrelRollSprite, BarrelFallSprite, BarrelDestroyedSprite })
                ConfigureSprite(p, SpriteAlignment.Center);

            Debug.Log($"[Phase4Builder] Barrel art: scale {scale:0.000}, roll frame {rollFrame.width}x{rollFrame.height} px → " +
                      $"{AssetDatabase.LoadAssetAtPath<Sprite>(BarrelRollSprite).rect.size}");
        }

        // ---------------- Datos ----------------

        static void CreateZoneConfigs()
        {
            Directory.CreateDirectory(ZoneConfigDir);
            for (int i = 0; i < ZoneDefaults.Length; i++)
            {
                string path = ZonePath(i);
                if (AssetDatabase.LoadAssetAtPath<ZoneConfig>(path) != null) continue; // respetar ajustes manuales
                var z = ScriptableObject.CreateInstance<ZoneConfig>();
                z.displayName = ZoneDefaults[i].name;
                z.barrelSpeed = ZoneDefaults[i].speed;
                z.spawnInterval = ZoneDefaults[i].interval;
                z.maxBarrels = 6;
                AssetDatabase.CreateAsset(z, path);
            }
        }

        static string ZonePath(int i) => $"{ZoneConfigDir}/Zone{i + 1}.asset";

        // ---------------- Prefabs ----------------

        static void CreateBarrelPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BarrelPrefabPath));
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);

            var root = new GameObject("Barrel") { layer = Layer(BarrelLayer) };
            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.mass = 1f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.sharedMaterial = material;

            var circle = root.AddComponent<CircleCollider2D>();
            circle.radius = BarrelRadius;
            circle.sharedMaterial = material;

            var visual = new GameObject("Visual") { layer = root.layer };
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarrelRollSprite);
            sr.sortingOrder = 5;

            var bc = root.AddComponent<BarrelController>();
            var so = new SerializedObject(bc);
            so.FindProperty("groundMask").intValue = 1 << Layer(GroundLayer);
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            so.FindProperty("rollingSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(BarrelRollSprite);
            so.FindProperty("fallingSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(BarrelFallSprite);
            so.FindProperty("destroyedSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(BarrelDestroyedSprite);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BarrelPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void AddPlayerCollisionToPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var pc = root.GetComponent<PlayerCollision>();
                if (pc == null) pc = root.AddComponent<PlayerCollision>();
                var so = new SerializedObject(pc);
                so.FindProperty("barrelMask").intValue = 1 << Layer(BarrelLayer);
                so.FindProperty("spriteRenderer").objectReferenceValue = root.GetComponentInChildren<SpriteRenderer>();
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------------- Escena ----------------

        static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.GameLevelScene, OpenSceneMode.Single);
            var layout = Phase3Builder.LevelLayout.Load(Phase3Builder.LayoutPath);
            var barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath).GetComponent<BarrelController>();

            var spawnCells = layout.All('S').OrderBy(p => p.y).ToList();
            var spawners = new BarrelSpawner[spawnCells.Count];
            for (int i = 0; i < spawnCells.Count; i++)
            {
                var marker = GameObject.Find($"BarrelSpawn_Z{i + 1}");
                if (marker == null) throw new InvalidOperationException($"Falta BarrelSpawn_Z{i + 1}");
                var spawner = marker.GetComponent<BarrelSpawner>();
                if (spawner == null) spawner = marker.AddComponent<BarrelSpawner>();
                var so = new SerializedObject(spawner);
                so.FindProperty("barrelPrefab").objectReferenceValue = barrelPrefab;
                so.FindProperty("zone").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ZoneConfig>(ZonePath(i));
                so.FindProperty("direction").intValue = RollDirection(layout, spawnCells[i]);
                so.ApplyModifiedPropertiesWithoutUndo();
                spawners[i] = spawner;
            }

            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Managers") Object.DestroyImmediate(go);
            var managers = new GameObject("Managers");
            var levelGo = new GameObject("LevelManager");
            levelGo.transform.SetParent(managers.transform, false);
            var lm = levelGo.AddComponent<LevelManager>();

            var player = Object.FindFirstObjectByType<PlayerController>();
            float[] starts = layout.ZoneStartsY();
            var lso = new SerializedObject(lm);
            lso.FindProperty("player").objectReferenceValue = player.transform;
            var startsProp = lso.FindProperty("zoneStartY");
            startsProp.arraySize = starts.Length;
            for (int i = 0; i < starts.Length; i++) startsProp.GetArrayElementAtIndex(i).floatValue = starts[i];
            var spProp = lso.FindProperty("zoneSpawners");
            spProp.arraySize = spawners.Length;
            for (int i = 0; i < spawners.Length; i++) spProp.GetArrayElementAtIndex(i).objectReferenceValue = spawners[i];
            lso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Rueda hacia el extremo más lejano de la plataforma en la que aparece.</summary>
        static int RollDirection(Phase3Builder.LevelLayout layout, Vector2Int cell)
        {
            int row = cell.y - 1, left = cell.x, right = cell.x;
            while (layout[left - 1, row] == '#') left--;
            while (layout[right + 1, row] == '#') right++;
            return (right - cell.x) >= (cell.x - left) ? 1 : -1;
        }
    }
}
