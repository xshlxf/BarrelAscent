using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BarrelAscent.EditorTools.Art;
using BarrelAscent.Effects;
using BarrelAscent.Managers;
using BarrelAscent.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using static BarrelAscent.EditorTools.Phase2Builder;
using Object = UnityEngine.Object;

namespace BarrelAscent.EditorTools
{
    /// <summary>
    /// Fase 7: arte definitivo y audio.
    ///  1. Personaje (ArtSource/01_Character): fotogramas a 32 PPU con los pies en el pivote, clips y Animator.
    ///  2. Tileset (factory_tileset.png): viga con franjas (extremos izq./der. + tramo central) y escalera.
    ///  3. Fondos por zona (factory_background_zone_0X.jpg) reducidos y oscurecidos en los bordes.
    ///  4. Efectos (ArtSource/05_Effects): polvo, destrucción de barril, recogida, daño.
    ///  5. Meta con la baliza amarilla de factory_decorations.png.
    ///  6. Reconstruye las fases 3–5, aplica todo a GameLevel, añade EffectsManager y AudioManager,
    ///     y vuelve a construir la UI (Fase 6).
    /// Menú: Barrel Ascent/Setup/Build Phase 7 (Art + Audio).
    /// Batchmode: -executeMethod BarrelAscent.EditorTools.Phase7Builder.Build
    /// Todas las coordenadas de recorte están en píxeles de Unity (origen abajo a la izquierda).
    /// </summary>
    public static class Phase7Builder
    {
        public const string PlayerSpriteDir = "Assets/Sprites/Characters/Player";
        public const string PlayerAnimDir = "Assets/Animations/Player";
        public const string EnvSpriteDir = "Assets/Sprites/Environment";
        public const string EffectSpriteDir = "Assets/Sprites/Effects";
        public const string TileDir = "Assets/Tilemaps/Tiles";
        public const string GoalSpritePath = EnvSpriteDir + "/goal_beacon.png";
        public const string LadderSpritePath = EnvSpriteDir + "/ladder.png";

        // ---------------- Personaje ----------------

        const string CharacterArt = "ArtSource/01_Character";
        // 1 "píxel" del arte ≈ 7,5 px reales → el personaje de pie mide 39 px (≈1,2 u; collider 0,94 u).
        const float CharacterScale = 0.135f;

        struct Strip
        {
            public string file, name;
            public int expected;
            public bool hasLadder, anchorOnHead;
        }

        static readonly Strip[] CharacterStrips =
        {
            new Strip { file = "character_idle", name = "idle", expected = 4, anchorOnHead = true },
            new Strip { file = "character_walk_LeftToRight", name = "run", expected = 6, anchorOnHead = true },
            new Strip { file = "character_jump", name = "jump", expected = 3, anchorOnHead = true },
            new Strip { file = "character_climb", name = "climb", expected = 4, hasLadder = true, anchorOnHead = true },
            new Strip { file = "character_damage", name = "hurt", expected = 4, anchorOnHead = false },
        };

        // ---------------- Tileset ----------------

        const string TilesetArt = "ArtSource/02_Environment/factory_tileset.png";
        const string DecorationsArt = "ArtSource/02_Environment/factory_decorations.png";
        // Viga con franjas de advertencia (fila 2 del tileset). El periodo de las franjas es 60 px:
        // cada pieza abarca 3 periodos (180 px) para que el tramo central encaje consigo mismo.
        static readonly RectInt GirderLeft = new RectInt(240, 1361, 180, 187);
        static readonly RectInt GirderMid = new RectInt(420, 1361, 180, 187);
        static readonly RectInt GirderRight = new RectInt(668, 1361, 180, 187); // extremo con franjas naranjas
        // Un peldaño de la primera escalera (fila 6) con su hueco; se apila 4 veces por tile.
        static readonly RectInt LadderRung = new RectInt(52, 342, 136, 34);
        const int LadderWidth = 24, RungHeight = 8;
        // Baliza amarilla con destellos (factory_decorations, fila 5).
        static readonly RectInt BeaconArea = new RectInt(260, 562, 180, 190);

        static readonly string[] PlatformPieces = { "platform_left", "platform_mid", "platform_right" };
        static string PlatformSprite(string piece) => $"{EnvSpriteDir}/{piece}.png";
        static string PlatformTile(string piece) => $"{TileDir}/{piece}.asset";

        // ---------------- Efectos ----------------

        const string EffectArt = "ArtSource/05_Effects";

        struct EffectStrip
        {
            public string file, name;
            public int expected, maxSize;
            public int[] use;            // fotogramas usados (null = todos)
            public SpriteAlignment pivot;
        }

        static readonly EffectStrip[] EffectStrips =
        {
            new EffectStrip { file = "effect_dust", name = "dust", expected = 4, maxSize = 28, pivot = SpriteAlignment.BottomCenter },
            // Fotogramas 1-3 muestran el barril entero (ya lo dibuja el propio barril): solo destello y astillas.
            new EffectStrip { file = "effect_explosion", name = "smash", expected = 6, maxSize = 36, use = new[] { 0, 4, 5 }, pivot = SpriteAlignment.Center },
            new EffectStrip { file = "effect_sparks", name = "sparks", expected = 4, maxSize = 24, pivot = SpriteAlignment.Center },
            new EffectStrip { file = "effect_impact", name = "impact", expected = 4, maxSize = 26, pivot = SpriteAlignment.Center },
        };

        [MenuItem("Barrel Ascent/Setup/Build Phase 7 (Art + Audio)")]
        public static void Build()
        {
            ImportCharacter();
            ImportTiles();
            ImportBackgrounds();
            ImportEffects();
            CreateGoalSprite();
            AssetDatabase.SaveAssets();

            Phase5Builder.Build();   // nivel, jugador, barriles, reglas (usa ya el tile y la escalera nuevos)
            ApplyGoalSprite();
            WireScene();
            Phase6Builder.Build();   // la UI depende de los managers de GameLevel
            Debug.Log("[Phase7Builder] Phase 7 build complete.");
        }

        // =====================================================================
        // Personaje
        // =====================================================================

        static void ImportCharacter()
        {
            Directory.CreateDirectory(PlayerSpriteDir);
            var paths = new Dictionary<string, List<string>>();
            foreach (var strip in CharacterStrips)
            {
                var sheet = SpriteExtractor.Load(Path.Combine(CharacterArt, strip.file + ".png"));
                if (sheet.frames.Count != strip.expected) SpriteExtractor.FindFramesCount(sheet, strip.expected);

                var list = new List<string>();
                for (int i = 0; i < sheet.frames.Count; i++)
                {
                    var tex = SpriteExtractor.Downscale(sheet, sheet.frames[i], CharacterScale);
                    if (strip.hasLadder) SpriteExtractor.StripLadder(tex);
                    tex = CropToInk(tex);

                    // Ancla horizontal: centro de la cabeza (tercio superior), estable aunque las piernas se abran.
                    float ax = strip.anchorOnHead ? HeadCenterX(tex) : tex.width * 0.5f;
                    int half = Mathf.CeilToInt(Mathf.Max(ax, tex.width - ax)) + 1;
                    int h = tex.height;
                    tex = SpriteExtractor.Place(tex, half * 2, h, half - Mathf.RoundToInt(ax), 0);

                    string path = $"{PlayerSpriteDir}/player_{strip.name}_{i}.png";
                    SpriteExtractor.SavePng(tex, path);
                    list.Add(path);
                }
                paths[strip.name] = list;
            }
            AssetDatabase.Refresh();
            foreach (var p in paths.Values.SelectMany(l => l)) ConfigureSprite(p, SpriteAlignment.BottomCenter);

            Sprite[] Load(string n) => paths[n].Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToArray();
            var idle = Load("idle");
            var run = Load("run");
            var jump = Load("jump");
            var climb = Load("climb");
            var hurt = Load("hurt");

            Directory.CreateDirectory(PlayerAnimDir);
            var clips = new Dictionary<string, AnimationClip>
            {
                ["Idle"] = MakeClip("Player_Idle", idle, 6f, true),
                ["Run"] = MakeClip("Player_Run", run, 12f, true),
                ["JumpRise"] = MakeClip("Player_JumpRise", new[] { jump[1] }, 12f, false),
                ["JumpFall"] = MakeClip("Player_JumpFall", new[] { jump[2] }, 12f, false),
                ["Climb"] = MakeClip("Player_Climb", climb, 8f, true),
                ["Hurt"] = MakeClip("Player_Hurt", new[] { hurt[0] }, 12f, false),
            };
            BuildAnimator(clips);

            // Sprite por defecto del prefab (lo que se ve antes de que arranque el Animator).
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                root.GetComponentInChildren<SpriteRenderer>().sprite = idle[0];
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            Debug.Log($"[Phase7Builder] Character: idle {idle[0].rect.size}, run {run.Length} f, climb {climb.Length} f.");
        }

        static Texture2D CropToInk(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int x0 = tex.width, y0 = tex.height, x1 = -1, y1 = -1;
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    if (px[y * tex.width + x].a > 0)
                    {
                        x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
                        y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                    }
            if (x1 < 0) return tex;
            return SpriteExtractor.Place(tex, x1 - x0 + 1, y1 - y0 + 1, -x0, -y0);
        }

        static float HeadCenterX(Texture2D tex)
        {
            var px = tex.GetPixels32();
            float sum = 0f; int n = 0;
            for (int y = Mathf.FloorToInt(tex.height * 0.7f); y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    if (px[y * tex.width + x].a > 0) { sum += x + 0.5f; n++; }
            return n > 0 ? sum / n : tex.width * 0.5f;
        }

        static AnimationClip MakeClip(string name, Sprite[] frames, float fps, bool loop)
        {
            string path = $"{PlayerAnimDir}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.frameRate = fps;
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            // Clave final repetida para que el último fotograma dure lo mismo que los demás.
            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>
        /// Rehace la máquina de estados del Animator existente (mismo asset y GUID, así el prefab no pierde
        /// la referencia): Idle / Run / Jump (blend por VelocityY) / Climb (velocidad = ClimbSpeed) / Hurt.
        /// </summary>
        static void BuildAnimator(Dictionary<string, AnimationClip> clips)
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath)
                     ?? throw new InvalidOperationException("Falta Player.controller (ejecuta la Fase 2).");

            foreach (var (name, type) in new[]
                     {
                         ("Speed", AnimatorControllerParameterType.Float), ("Grounded", AnimatorControllerParameterType.Bool),
                         ("Climbing", AnimatorControllerParameterType.Bool), ("ClimbSpeed", AnimatorControllerParameterType.Float),
                         ("VelocityY", AnimatorControllerParameterType.Float), ("Hurt", AnimatorControllerParameterType.Bool),
                     })
                if (ac.parameters.All(p => p.name != name)) ac.AddParameter(name, type);

            var sm = ac.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);
            foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(PlayerControllerPath))
                if (o is BlendTree) AssetDatabase.RemoveObjectFromAsset(o);

            var idle = sm.AddState("Idle", new Vector3(300, 0));
            var run = sm.AddState("Run", new Vector3(300, 100));
            var jump = sm.AddState("Jump", new Vector3(550, 0));
            var climb = sm.AddState("Climb", new Vector3(550, 100));
            var hurt = sm.AddState("Hurt", new Vector3(550, 200));
            sm.defaultState = idle;

            idle.motion = clips["Idle"];
            run.motion = clips["Run"];
            hurt.motion = clips["Hurt"];
            climb.motion = clips["Climb"];
            climb.speedParameter = "ClimbSpeed";   // 0 = animación detenida en la escalera
            climb.speedParameterActive = true;

            var tree = new BlendTree
            {
                name = "JumpBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "VelocityY",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, ac);
            tree.AddChild(clips["JumpFall"], -0.5f);
            tree.AddChild(clips["JumpRise"], 0.5f);
            jump.motion = tree;

            Transition(idle, run, ("Speed", AnimatorConditionMode.Greater, 0.1f));
            Transition(run, idle, ("Speed", AnimatorConditionMode.Less, 0.1f));

            var toHurt = sm.AddAnyStateTransition(hurt);
            Configure(toHurt, ("Hurt", AnimatorConditionMode.If, 0));
            toHurt.canTransitionToSelf = false;
            Transition(hurt, idle, ("Hurt", AnimatorConditionMode.IfNot, 0));

            var toJump = sm.AddAnyStateTransition(jump);
            Configure(toJump, ("Grounded", AnimatorConditionMode.IfNot, 0), ("Climbing", AnimatorConditionMode.IfNot, 0),
                      ("Hurt", AnimatorConditionMode.IfNot, 0));
            toJump.canTransitionToSelf = false;
            Transition(jump, idle, ("Grounded", AnimatorConditionMode.If, 0));

            var toClimb = sm.AddAnyStateTransition(climb);
            Configure(toClimb, ("Climbing", AnimatorConditionMode.If, 0));
            toClimb.canTransitionToSelf = false;
            Transition(climb, idle, ("Climbing", AnimatorConditionMode.IfNot, 0));

            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
        }

        static void Transition(AnimatorState from, AnimatorState to, params (string, AnimatorConditionMode, float)[] conds)
            => Configure(from.AddTransition(to), conds);

        static void Configure(AnimatorStateTransition t, params (string p, AnimatorConditionMode m, float v)[] conds)
        {
            t.hasExitTime = false;
            t.duration = 0f;
            foreach (var c in conds) t.AddCondition(c.m, c.v, c.p);
        }

        // =====================================================================
        // Tileset: plataformas y escalera
        // =====================================================================

        static void ImportTiles()
        {
            Directory.CreateDirectory(EnvSpriteDir);
            var sheet = SpriteExtractor.Load(TilesetArt, minGap: 10000);

            var rects = new[] { GirderLeft, GirderMid, GirderRight };
            for (int i = 0; i < PlatformPieces.Length; i++)
            {
                var px = SpriteExtractor.Sample(sheet, rects[i], 32, 32);
                for (int k = 0; k < px.Length; k++) px[k].a = 255; // la viga llena el tile entero
                SpriteExtractor.SavePng(SpriteExtractor.NewTexture(32, 32, px), PlatformSprite(PlatformPieces[i]));
            }

            // Escalera: un peldaño remuestreado a 24×8, sin el fondo azul oscuro entre largueros, apilado 4 veces.
            var rung = SpriteExtractor.Sample(sheet, LadderRung, LadderWidth, RungHeight);
            for (int y = 0; y < RungHeight; y++)
                for (int x = 5; x < LadderWidth - 5; x++) // columnas interiores (los largueros ocupan 5 px por lado)
                {
                    var c = rung[y * LadderWidth + x];
                    if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 40) rung[y * LadderWidth + x] = new Color32(0, 0, 0, 0);
                }
            var ladder = new Color32[32 * 32];
            int ox = (32 - LadderWidth) / 2;
            for (int k = 0; k < 32 / RungHeight; k++)
                for (int y = 0; y < RungHeight; y++)
                    for (int x = 0; x < LadderWidth; x++)
                        ladder[(k * RungHeight + y) * 32 + ox + x] = rung[y * LadderWidth + x];
            SpriteExtractor.SavePng(SpriteExtractor.NewTexture(32, 32, ladder), LadderSpritePath);

            AssetDatabase.Refresh();
            foreach (var p in PlatformPieces) ConfigureSprite(PlatformSprite(p), SpriteAlignment.Center);
            ConfigureSprite(LadderSpritePath, SpriteAlignment.Center);

            // Tiles: uno por pieza, misma colisión de rejilla que el placeholder.
            Directory.CreateDirectory(TileDir);
            foreach (var p in PlatformPieces)
            {
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(PlatformTile(p));
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, PlatformTile(p));
                }
                tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlatformSprite(p));
                tile.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(tile);
            }

            // El tile placeholder de la Fase 3 pasa a usar el tramo central (por si se reconstruye sin la Fase 7).
            var ph = AssetDatabase.LoadAssetAtPath<Tile>(Phase3Builder.PlatformTilePath);
            if (ph != null)
            {
                ph.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlatformSprite("platform_mid"));
                EditorUtility.SetDirty(ph);
            }

            var root = PrefabUtility.LoadPrefabContents(Phase3Builder.LadderPrefabPath);
            try
            {
                root.GetComponentInChildren<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LadderSpritePath);
                PrefabUtility.SaveAsPrefabAsset(root, Phase3Builder.LadderPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // =====================================================================
        // Fondos
        // =====================================================================

        static string BackgroundSprite(int zone) => $"{EnvSpriteDir}/bg_zone{zone + 1}.png";

        /// <summary>Altura (en tiles) que cubre cada zona, igual que los paneles de la Fase 3.</summary>
        static (float bottom, float top) ZoneSpan(Phase3Builder.LevelLayout layout, int z)
        {
            float[] starts = layout.ZoneStartsY();
            float bottom = z == 0 ? -2f : starts[z];
            float top = z == 2 ? layout.Height + 2f : starts[z + 1];
            return (bottom, top);
        }

        static void ImportBackgrounds()
        {
            var layout = Phase3Builder.LevelLayout.Load(Phase3Builder.LayoutPath);
            Color32 shadow = Shadow;
            for (int z = 0; z < 3; z++)
            {
                var (bottom, top) = ZoneSpan(layout, z);
                int h = Mathf.RoundToInt((top - bottom) * ProjectSetup.PixelsPerUnit);
                int w = Mathf.RoundToInt(h * 16f / 9f / 2f) * 2;   // mantiene la proporción 16:9 del original
                var tex = SpriteExtractor.Resize($"ArtSource/02_Environment/factory_background_zone_0{z + 1}.jpg", w, h);

                // Oscurecer un poco (legibilidad) y fundir a sombra en los bordes superior/inferior
                // para que el cambio de una zona a otra no sea un corte brusco.
                var px = tex.GetPixels32();
                // La zona 2 tiene un resplandor naranja detrás de las plataformas: se oscurece más para que los barriles se vean.
                float brightness = z == 1 ? 0.62f : 0.8f;
                const float fade = 40f;
                for (int y = 0; y < h; y++)
                {
                    float edge = Mathf.Min(y, h - 1 - y);
                    float k = edge < fade ? Mathf.Lerp(0.35f, 1f, edge / fade) : 1f;
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[y * w + x];
                        Color32 lit = new Color32((byte)(c.r * brightness), (byte)(c.g * brightness), (byte)(c.b * brightness), 255);
                        px[y * w + x] = Color32.Lerp(shadow, lit, k);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                SpriteExtractor.SavePng(tex, BackgroundSprite(z));
            }
            AssetDatabase.Refresh();
            for (int z = 0; z < 3; z++)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(BackgroundSprite(z));
                importer.maxTextureSize = 2048;
                ConfigureSprite(BackgroundSprite(z), SpriteAlignment.Center);
            }
        }

        // =====================================================================
        // Efectos
        // =====================================================================

        static string EffectSprite(string name, int i) => $"{EffectSpriteDir}/fx_{name}_{i}.png";

        static void ImportEffects()
        {
            Directory.CreateDirectory(EffectSpriteDir);
            var saved = new List<(string path, SpriteAlignment pivot)>();
            foreach (var e in EffectStrips)
            {
                var sheet = SpriteExtractor.Load(Path.Combine(EffectArt, e.file + ".png"));
                SpriteExtractor.FindFramesCount(sheet, e.expected, minFrameSize: 8);
                int maxDim = sheet.frames.Max(f => Mathf.Max(f.width, f.height));
                float scale = e.maxSize / (float)maxDim;
                var use = e.use ?? Enumerable.Range(0, sheet.frames.Count).ToArray();
                for (int k = 0; k < use.Length; k++)
                {
                    var tex = SpriteExtractor.Downscale(sheet, sheet.frames[use[k]], scale);
                    // Lienzo par para que el pivote central caiga en el borde de un píxel.
                    tex = SpriteExtractor.Place(tex, tex.width + (tex.width & 1), tex.height + (tex.height & 1), 0, 0);
                    string path = EffectSprite(e.name, k);
                    SpriteExtractor.SavePng(tex, path);
                    saved.Add((path, e.pivot));
                }
            }
            AssetDatabase.Refresh();
            foreach (var (path, pivot) in saved) ConfigureSprite(path, pivot);
        }

        static Sprite[] EffectFrames(string name)
        {
            var list = new List<Sprite>();
            for (int i = 0; ; i++)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(EffectSprite(name, i));
                if (s == null) break;
                list.Add(s);
            }
            if (list.Count == 0) throw new InvalidOperationException($"No hay fotogramas para el efecto {name}.");
            return list.ToArray();
        }

        // =====================================================================
        // Meta
        // =====================================================================

        /// <summary>
        /// Meta 32×64: mástil y bandera a cuadros (dibujados, como antes) rematados con la baliza amarilla
        /// de factory_decorations, que la hace reconocible desde lejos.
        /// </summary>
        static void CreateGoalSprite()
        {
            var deco = SpriteExtractor.Load(DecorationsArt, minGap: 10000);
            var bounds = SpriteExtractor.InkBounds(deco, BeaconArea);
            var beacon = SpriteExtractor.Downscale(deco, bounds, 20f / bounds.width);
            int bw = beacon.width, bh = beacon.height;
            var beaconPx = beacon.GetPixels32();
            Object.DestroyImmediate(beacon);

            const int W = 32, H = 64, poleX = 14;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.SetPixels32(new Color32[W * H]);
            int poleTop = H - bh + 3; // la base de la baliza se apoya sobre el mástil
            Rect(t, poleX, 0, poleX + 2, poleTop, MetalGray);
            Rect(t, poleX, 0, poleX, poleTop, LightGray);
            Rect(t, poleX - 3, 0, poleX + 5, 2, Steel);
            int flagTop = poleTop - 3;
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 5; x++)
                    Rect(t, poleX + 3 + x * 3, flagTop - 11 + y * 3, poleX + 5 + x * 3, flagTop - 9 + y * 3,
                         (x + y) % 2 == 0 ? Yellow : Hex(0xB83B36));
            Rect(t, poleX + 3, flagTop - 12, poleX + 17, flagTop - 12, Shadow);

            int bx = poleX + 1 - bw / 2, by = H - bh;
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    var c = beaconPx[y * bw + x];
                    int tx = bx + x, ty = by + y;
                    if (c.a > 0 && tx >= 0 && tx < W && ty >= 0 && ty < H) t.SetPixel(tx, ty, c);
                }
            t.Apply();
            SpriteExtractor.SavePng(t, GoalSpritePath);
            AssetDatabase.Refresh();
            ConfigureSprite(GoalSpritePath, SpriteAlignment.BottomCenter);
        }

        static void ApplyGoalSprite()
        {
            var root = PrefabUtility.LoadPrefabContents(Phase5Builder.GoalPrefabPath);
            try
            {
                root.GetComponentInChildren<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GoalSpritePath);
                PrefabUtility.SaveAsPrefabAsset(root, Phase5Builder.GoalPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // =====================================================================
        // Escena
        // =====================================================================

        static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.GameLevelScene, OpenSceneMode.Single);
            var layout = Phase3Builder.LevelLayout.Load(Phase3Builder.LayoutPath);

            ApplyPlatformPieces();
            ApplyBackgrounds(layout);

            var managers = GameObject.Find("Managers")?.transform
                           ?? throw new InvalidOperationException("Falta Managers (ejecuta la Fase 5).");
            var player = Object.FindFirstObjectByType<PlayerController>();
            var collision = player.GetComponent<PlayerCollision>();

            // Efectos.
            var fxGo = new GameObject("EffectsManager");
            fxGo.transform.SetParent(managers, false);
            var fx = fxGo.AddComponent<EffectsManager>();
            var so = new SerializedObject(fx);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("playerCollision").objectReferenceValue = collision;
            SetSequence(so, "dust", EffectFrames("dust"), 14f, Vector2.zero);
            SetSequence(so, "barrelSmash", EffectFrames("smash"), 12f, Vector2.zero);
            SetSequence(so, "collect", EffectFrames("sparks"), 16f, Vector2.zero);
            SetSequence(so, "damage", EffectFrames("impact"), 14f, new Vector2(0f, 0.5f));
            so.ApplyModifiedPropertiesWithoutUndo();

            // Audio (ranuras vacías: no hay sonidos en el paquete).
            var audioGo = new GameObject("AudioManager");
            audioGo.transform.SetParent(managers, false);
            var audio = audioGo.AddComponent<AudioManager>();
            var aso = new SerializedObject(audio);
            aso.FindProperty("gameManager").objectReferenceValue = Object.FindFirstObjectByType<GameManager>();
            aso.FindProperty("levelManager").objectReferenceValue = Object.FindFirstObjectByType<LevelManager>();
            aso.FindProperty("player").objectReferenceValue = player;
            aso.FindProperty("playerCollision").objectReferenceValue = collision;
            aso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void SetSequence(SerializedObject so, string field, Sprite[] frames, float fps, Vector2 offset)
        {
            var p = so.FindProperty(field);
            var arr = p.FindPropertyRelative("frames");
            arr.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            p.FindPropertyRelative("fps").floatValue = fps;
            p.FindPropertyRelative("offset").vector2Value = offset;
        }

        /// <summary>
        /// Sustituye cada tile del Tilemap por su pieza: extremo izquierdo, tramo central o extremo derecho
        /// según los vecinos. Todas usan colisión de rejilla, así que el CompositeCollider2D no cambia.
        /// </summary>
        static void ApplyPlatformPieces()
        {
            var ground = GameObject.Find($"{Phase3Builder.LevelRootName}/Grid/Ground")
                         ?? throw new InvalidOperationException("Falta Level/Grid/Ground.");
            var tilemap = ground.GetComponent<Tilemap>();
            var left = AssetDatabase.LoadAssetAtPath<Tile>(PlatformTile("platform_left"));
            var mid = AssetDatabase.LoadAssetAtPath<Tile>(PlatformTile("platform_mid"));
            var right = AssetDatabase.LoadAssetAtPath<Tile>(PlatformTile("platform_right"));

            var cells = new List<Vector3Int>();
            foreach (var pos in tilemap.cellBounds.allPositionsWithin)
                if (tilemap.HasTile(pos)) cells.Add(pos);
            foreach (var pos in cells)
            {
                bool l = tilemap.HasTile(pos + Vector3Int.left), r = tilemap.HasTile(pos + Vector3Int.right);
                tilemap.SetTile(pos, !l && r ? left : l && !r ? right : mid);
            }

            var tc = ground.GetComponent<TilemapCollider2D>();
            var composite = ground.GetComponent<CompositeCollider2D>();
            tc.ProcessTilemapChanges();
            composite.GenerateGeometry();
            if (composite.pathCount == 0) throw new InvalidOperationException("El Tilemap se quedó sin colisiones.");
        }

        static void ApplyBackgrounds(Phase3Builder.LevelLayout layout)
        {
            for (int z = 0; z < 3; z++)
            {
                var go = GameObject.Find($"{Phase3Builder.LevelRootName}/Backgrounds/Background_Zone{z + 1}")
                         ?? throw new InvalidOperationException($"Falta Background_Zone{z + 1}.");
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSprite(z));
                sr.drawMode = SpriteDrawMode.Simple;
                sr.color = Color.white;
                var (bottom, top) = ZoneSpan(layout, z);
                var p = go.transform.position;
                go.transform.position = new Vector3(p.x, (bottom + top) * 0.5f, p.z);
            }
        }
    }
}
