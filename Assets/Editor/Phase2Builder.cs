using System.IO;
using BarrelAscent.Environment;
using BarrelAscent.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BarrelAscent.EditorTools
{
    /// <summary>
    /// Fase 2: capas de física, sprites placeholder, Animator del jugador, prefab Player
    /// y una zona de pruebas en GameLevel (suelo, plataformas, escaleras, obstáculo).
    /// Menú: Barrel Ascent/Setup/Build Phase 2 (Player + Test Area).
    /// Batchmode: -executeMethod BarrelAscent.EditorTools.Phase2Builder.Build
    /// Reejecutable: reconstruye la zona de pruebas y el jugador de la escena.
    /// Los sprites PH_* son placeholders claramente identificados y se sustituirán en la Fase 7.
    /// </summary>
    public static class Phase2Builder
    {
        public const string PlaceholderDir = "Assets/Sprites/Placeholders";
        public const string PlayerSpritePath = PlaceholderDir + "/PH_Player.png";
        public const string PlatformSpritePath = PlaceholderDir + "/PH_PlatformTile.png";
        public const string LadderSpritePath = PlaceholderDir + "/PH_Ladder.png";
        public const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        public const string PlayerControllerPath = "Assets/Animations/Player/Player.controller";
        public const string NoFrictionPath = "Assets/Settings/Physics/NoFriction.physicsMaterial2D";

        public const string GroundLayer = "Ground";
        public const string LadderLayer = "Ladder";
        public const string PlayerLayer = "Player";
        public const string BarrelLayer = "Barrel";

        const string TestAreaName = "Phase2_TestArea";

        // Paleta Barrel Ascent
        internal static readonly Color32 DarkBlue = Hex(0x182B38);
        internal static readonly Color32 Steel = Hex(0x354B55);
        internal static readonly Color32 MetalGray = Hex(0x77858A);
        internal static readonly Color32 Rust = Hex(0x875333);
        internal static readonly Color32 Orange = Hex(0xD97732);
        internal static readonly Color32 Yellow = Hex(0xE6C66B);
        internal static readonly Color32 LightGray = Hex(0xD7D5C7);
        internal static readonly Color32 Shadow = Hex(0x101820);
        internal static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        [MenuItem("Barrel Ascent/Setup/Build Phase 2 (Player + Test Area)")]
        public static void Build()
        {
            EnsureLayers();
            CreatePlaceholderSprites();
            var material = CreateNoFrictionMaterial();
            var controller = CreatePlayerAnimator();
            var prefab = CreatePlayerPrefab(material, controller);
            BuildTestArea(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[Phase2Builder] Phase 2 build complete.");
        }

        // ---------------- Capas ----------------

        public static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            SetLayer(layers, 6, GroundLayer);
            SetLayer(layers, 7, LadderLayer);
            SetLayer(layers, 8, PlayerLayer);
            SetLayer(layers, 9, BarrelLayer);
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayer(SerializedProperty layers, int index, string name)
        {
            var p = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = name;
            else if (p.stringValue != name)
                Debug.LogWarning($"[Phase2Builder] Layer {index} ya está ocupada por '{p.stringValue}', se esperaba '{name}'.");
        }

        internal static int Layer(string name) => LayerMask.NameToLayer(name);

        // ---------------- Sprites placeholder ----------------

        static void CreatePlaceholderSprites()
        {
            Directory.CreateDirectory(PlaceholderDir);
            WritePng(PlayerSpritePath, DrawPlayer());
            WritePng(PlatformSpritePath, DrawPlatformTile());
            WritePng(LadderSpritePath, DrawLadder());
            AssetDatabase.Refresh();

            ConfigureSprite(PlayerSpritePath, SpriteAlignment.BottomCenter);
            ConfigureSprite(PlatformSpritePath, SpriteAlignment.Center);
            ConfigureSprite(LadderSpritePath, SpriteAlignment.Center);
        }

        internal static void ConfigureSprite(string path, SpriteAlignment alignment)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = ProjectSetup.PixelsPerUnit;
            settings.filterMode = FilterMode.Point;
            settings.mipmapEnabled = false;
            settings.spriteMeshType = SpriteMeshType.FullRect; // necesario para Draw Mode = Tiled
            settings.spriteAlignment = (int)alignment;
            settings.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        internal static void WritePng(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        internal static Texture2D NewTex(Color32 fill)
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var px = new Color32[32 * 32];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
            t.SetPixels32(px);
            return t;
        }

        internal static void Rect(Texture2D t, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    t.SetPixel(x, y, c);
        }

        // Trabajador de mantenimiento: casco amarillo, mono naranja, botas oscuras. Mira a la derecha.
        static Texture2D DrawPlayer()
        {
            var t = NewTex(Clear);
            Rect(t, 10, 0, 14, 3, Shadow);      // bota izq
            Rect(t, 17, 0, 21, 3, Shadow);      // bota der
            Rect(t, 10, 4, 14, 10, Steel);      // pernera izq
            Rect(t, 17, 4, 21, 10, Steel);      // pernera der
            Rect(t, 9, 11, 22, 20, Orange);     // mono
            Rect(t, 9, 11, 22, 12, Rust);       // cinturón / sombra
            Rect(t, 12, 15, 19, 19, Yellow);    // franja reflectante
            Rect(t, 7, 13, 8, 19, Orange);      // brazo
            Rect(t, 23, 13, 24, 19, Orange);
            Rect(t, 11, 21, 21, 26, LightGray); // cara
            Rect(t, 19, 23, 19, 24, Shadow);    // ojo
            Rect(t, 10, 27, 22, 30, Yellow);    // casco
            Rect(t, 10, 27, 24, 27, Rust);      // visera
            Rect(t, 12, 30, 20, 31, Yellow);
            return Finish(t);
        }

        // Plataforma de acero con remaches y borde superior iluminado (luz arriba-izquierda).
        static Texture2D DrawPlatformTile()
        {
            var t = NewTex(Steel);
            Rect(t, 0, 28, 31, 31, MetalGray);  // borde superior
            Rect(t, 0, 31, 31, 31, LightGray);
            Rect(t, 0, 0, 31, 2, Shadow);       // borde inferior
            Rect(t, 0, 3, 31, 4, DarkBlue);
            for (int x = 4; x < 32; x += 12)    // remaches
            {
                Rect(t, x, 22, x + 1, 23, MetalGray);
                Rect(t, x, 9, x + 1, 10, MetalGray);
            }
            Rect(t, 20, 12, 23, 13, Rust);      // óxido
            return Finish(t);
        }

        // Escalera metálica: largueros grises, peldaños amarillos de advertencia.
        static Texture2D DrawLadder()
        {
            var t = NewTex(Clear);
            Rect(t, 4, 0, 6, 31, MetalGray);
            Rect(t, 25, 0, 27, 31, MetalGray);
            Rect(t, 4, 0, 4, 31, LightGray);
            Rect(t, 27, 0, 27, 31, Steel);
            for (int y = 3; y < 32; y += 8)
            {
                Rect(t, 7, y, 24, y + 1, Yellow);
                Rect(t, 7, y - 1, 24, y - 1, Rust);
            }
            return Finish(t);
        }

        internal static Texture2D Finish(Texture2D t)
        {
            t.Apply();
            return t;
        }

        internal static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);

        // ---------------- Física ----------------

        static PhysicsMaterial2D CreateNoFrictionMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
            if (mat != null) return mat;
            Directory.CreateDirectory(Path.GetDirectoryName(NoFrictionPath));
            mat = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(mat, NoFrictionPath);
            return mat;
        }

        // ---------------- Animator ----------------

        static AnimatorController CreatePlayerAnimator()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (existing != null) return existing; // no pisar un Animator ya editado a mano

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerControllerPath));
            var ac = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Climbing", AnimatorControllerParameterType.Bool);
            ac.AddParameter("ClimbSpeed", AnimatorControllerParameterType.Float);
            ac.AddParameter("VelocityY", AnimatorControllerParameterType.Float);

            var sm = ac.layers[0].stateMachine;
            var idle = sm.AddState("Idle", new Vector3(300, 0));
            var run = sm.AddState("Run", new Vector3(300, 100));
            var jump = sm.AddState("Jump", new Vector3(550, 0));
            var climb = sm.AddState("Climb", new Vector3(550, 100));
            sm.defaultState = idle;

            Transition(idle, run, ("Speed", AnimatorConditionMode.Greater, 0.1f));
            Transition(run, idle, ("Speed", AnimatorConditionMode.Less, 0.1f));

            var toJump = sm.AddAnyStateTransition(jump);
            Configure(toJump, ("Grounded", AnimatorConditionMode.IfNot, 0), ("Climbing", AnimatorConditionMode.IfNot, 0));
            toJump.canTransitionToSelf = false;
            Transition(jump, idle, ("Grounded", AnimatorConditionMode.If, 0));

            var toClimb = sm.AddAnyStateTransition(climb);
            Configure(toClimb, ("Climbing", AnimatorConditionMode.If, 0));
            toClimb.canTransitionToSelf = false;
            Transition(climb, idle, ("Climbing", AnimatorConditionMode.IfNot, 0));

            return ac;
        }

        static void Transition(AnimatorState from, AnimatorState to, params (string, AnimatorConditionMode, float)[] conds)
        {
            Configure(from.AddTransition(to), conds);
        }

        static void Configure(AnimatorStateTransition t, params (string p, AnimatorConditionMode m, float v)[] conds)
        {
            t.hasExitTime = false;
            t.duration = 0f;
            foreach (var c in conds) t.AddCondition(c.m, c.v, c.p);
        }

        // ---------------- Prefab del jugador ----------------

        static GameObject CreatePlayerPrefab(PhysicsMaterial2D material, AnimatorController controller)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));

            var root = new GameObject("Player") { layer = Layer(PlayerLayer) };
            root.tag = "Player";

            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.mass = 1f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.sharedMaterial = material;

            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.6f, 0.94f);
            capsule.offset = new Vector2(0f, 0.47f); // pivote en los pies
            capsule.sharedMaterial = material;

            var visual = new GameObject("Visual") { layer = root.layer };
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
            sr.sortingOrder = 10;
            var anim = visual.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            var pc = root.AddComponent<PlayerController>();
            var so = new SerializedObject(pc);
            so.FindProperty("groundMask").intValue = 1 << Layer(GroundLayer);
            so.FindProperty("ladderMask").intValue = 1 << Layer(LadderLayer);
            so.FindProperty("animator").objectReferenceValue = anim;
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------- Zona de pruebas ----------------

        static void BuildTestArea(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.GameLevelScene, OpenSceneMode.Single);

            // Si ya existe el nivel real (Fase 3), no volver a crear la zona de pruebas.
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Level")
                {
                    Debug.Log("[Phase2Builder] GameLevel ya tiene el nivel de la Fase 3; se omite la zona de pruebas.");
                    return;
                }

            foreach (var go in scene.GetRootGameObjects())
                if (go.name == TestAreaName || go.name == "Player")
                    Object.DestroyImmediate(go);

            var area = new GameObject(TestAreaName);
            var platformSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlatformSpritePath);
            var ladderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LadderSpritePath);

            // Coordenadas en unidades (1 unidad = 1 tile de 32 px). Vista: x [-10,10], y [-5.6,5.6].
            CreatePlatform(area.transform, platformSprite, "Floor", left: -10, right: 10, top: -4.5f);
            CreatePlatform(area.transform, platformSprite, "Platform_A", left: -8, right: 4, top: -1f);
            CreatePlatform(area.transform, platformSprite, "Platform_B", left: -2, right: 8, top: 3f);
            CreatePlatform(area.transform, platformSprite, "Obstacle_Block", left: -4, right: -3, top: -3.5f);
            CreateWall(area.transform, "Wall_Left", -10.5f);
            CreateWall(area.transform, "Wall_Right", 10.5f);

            CreateLadder(area.transform, ladderSprite, "Ladder_FloorToA", x: 2.5f, bottom: -4.5f, top: -1f);
            CreateLadder(area.transform, ladderSprite, "Ladder_AToB", x: -0.5f, bottom: -1f, top: 3f);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = new Vector3(-7f, -4.5f, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void CreatePlatform(Transform parent, Sprite sprite, string name, float left, float right, float top)
        {
            float width = right - left;
            var go = new GameObject(name) { layer = Layer(GroundLayer) };
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((left + right) * 0.5f, top - 0.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(width, 1f);
            sr.sortingOrder = 0;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, 1f);
        }

        static void CreateWall(Transform parent, string name, float x)
        {
            var go = new GameObject(name) { layer = Layer(GroundLayer) };
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, 0f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(1f, 30f);
        }

        static void CreateLadder(Transform parent, Sprite sprite, string name, float x, float bottom, float top)
        {
            const float topExtension = 0.5f;
            float height = top - bottom;

            var go = new GameObject(name) { layer = Layer(LadderLayer) };
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, bottom + height * 0.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(1f, height);
            sr.sortingOrder = -1; // detrás de plataformas y jugador

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.8f, height + topExtension);
            box.offset = new Vector2(0f, topExtension * 0.5f);

            go.AddComponent<Ladder>();
        }
    }
}
