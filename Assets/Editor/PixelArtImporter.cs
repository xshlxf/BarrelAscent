using UnityEditor;
using UnityEngine;

namespace BarrelAscent.EditorTools
{
    /// <summary>
    /// Aplica automáticamente la configuración de pixel art a toda textura nueva
    /// dentro de Assets/Sprites, Assets/Tilemaps y Assets/UI:
    /// Sprite, PPU 32, Filter Point, sin compresión, sin mipmaps.
    /// Solo actúa en la primera importación, para no pisar ajustes manuales posteriores.
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        static readonly string[] Folders = { "Assets/Sprites/", "Assets/Tilemaps/", "Assets/UI/" };

        void OnPreprocessTexture()
        {
            if (!IsInPixelArtFolder(assetPath)) return;

            var importer = (TextureImporter)assetImporter;
            if (!importer.importSettingsMissing) return; // solo la primera vez

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = ProjectSetup.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            // FullRect permite usar Draw Mode = Tiled en plataformas y escaleras.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        static bool IsInPixelArtFolder(string path)
        {
            foreach (var f in Folders)
                if (path.StartsWith(f)) return true;
            return false;
        }
    }
}
