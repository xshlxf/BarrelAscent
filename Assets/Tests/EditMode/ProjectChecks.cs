using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BarrelAscent.Tests
{
    /// <summary>
    /// Comprobaciones estáticas del proyecto antes de compilar el juego:
    /// escenas del build, que no quede arte placeholder en uso, ajustes de importación pixel art
    /// y que todos los estados del Animator del jugador tengan animación.
    /// </summary>
    public class ProjectChecks
    {
        const string MainMenu = "Assets/Scenes/MainMenu/MainMenu.unity";
        const string GameLevel = "Assets/Scenes/GameLevel/GameLevel.unity";

        [Test]
        public void BuildSettings_StartWithMainMenu_ThenGameLevel()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            CollectionAssert.AreEqual(new[] { MainMenu, GameLevel }, scenes);
        }

        [Test]
        public void Scenes_DoNotUsePlaceholderArt()
        {
            var placeholders = AssetDatabase.GetDependencies(new[] { MainMenu, GameLevel }, true)
                .Where(p => p.Contains("/Placeholders/")).ToArray();
            CollectionAssert.IsEmpty(placeholders, "Arte placeholder aún en uso: " + string.Join(", ", placeholders));
        }

        [Test]
        public void PixelArtTextures_Are32Ppu_Point_Uncompressed_WithoutMipmaps()
        {
            var folders = new[] { "Assets/Sprites", "Assets/UI/Sprites", "Assets/Tilemaps" };
            var bad = AssetDatabase.FindAssets("t:Texture2D", folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => (path: p, imp: AssetImporter.GetAtPath(p) as TextureImporter))
                .Where(t => t.imp != null && (t.imp.spritePixelsPerUnit != 32 || t.imp.filterMode != FilterMode.Point ||
                                              t.imp.textureCompression != TextureImporterCompression.Uncompressed ||
                                              t.imp.mipmapEnabled))
                .Select(t => t.path).ToArray();
            CollectionAssert.IsEmpty(bad, "Texturas con importación incorrecta: " + string.Join(", ", bad));
        }

        [Test]
        public void PlayerAnimator_EveryStateHasAMotion()
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/Player.controller");
            Assert.IsNotNull(ac);
            var states = ac.layers[0].stateMachine.states.Select(s => s.state).ToArray();
            CollectionAssert.IsSupersetOf(states.Select(s => s.name).ToArray(), new[] { "Idle", "Run", "Jump", "Climb", "Hurt" });
            foreach (var s in states) Assert.IsNotNull(s.motion, $"El estado {s.name} no tiene animación");
        }
    }
}
