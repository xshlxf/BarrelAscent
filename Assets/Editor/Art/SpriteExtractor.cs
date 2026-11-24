using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BarrelAscent.EditorTools.Art
{
    /// <summary>
    /// Convierte las imágenes generadas por IA (ArtSource/) en sprites de pixel art reales:
    /// 1) elimina el fondo (transparente o blanco) por relleno desde los bordes,
    /// 2) separa los fotogramas de una tira horizontal por columnas vacías,
    /// 3) reduce cada fotograma a la escala del juego (32 PPU) muestreando el centro de cada bloque.
    /// Las imágenes de origen tienen "píxeles" de ~8-10 px reales, por eso se reducen.
    /// </summary>
    public static class SpriteExtractor
    {
        public class Sheet
        {
            public Color32[] pixels;
            public bool[] foreground;
            public int width, height;
            public List<RectInt> frames = new List<RectInt>();

            public bool IsForeground(int x, int y) => foreground[y * width + x];
            public Color32 Get(int x, int y) => pixels[y * width + x];
        }

        /// <summary>Carga una imagen, quita el fondo y detecta los fotogramas (de izquierda a derecha).</summary>
        public static Sheet Load(string path, int minGap = 12, int minFrameSize = 24)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
                throw new IOException($"No se pudo leer {path}");

            var sheet = new Sheet { width = tex.width, height = tex.height, pixels = tex.GetPixels32() };
            Object.DestroyImmediate(tex);
            sheet.foreground = BuildForegroundMask(sheet);
            sheet.frames = FindFrames(sheet, minGap, minFrameSize);
            return sheet;
        }

        // Relleno por inundación desde los bordes: solo el fondo conectado al exterior se elimina,
        // así los brillos claros dentro del sprite se conservan.
        // El color de fondo se toma de la esquina: transparente, blanco (la mayoría) o negro (paneles de UI).
        static bool[] BuildForegroundMask(Sheet s)
        {
            int w = s.width, h = s.height;
            Color32 corner = s.pixels[0];
            bool lightBackground = corner.a < 40 || (corner.r > 200 && corner.g > 200 && corner.b > 200);

            bool LooksLikeBackground(Color32 c)
            {
                if (c.a < 40) return true;
                if (lightBackground) return c.r > 205 && c.g > 205 && c.b > 205;
                int d = Mathf.Abs(c.r - corner.r) + Mathf.Abs(c.g - corner.g) + Mathf.Abs(c.b - corner.b);
                return d < 24;
            }

            var background = new bool[w * h];
            var queue = new Queue<int>();

            void TrySeed(int x, int y)
            {
                int i = y * w + x;
                if (!background[i] && LooksLikeBackground(s.pixels[i]))
                {
                    background[i] = true;
                    queue.Enqueue(i);
                }
            }

            for (int x = 0; x < w; x++) { TrySeed(x, 0); TrySeed(x, h - 1); }
            for (int y = 0; y < h; y++) { TrySeed(0, y); TrySeed(w - 1, y); }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;
                if (x > 0) TrySeed(x - 1, y);
                if (x < w - 1) TrySeed(x + 1, y);
                if (y > 0) TrySeed(x, y - 1);
                if (y < h - 1) TrySeed(x, y + 1);
            }

            var fg = new bool[w * h];
            for (int i = 0; i < fg.Length; i++)
                fg[i] = !background[i] && s.pixels[i].a >= 40;
            return fg;
        }

        static List<RectInt> FindFrames(Sheet s, int minGap, int minFrameSize)
        {
            var colHasInk = new bool[s.width];
            for (int x = 0; x < s.width; x++)
                for (int y = 0; y < s.height && !colHasInk[x]; y++)
                    colHasInk[x] = s.IsForeground(x, y);

            var frames = new List<RectInt>();
            int xStart = -1, lastInk = -1;
            for (int x = 0; x <= s.width; x++)
            {
                bool ink = x < s.width && colHasInk[x];
                if (ink)
                {
                    if (xStart < 0) xStart = x;
                    lastInk = x;
                }
                else if (xStart >= 0 && (x - lastInk > minGap || x == s.width))
                {
                    AddFrame(s, xStart, lastInk, frames, minFrameSize);
                    xStart = -1;
                }
            }
            return frames;
        }

        static void AddFrame(Sheet s, int x0, int x1, List<RectInt> frames, int minFrameSize)
        {
            int y0 = int.MaxValue, y1 = -1;
            for (int y = 0; y < s.height; y++)
                for (int x = x0; x <= x1; x++)
                    if (s.IsForeground(x, y)) { y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); break; }
            if (y1 < 0) return;
            var r = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            if (r.width >= minFrameSize && r.height >= minFrameSize) frames.Add(r);
        }

        /// <summary>
        /// Reduce un fotograma con un factor fijo (px de salida por px de origen).
        /// Cada píxel de salida toma el color del centro de su bloque; es transparente si
        /// menos de la mitad del bloque pertenece al sprite.
        /// </summary>
        public static Texture2D Downscale(Sheet s, RectInt frame, float scale)
        {
            int w = Mathf.Max(1, Mathf.RoundToInt(frame.width * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(frame.height * scale));
            var output = new Color32[w * h];
            float bw = frame.width / (float)w, bh = frame.height / (float)h;

            for (int oy = 0; oy < h; oy++)
                for (int ox = 0; ox < w; ox++)
                {
                    int sx0 = frame.x + Mathf.FloorToInt(ox * bw), sx1 = frame.x + Mathf.FloorToInt((ox + 1) * bw);
                    int sy0 = frame.y + Mathf.FloorToInt(oy * bh), sy1 = frame.y + Mathf.FloorToInt((oy + 1) * bh);
                    int total = 0, ink = 0;
                    for (int y = sy0; y < Mathf.Max(sy1, sy0 + 1); y++)
                        for (int x = sx0; x < Mathf.Max(sx1, sx0 + 1); x++)
                        {
                            total++;
                            if (s.IsForeground(x, y)) ink++;
                        }

                    if (ink * 2 < total) { output[oy * w + ox] = new Color32(0, 0, 0, 0); continue; }

                    int cx = Mathf.Clamp((sx0 + sx1) / 2, frame.xMin, frame.xMax - 1);
                    int cy = Mathf.Clamp((sy0 + sy1) / 2, frame.yMin, frame.yMax - 1);
                    Color32 c = s.IsForeground(cx, cy) ? s.Get(cx, cy) : NearestInk(s, sx0, sy0, sx1, sy1);
                    c.a = 255;
                    output[oy * w + ox] = c;
                }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(output);
            tex.Apply();
            return tex;
        }

        static Color32 NearestInk(Sheet s, int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y < Mathf.Max(y1, y0 + 1); y++)
                for (int x = x0; x < Mathf.Max(x1, x0 + 1); x++)
                    if (s.IsForeground(x, y)) return s.Get(x, y);
            return new Color32(0, 0, 0, 0);
        }

        /// <summary>
        /// Reduce una imagen completa (sin quitar fondo) a w×h muestreando el centro de cada bloque.
        /// Para fondos de pantalla.
        /// </summary>
        public static Texture2D Resize(string path, int w, int h)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(File.ReadAllBytes(path))) throw new IOException($"No se pudo leer {path}");
            var pixels = src.GetPixels32();
            int sw = src.width, sh = src.height;
            Object.DestroyImmediate(src);

            var output = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * sw / w), 0, sw - 1);
                    int sy = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) * sh / h), 0, sh - 1);
                    var c = pixels[sy * sw + sx];
                    c.a = 255;
                    output[y * w + x] = c;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(output);
            tex.Apply();
            return tex;
        }

        // ---------------- Recorte, limpieza y composición ----------------

        /// <summary>
        /// Vuelve a separar los fotogramas buscando el hueco mínimo que da exactamente <paramref name="expected"/>
        /// fotogramas (las tiras de efectos tienen partículas sueltas que un hueco pequeño separaría).
        /// </summary>
        public static void FindFramesCount(Sheet s, int expected, int minFrameSize = 24)
        {
            for (int gap = 160; gap >= 8; gap -= 4)
            {
                var frames = FindFrames(s, gap, minFrameSize);
                if (frames.Count == expected) { s.frames = frames; return; }
            }
            throw new System.InvalidOperationException($"No se encontraron {expected} fotogramas.");
        }

        /// <summary>Caja que envuelve todo el primer plano dentro de un área (coordenadas de Unity, y hacia arriba).</summary>
        public static RectInt InkBounds(Sheet s, RectInt area)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = area.yMin; y < area.yMax; y++)
                for (int x = area.xMin; x < area.xMax; x++)
                    if (s.IsForeground(x, y))
                    {
                        x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
                        y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                    }
            if (x1 < 0) throw new System.InvalidOperationException($"Área {area} vacía.");
            return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        /// <summary>
        /// Remuestrea un rectángulo del origen a w×h exactos tomando el píxel central de cada celda
        /// (sin regla de mayoría). Para piezas de tileset que deben llenar el tile entero.
        /// </summary>
        public static Color32[] Sample(Sheet s, RectInt src, int w, int h)
        {
            var output = new Color32[w * h];
            for (int oy = 0; oy < h; oy++)
                for (int ox = 0; ox < w; ox++)
                {
                    int sx = src.x + Mathf.Min(src.width - 1, (int)((ox + 0.5f) * src.width / w));
                    int sy = src.y + Mathf.Min(src.height - 1, (int)((oy + 0.5f) * src.height / h));
                    var c = s.Get(sx, sy);
                    c.a = s.IsForeground(sx, sy) ? (byte)255 : (byte)0;
                    output[oy * w + ox] = c;
                }
            return output;
        }

        /// <summary>
        /// Quita la escalera dibujada dentro de los fotogramas de trepar: largueros (columnas casi llenas
        /// en los bordes), peldaños grises conectados a ellos, contornos oscuros que se quedan sueltos
        /// y restos de pocos píxeles. Los guantes (gris cálido) y el mono (azul saturado) se conservan.
        /// </summary>
        public static void StripLadder(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            var removed = new bool[w * h];
            bool Opaque(int i) => px[i].a > 0;

            float ColumnFill(int x)
            {
                int n = 0;
                for (int y = 0; y < h; y++) if (Opaque(y * w + x)) n++;
                return n / (float)h;
            }
            for (int x = 0; x < w && ColumnFill(x) > 0.85f; x++)
                for (int y = 0; y < h; y++) removed[y * w + x] = true;
            for (int x = w - 1; x >= 0 && ColumnFill(x) > 0.85f; x--)
                for (int y = 0; y < h; y++) removed[y * w + x] = true;

            bool LadderGray(Color32 c)
            {
                int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                float lum = (c.r + c.g + c.b) / 3f;
                return c.a > 0 && max - min < 40 && c.b >= c.r - 4 && lum >= 80 && lum <= 220;
            }
            bool Dark(Color32 c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 70;

            var stack = new Stack<int>();
            for (int i = 0; i < removed.Length; i++) if (removed[i]) stack.Push(i);
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % w, y = i / w;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (!removed[j] && LadderGray(px[j])) { removed[j] = true; stack.Push(j); }
                }
            }

            for (int pass = 0; pass < 3; pass++)
            {
                var kill = new List<int>();
                for (int i = 0; i < px.Length; i++)
                {
                    if (removed[i] || !Opaque(i) || !Dark(px[i])) continue;
                    int x = i % w, y = i / w;
                    bool keep = false;
                    for (int dy = -1; dy <= 1 && !keep; dy++)
                        for (int dx = -1; dx <= 1 && !keep; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int j = ny * w + nx;
                            keep = Opaque(j) && !removed[j] && !Dark(px[j]);
                        }
                    if (!keep) kill.Add(i);
                }
                foreach (int i in kill) removed[i] = true;
            }

            RemoveSmallIslands(px, removed, w, h, 10);
            for (int i = 0; i < px.Length; i++) if (removed[i]) px[i] = new Color32(0, 0, 0, 0);
            tex.SetPixels32(px);
            tex.Apply();
        }

        static void RemoveSmallIslands(Color32[] px, bool[] removed, int w, int h, int minSize)
        {
            var seen = new bool[px.Length];
            var comp = new List<int>();
            for (int i = 0; i < px.Length; i++)
            {
                if (seen[i] || removed[i] || px[i].a == 0) continue;
                comp.Clear();
                comp.Add(i);
                seen[i] = true;
                for (int k = 0; k < comp.Count; k++)
                {
                    int x = comp[k] % w, y = comp[k] / w;
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (!seen[j] && !removed[j] && px[j].a > 0) { seen[j] = true; comp.Add(j); }
                    }
                }
                if (comp.Count < minSize) foreach (int j in comp) removed[j] = true;
            }
        }

        /// <summary>Devuelve una copia con 1 px de margen y un contorno de color alrededor del sprite.</summary>
        public static Texture2D AddOutline(Texture2D tex, Color32 color)
        {
            int w = tex.width + 2, h = tex.height + 2;
            var src = tex.GetPixels32();
            var output = new Color32[w * h];
            bool Ink(int x, int y) => x >= 0 && y >= 0 && x < tex.width && y < tex.height && src[y * tex.width + x].a > 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = x - 1, sy = y - 1;
                    if (Ink(sx, sy)) { output[y * w + x] = src[sy * tex.width + sx]; continue; }
                    bool edge = Ink(sx - 1, sy) || Ink(sx + 1, sy) || Ink(sx, sy - 1) || Ink(sx, sy + 1);
                    output[y * w + x] = edge ? color : new Color32(0, 0, 0, 0);
                }
            Object.DestroyImmediate(tex);
            return NewTexture(w, h, output);
        }

        /// <summary>Copia un sprite en un lienzo w×h con su esquina inferior izquierda en (ox, oy).</summary>
        public static Texture2D Place(Texture2D tex, int w, int h, int ox, int oy)
        {
            var src = tex.GetPixels32();
            var output = new Color32[w * h];
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    int tx = x + ox, ty = y + oy;
                    if (tx < 0 || ty < 0 || tx >= w || ty >= h) continue;
                    if (src[y * tex.width + x].a > 0) output[ty * w + tx] = src[y * tex.width + x];
                }
            Object.DestroyImmediate(tex);
            return NewTexture(w, h, output);
        }

        public static Texture2D NewTexture(int w, int h, Color32[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static void SavePng(Texture2D tex, string assetPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
