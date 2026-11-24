using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BarrelAscent.EditorTools
{
    /// <summary>
    /// Fase 8: build independiente para macOS (Intel + Apple silicon, Mono).
    /// Menú: Barrel Ascent/Build/macOS.
    /// Batchmode: -executeMethod BarrelAscent.EditorTools.BuildScript.BuildMacOS
    /// Resultado: Builds/macOS/Barrel Ascent.app (la carpeta Builds/ está en .gitignore).
    /// En batchmode sale con código 1 si el build falla.
    /// </summary>
    public static class BuildScript
    {
        public const string ProductName = "Barrel Ascent";
        public const string OutputDir = "Builds/macOS";

        [MenuItem("Barrel Ascent/Build/macOS")]
        public static void BuildMacOS()
        {
            ConfigurePlayer();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputDir, ProductName + ".app"),
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            Directory.CreateDirectory(OutputDir);
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[BuildScript] {summary.result}: {summary.outputPath}, {summary.totalSize / (1024f * 1024f):0.0} MB, " +
                      $"{summary.totalErrors} errores, {summary.totalWarnings} avisos, {summary.totalTime.TotalSeconds:0} s");

            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        /// <summary>Ajustes del reproductor: nombre, identificador, arquitectura y ventana.</summary>
        static void ConfigurePlayer()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = ProductName;
            PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.barrelascent.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.x64ARM64;

            // Pantalla completa en ventana; si el jugador sale de ella, ventana 16:9 redimensionable
            // (1280×720 = ×2 exacto de la resolución de referencia 640×360).
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.visibleInBackground = false;
            AssetDatabase.SaveAssets();
        }
    }
}
