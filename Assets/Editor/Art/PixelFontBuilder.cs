using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace BarrelAscent.EditorTools.Art
{
    /// <summary>
    /// Crea "BarrelPixel", una fuente bitmap de 5×7 px (mayúsculas, dígitos, signos y vocales con tilde)
    /// como TMP_FontAsset en modo RASTER, para que el texto de la interfaz sea pixel art como el resto del juego.
    /// Tamaño de fuente 9 = 1 píxel de glifo por píxel de referencia (640×360); usar múltiplos (18, 27, 36).
    /// Las minúsculas se dibujan con el glifo de la mayúscula.
    /// </summary>
    public static class PixelFontBuilder
    {
        public const string FontPath = "Assets/UI/Fonts/BarrelPixel.asset";
        public const int PointSize = 9;
        const int CapHeight = 7;
        const int AtlasSize = 256;

        // Filas de arriba abajo; '#' = píxel encendido. Las tildes ocupan 2 filas extra encima.
        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { "###", ".#.", ".#.", ".#.", ".#.", ".#.", "###" },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###." },
            [':'] = new[] { ".", "#", ".", ".", ".", "#", "." },
            ['.'] = new[] { ".", ".", ".", ".", ".", ".", "#" },
            [','] = new[] { "..", "..", "..", "..", "..", ".#", "#." },
            ['!'] = new[] { "#", "#", "#", "#", "#", ".", "#" },
            ['¡'] = new[] { "#", ".", "#", "#", "#", "#", "#" },
            ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
            ['-'] = new[] { "...", "...", "...", "###", "...", "...", "..." },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            ['/'] = new[] { "....#", "...#.", "...#.", "..#..", ".#...", ".#...", "#...." },
            ['·'] = new[] { ".", ".", ".", "#", ".", ".", "." },
            ['×'] = new[] { ".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#" },
            ['\''] = new[] { "#", "#", ".", ".", ".", ".", "." },
        };

        static readonly Dictionary<char, (char baseChar, string[] accent)> Accented = new Dictionary<char, (char, string[])>
        {
            ['Á'] = ('A', new[] { "...#.", "..#.." }),
            ['É'] = ('E', new[] { "...#.", "..#.." }),
            ['Í'] = ('I', new[] { "..#", ".#." }),
            ['Ó'] = ('O', new[] { "...#.", "..#.." }),
            ['Ú'] = ('U', new[] { "...#.", "..#.." }),
            ['Ñ'] = ('N', new[] { ".##.#", "#..#." }),
            ['Ü'] = ('U', new[] { ".#.#.", "....." }),
        };

        [MenuItem("Barrel Ascent/Tools/Build Pixel Font")]
        public static TMP_FontAsset Build()
        {
            var all = new List<(char ch, string[] rows)>();
            foreach (var kv in Glyphs) all.Add((kv.Key, kv.Value));
            foreach (var kv in Accented)
            {
                var rows = new List<string>(kv.Value.accent);
                rows.AddRange(Glyphs[kv.Value.baseChar]);
                all.Add((kv.Key, rows.ToArray()));
            }

            var atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false)
            {
                name = "BarrelPixel Atlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var clear = new Color32[AtlasSize * AtlasSize];
            atlas.SetPixels32(clear);

            var glyphs = new List<Glyph>();
            var characters = new List<TMP_Character>();
            int x = 2, y = 2, rowHeight = 0;
            uint index = 1;

            // Espacio: sin píxeles, avance de 4.
            var space = new Glyph(index++, new GlyphMetrics(0, 0, 0, 0, 4), new GlyphRect(0, 0, 0, 0), 1f, 0);
            glyphs.Add(space);
            characters.Add(new TMP_Character(' ', space));

            foreach (var (ch, rows) in all)
            {
                int w = rows[0].Length, h = rows.Length;
                if (x + w + 2 > AtlasSize) { x = 2; y += rowHeight + 3; rowHeight = 0; }
                for (int r = 0; r < h; r++)
                    for (int c = 0; c < w; c++)
                        if (rows[r][c] == '#')
                            atlas.SetPixel(x + c, y + (h - 1 - r), Color.white); // atlas: origen abajo

                var glyph = new Glyph(index++, new GlyphMetrics(w, h, 0, h, w + 1), new GlyphRect(x, y, w, h), 1f, 0);
                glyphs.Add(glyph);
                characters.Add(new TMP_Character(ch, glyph));
                char lower = char.ToLowerInvariant(ch);
                if (lower != ch) characters.Add(new TMP_Character(lower, glyph));

                x += w + 3;
                rowHeight = Mathf.Max(rowHeight, h);
            }
            atlas.Apply();

            var font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            font.name = "BarrelPixel";
            font.faceInfo = new FaceInfo
            {
                familyName = "BarrelPixel",
                styleName = "Regular",
                pointSize = PointSize,
                scale = 1f,
                lineHeight = 11,
                ascentLine = CapHeight + 2,
                capLine = CapHeight,
                meanLine = 5,
                baseline = 0,
                descentLine = -2,
                underlineOffset = -2,
                underlineThickness = 1,
                strikethroughOffset = 3,
                strikethroughThickness = 1,
                superscriptSize = 0.5f,
                subscriptSize = 0.5f,
                tabWidth = 16,
            };
            Set(font, "version", "1.1.0");
            Set(font, "atlasPopulationMode", AtlasPopulationMode.Static);
            Set(font, "atlasRenderMode", GlyphRenderMode.RASTER);
            Set(font, "atlasWidth", AtlasSize);
            Set(font, "atlasHeight", AtlasSize);
            Set(font, "atlasPadding", 1);
            Set(font, "glyphTable", glyphs);
            Set(font, "characterTable", characters);
            font.atlasTextures = new[] { atlas };

            var shader = Shader.Find("TextMeshPro/Bitmap");
            if (shader == null) throw new InvalidOperationException("Falta el shader TextMeshPro/Bitmap: importa TMP Essential Resources.");
            var material = new Material(shader) { name = "BarrelPixel Material" };
            material.SetTexture("_MainTex", atlas);
            font.material = material;

            Directory.CreateDirectory(Path.GetDirectoryName(FontPath));
            AssetDatabase.DeleteAsset(FontPath);
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(material, font);
            font.ReadFontAssetDefinition();
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontPath);

            var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Debug.Log($"[PixelFontBuilder] BarrelPixel: {saved.glyphTable.Count} glyphs, {saved.characterTable.Count} characters.");
            return saved;
        }

        /// <summary>Asigna propiedades con setter interno de TMP_FontAsset/TMP_Asset.</summary>
        static void Set(object target, string property, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var p = t.GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                var setter = p?.GetSetMethod(true);
                if (setter != null)
                {
                    setter.Invoke(target, new[] { value });
                    return;
                }
            }
            throw new MissingMemberException(target.GetType().Name, property);
        }
    }
}
