using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BarrelAscent.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using static BarrelAscent.EditorTools.Phase2Builder;
using Object = UnityEngine.Object;

namespace BarrelAscent.EditorTools
{
    /// <summary>
    /// Fase 3: construye el nivel vertical de GameLevel a partir de Assets/Tilemaps/LevelLayout.txt.
    ///
    /// Leyenda (1 carácter = 1 tile de 32 px; la primera línea es la fila superior):
    ///   #  plataforma sólida (Tilemap "Ground", capa Ground)
    ///   H  escalera (en filas vacías; sube hasta la superficie de la plataforma de encima)
    ///   P  punto de inicio del jugador      G  meta
    ///   S  punto de aparición de barriles    C  coleccionable
    ///   2/3  fila donde empieza la zona 2 / 3
    ///   .  vacío
    ///
    /// Menú: Barrel Ascent/Setup/Build Phase 3 (Level).
    /// Batchmode: -executeMethod BarrelAscent.EditorTools.Phase3Builder.Build
    /// Reejecutable: reconstruye "Level" y el jugador; respeta el resto de la escena.
    /// </summary>
    public static class Phase3Builder
    {
        public const string LayoutPath = "Assets/Tilemaps/LevelLayout.txt";
        public const string PlatformTilePath = "Assets/Tilemaps/Tiles/PH_Platform.asset";
        public const string LadderPrefabPath = "Assets/Prefabs/Environment/Ladder.prefab";
        public const string WallPanelSpritePath = PlaceholderDir + "/PH_WallPanel.png";

        public const string LevelRootName = "Level";
        public const float LevelOriginX = -10f; // la columna 0 empieza en x = -10 → nivel centrado en x = 0

        static readonly Color[] ZoneTints =
        {
            new Color(1f, 1f, 1f),        // Zona 1: base industrial (azul)
            new Color(1f, 0.82f, 0.72f),  // Zona 2: maquinaria (tono óxido)
            new Color(0.7f, 0.72f, 0.8f), // Zona 3: la cima (más oscura)
        };

        [MenuItem("Barrel Ascent/Setup/Build Phase 3 (Level)")]
        public static void Build()
        {
            EnsureLayers();
            var layout = LevelLayout.Load(LayoutPath);
            layout.Validate();

            // Crear los assets primero; abrir la escena descarga los objetos no referenciados,
            // así que se vuelven a cargar después de abrirla.
            CreatePlatformTile();
            CreateLadderPrefab();
            CreateWallPanelSprite();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(ProjectSetup.GameLevelScene, OpenSceneMode.Single);

            var tile = AssetDatabase.LoadAssetAtPath<Tile>(PlatformTilePath);
            var ladderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LadderPrefabPath);
            var wallSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WallPanelSpritePath);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null) throw new InvalidOperationException("Ejecuta antes Build Phase 2 (falta Player.prefab).");
            if (tile == null || ladderPrefab == null || wallSprite == null)
                throw new InvalidOperationException("No se pudieron cargar los assets del nivel (tile, escalera o fondo).");
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == LevelRootName || go.name == "Player" || go.name == "Phase2_TestArea")
                    Object.DestroyImmediate(go);

            var level = new GameObject(LevelRootName);
            BuildTilemap(level.transform, layout, tile);
            BuildLadders(level.transform, layout, ladderPrefab);
            BuildBounds(level.transform, layout);
            BuildBackgrounds(level.transform, layout, wallSprite);
            BuildMarkers(level.transform, layout);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = layout.CellToFeet(layout.Single('P'));

            ConfigureCamera(player.transform, layout);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase3Builder] Level built: {layout.Width}x{layout.Height} tiles, " +
                      $"{layout.LadderRuns().Count} ladders, {layout.All('C').Count} collectible markers.");
        }

        // ---------------- Assets ----------------

        static Tile CreatePlatformTile()
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(PlatformTilePath);
            if (tile != null) return tile;
            Directory.CreateDirectory(Path.GetDirectoryName(PlatformTilePath));
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlatformSpritePath);
            tile.colliderType = Tile.ColliderType.Grid; // colisión exacta de 1x1
            AssetDatabase.CreateAsset(tile, PlatformTilePath);
            return tile;
        }

        static GameObject CreateLadderPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LadderPrefabPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(LadderPrefabPath));
            var root = new GameObject("Ladder") { layer = Layer(LadderLayer) };
            root.AddComponent<BoxCollider2D>().isTrigger = true;
            var ladder = root.AddComponent<Ladder>();

            var visual = new GameObject("Visual") { layer = root.layer };
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LadderSpritePath);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.sortingOrder = -1; // detrás de plataformas y personajes

            ladder.Configure(4f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, LadderPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Panel de pared industrial para el fondo: placas con juntas y remaches, tonos oscuros.
        static Sprite CreateWallPanelSprite()
        {
            if (!File.Exists(WallPanelSpritePath))
            {
                var t = NewTex(DarkBlue);
                Rect(t, 0, 0, 31, 0, Shadow);
                Rect(t, 0, 0, 0, 31, Shadow);
                Rect(t, 1, 31, 31, 31, Hex(0x223744));
                Rect(t, 31, 1, 31, 31, Hex(0x223744));
                Rect(t, 3, 27, 4, 28, Steel);
                Rect(t, 27, 27, 28, 28, Steel);
                Rect(t, 3, 3, 4, 4, Steel);
                Rect(t, 27, 3, 28, 4, Steel);
                Rect(t, 8, 14, 23, 15, Hex(0x1E3340)); // junta horizontal
                WritePng(WallPanelSpritePath, Finish(t));
                AssetDatabase.Refresh();
                ConfigureSprite(WallPanelSpritePath, SpriteAlignment.Center);
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(WallPanelSpritePath);
        }

        // ---------------- Construcción ----------------

        static void BuildTilemap(Transform parent, LevelLayout layout, Tile tile)
        {
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(parent, false);
            gridGo.transform.position = new Vector3(LevelOriginX, 0f, 0f);
            gridGo.AddComponent<Grid>().cellSize = Vector3.one;

            var groundGo = new GameObject("Ground") { layer = Layer(GroundLayer) };
            groundGo.transform.SetParent(gridGo.transform, false);
            var tilemap = groundGo.AddComponent<Tilemap>();
            var renderer = groundGo.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = 0;

            for (int r = 0; r < layout.Height; r++)
                for (int c = 0; c < layout.Width; c++)
                    if (layout[c, r] == '#')
                        tilemap.SetTile(new Vector3Int(c, r, 0), tile);
            tilemap.CompressBounds();

            var rb = groundGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var tc = groundGo.AddComponent<TilemapCollider2D>();
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = groundGo.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;

            tc.ProcessTilemapChanges();
            composite.GenerateGeometry();
            if (composite.pathCount == 0)
                throw new InvalidOperationException("El Tilemap no generó colisiones (CompositeCollider2D vacío).");
        }

        static void BuildLadders(Transform parent, LevelLayout layout, GameObject prefab)
        {
            var root = new GameObject("Ladders");
            root.transform.SetParent(parent, false);
            int i = 0;
            foreach (var run in layout.LadderRuns())
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                go.name = $"Ladder_{i++:00}";
                go.transform.position = new Vector3(LevelOriginX + run.column + 0.5f, run.bottomY, 0f);
                var ladder = go.GetComponent<Ladder>();
                ladder.Configure(run.topY - run.bottomY);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<BoxCollider2D>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponentInChildren<SpriteRenderer>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponentInChildren<SpriteRenderer>().transform);
            }
        }

        static void BuildBounds(Transform parent, LevelLayout layout)
        {
            var root = new GameObject("Bounds");
            root.transform.SetParent(parent, false);
            float h = layout.Height + 10f;
            Wall(root.transform, "Wall_Left", LevelOriginX - 0.5f, h);
            Wall(root.transform, "Wall_Right", LevelOriginX + layout.Width + 0.5f, h);
        }

        static void Wall(Transform parent, string name, float x, float height)
        {
            var go = new GameObject(name) { layer = Layer(GroundLayer) };
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, height * 0.5f - 5f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(1f, height);
        }

        static void BuildBackgrounds(Transform parent, LevelLayout layout, Sprite sprite)
        {
            var root = new GameObject("Backgrounds");
            root.transform.SetParent(parent, false);
            float[] starts = layout.ZoneStartsY();
            for (int z = 0; z < 3; z++)
            {
                float bottom = z == 0 ? -2f : starts[z];             // un poco por debajo del suelo
                float top = z == 2 ? layout.Height + 2f : starts[z + 1];
                var go = new GameObject($"Background_Zone{z + 1}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(LevelOriginX + layout.Width * 0.5f, (bottom + top) * 0.5f, 5f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(layout.Width + 4f, top - bottom);
                sr.color = ZoneTints[z];
                sr.sortingOrder = -20;
            }
        }

        static void BuildMarkers(Transform parent, LevelLayout layout)
        {
            var root = new GameObject("Markers");
            root.transform.SetParent(parent, false);

            Marker(root.transform, "PlayerSpawn", layout.CellToFeet(layout.Single('P')));
            Marker(root.transform, "Goal", layout.CellToFeet(layout.Single('G')));

            var spawns = layout.All('S').OrderBy(p => p.y).ToList();
            for (int i = 0; i < spawns.Count; i++)
                Marker(root.transform, $"BarrelSpawn_Z{i + 1}", layout.CellToFeet(spawns[i]));

            float[] starts = layout.ZoneStartsY();
            for (int z = 0; z < 3; z++)
                Marker(root.transform, $"ZoneStart_{z + 1}", new Vector3(0f, starts[z], 0f));

            var collectibles = new GameObject("Collectibles");
            collectibles.transform.SetParent(root.transform, false);
            int n = 0;
            foreach (var cell in layout.All('C').OrderBy(p => p.y).ThenBy(p => p.x))
                Marker(collectibles.transform, $"Collectible_{n++:00}", layout.CellToCenter(cell));
        }

        static void Marker(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
        }

        static void ConfigureCamera(Transform player, LevelLayout layout)
        {
            var cam = Camera.main;
            if (cam == null) throw new InvalidOperationException("GameLevel no tiene Main Camera.");
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();

            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player;
            so.FindProperty("levelCenterX").floatValue = LevelOriginX + layout.Width * 0.5f;
            so.FindProperty("minY").floatValue = 0f;
            so.FindProperty("maxY").floatValue = layout.Height;
            so.ApplyModifiedPropertiesWithoutUndo();

            float half = ProjectSetup.RefResolutionY / (2f * ProjectSetup.PixelsPerUnit);
            cam.transform.position = new Vector3(0f, half, -10f);
        }

        // ---------------- Lectura y validación del layout ----------------

        public class LevelLayout
        {
            readonly char[,] cells; // [columna, fila], fila 0 = abajo
            public int Width { get; }
            public int Height { get; }

            public char this[int c, int r] => c < 0 || r < 0 || c >= Width || r >= Height ? '.' : cells[c, r];

            LevelLayout(List<string> topDownLines)
            {
                Height = topDownLines.Count;
                Width = topDownLines[0].Length;
                cells = new char[Width, Height];
                for (int i = 0; i < Height; i++)
                {
                    string line = topDownLines[i];
                    if (line.Length != Width)
                        throw new FormatException($"LevelLayout: la línea {i + 1} mide {line.Length}, se esperaba {Width}.");
                    int r = Height - 1 - i;
                    for (int c = 0; c < Width; c++) cells[c, r] = line[c];
                }
            }

            public static LevelLayout Load(string path)
            {
                var lines = File.ReadAllLines(path).Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToList();
                if (lines.Count == 0) throw new FormatException("LevelLayout vacío.");
                return new LevelLayout(lines);
            }

            public List<Vector2Int> All(char ch)
            {
                var list = new List<Vector2Int>();
                for (int r = 0; r < Height; r++)
                    for (int c = 0; c < Width; c++)
                        if (cells[c, r] == ch) list.Add(new Vector2Int(c, r));
                return list;
            }

            public Vector2Int Single(char ch)
            {
                var all = All(ch);
                if (all.Count != 1) throw new FormatException($"LevelLayout: se esperaba exactamente un '{ch}', hay {all.Count}.");
                return all[0];
            }

            /// <summary>Posición de los pies de algo apoyado en la celda (centro X, base Y).</summary>
            public Vector3 CellToFeet(Vector2Int cell) => new Vector3(LevelOriginX + cell.x + 0.5f, cell.y, 0f);

            public Vector3 CellToCenter(Vector2Int cell) => new Vector3(LevelOriginX + cell.x + 0.5f, cell.y + 0.5f, 0f);

            public struct LadderRun { public int column; public float bottomY; public float topY; }

            /// <summary>
            /// Tramos verticales de 'H'. La base es la superficie de la plataforma de abajo y la cima es la
            /// superficie de la plataforma de arriba (la escalera atraviesa esa fila de plataforma).
            /// </summary>
            public List<LadderRun> LadderRuns()
            {
                var runs = new List<LadderRun>();
                for (int c = 0; c < Width; c++)
                {
                    int r = 0;
                    while (r < Height)
                    {
                        if (cells[c, r] != 'H') { r++; continue; }
                        int start = r;
                        while (r < Height && cells[c, r] == 'H') r++;
                        // r = primera fila por encima del tramo (debe ser plataforma)
                        runs.Add(new LadderRun { column = c, bottomY = start, topY = r + 1 });
                    }
                }
                return runs;
            }

            /// <summary>Altura Y donde empieza cada zona: [0, fila de '2', fila de '3'].</summary>
            public float[] ZoneStartsY() => new float[] { 0f, Single('2').y, Single('3').y };

            public void Validate()
            {
                var errors = new List<string>();
                foreach (var run in LadderRuns())
                {
                    if (this[run.column, (int)run.bottomY - 1] != '#')
                        errors.Add($"Escalera en columna {run.column}, fila {run.bottomY}: no hay plataforma debajo.");
                    if (this[run.column, (int)run.topY - 1] != '#')
                        errors.Add($"Escalera en columna {run.column}: no llega a una plataforma arriba.");
                }
                foreach (char ch in new[] { 'P', 'G', 'S' })
                    foreach (var p in All(ch))
                        if (this[p.x, p.y - 1] != '#')
                            errors.Add($"'{ch}' en ({p.x},{p.y}) no está apoyado en una plataforma.");
                try { Single('P'); Single('G'); ZoneStartsY(); }
                catch (FormatException e) { errors.Add(e.Message); }
                if (All('S').Count != 3) errors.Add($"Se esperaban 3 'S' (una por zona), hay {All('S').Count}.");

                if (errors.Count > 0)
                    throw new FormatException("LevelLayout inválido:\n- " + string.Join("\n- ", errors));
            }
        }
    }
}
