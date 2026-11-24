#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using BarrelAscent.Enemies;
using BarrelAscent.Managers;
using BarrelAscent.Player;
using BarrelAscent.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BarrelAscent.Tests
{
    /// <summary>
    /// Pruebas finales: una sesión completa de principio a fin, como la haría un jugador,
    /// y una prueba de resistencia con los barriles activos. Cualquier error o excepción en el log
    /// hace fallar la prueba (comportamiento por defecto de LogAssert).
    /// </summary>
    public class FinalTests : InputTestFixture
    {
        const string ActionsPath = "Assets/Settings/BarrelAscentControls.inputactions";

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            Time.timeScale = 1f;
            var empty = SceneManager.CreateScene("EmptyFinalScene_" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != empty && (s.name == "GameLevel" || s.name == "MainMenu"))
                    yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator FixedSteps(int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (Time.timeScale == 0f) Assert.Fail("El tiempo está detenido (timeScale = 0).");
                yield return new WaitForFixedUpdate();
            }
        }

        static Button FindButton(string name) =>
            Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(b => b.name == name);

        static Vector3 Marker(string name) =>
            (GameObject.Find("Level/Markers/" + name) ?? throw new AssertionException($"Falta el marcador {name}")).transform.position;

        static PlayerController AcquirePlayer(bool disableBarrels)
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            var actions = ScriptableObject.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            player.SetInputActions(actions);
            Object.FindFirstObjectByType<GameManager>().SetInputActions(actions);
            if (disableBarrels)
                foreach (var s in Object.FindObjectsByType<BarrelSpawner>(FindObjectsSortMode.None))
                {
                    s.SetSpawning(false);
                    s.enabled = false;
                }
            return player;
        }

        [UnityTest]
        public IEnumerator FullSession_Menu_Zones_Goal_Victory_Menu_AndReplayStartsFresh()
        {
            SceneManager.LoadScene("MainMenu");
            yield return Frames(2);

            for (int round = 0; round < 2; round++)
            {
                FindButton("PlayButton").onClick.Invoke();
                yield return Frames(3);
                Assert.AreEqual("GameLevel", SceneManager.GetActiveScene().name);

                var gm = Object.FindFirstObjectByType<GameManager>();
                var lm = Object.FindFirstObjectByType<LevelManager>();
                Assert.AreSame(gm, GameManager.Instance, "Cada partida usa su propio GameManager");
                Assert.AreEqual(GameManager.GameState.Playing, gm.State);
                Assert.AreEqual(0, gm.Score, "La puntuación empieza en 0 en cada partida");
                Assert.AreEqual(3, gm.Lives, "Las vidas empiezan en 3 en cada partida");

                var player = AcquirePlayer(disableBarrels: true);
                int zonesCompleted = 0;
                lm.ZoneCompleted += _ => zonesCompleted++;
                yield return FixedSteps(10);

                // Recorre las zonas (puntos de reaparición de las zonas 2 y 3) y llega a la meta física.
                foreach (var marker in new[] { "Respawn_Z2", "Respawn_Z3", "Goal" })
                {
                    var p = Marker(marker);
                    player.Teleport(new Vector2(p.x, p.y + 0.05f));
                    // Al ganar el tiempo se detiene (timeScale = 0): se deja de esperar pasos físicos.
                    for (int i = 0; i < 15 && gm.State == GameManager.GameState.Playing; i++)
                        yield return new WaitForFixedUpdate();
                    if (gm.State == GameManager.GameState.Won) break;
                }

                Assert.AreEqual(GameManager.GameState.Won, gm.State, "Tocar la meta gana la partida");
                Assert.AreEqual(2, lm.HighestZoneReached);
                Assert.GreaterOrEqual(zonesCompleted, 2, "Se completan las zonas 1 y 2 por el camino");
                Assert.GreaterOrEqual(gm.Score, 1000 + 2 * 250);
                var ui = Object.FindFirstObjectByType<UIManager>();
                Assert.IsTrue(ui.VictoryPanel.activeSelf);

                ui.VictoryPanel.GetComponentsInChildren<Button>(true).First(b => b.name == "MenuButton").onClick.Invoke();
                yield return Frames(3);
                Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
                Assert.AreEqual(1f, Time.timeScale);
            }
        }

        [UnityTest]
        public IEnumerator Soak_SixtySecondsOfBarrels_NoErrors_AndBarrelCountStaysBounded()
        {
            SceneManager.LoadScene("GameLevel");
            yield return Frames(2);
            var player = AcquirePlayer(disableBarrels: false);
            player.GetComponent<PlayerCollision>().enabled = false; // el jugador no interfiere: solo barriles

            int maxAlive = Object.FindObjectsByType<BarrelSpawner>(FindObjectsSortMode.None).Sum(s => s.Zone.maxBarrels);
            int peak = 0, destroyed = 0;
            System.Action<BarrelController, bool> onDestroyed = (_, __) => destroyed++;
            BarrelController.AnyDestroyed += onDestroyed;
            try
            {
                Time.timeScale = 3f; // 20 s reales ≈ 60 s de juego
                float end = Time.time + 60f;
                while (Time.time < end)
                {
                    yield return new WaitForFixedUpdate();
                    peak = Mathf.Max(peak, Object.FindObjectsByType<BarrelController>(FindObjectsSortMode.None).Length);
                }
            }
            finally
            {
                Time.timeScale = 1f;
                BarrelController.AnyDestroyed -= onDestroyed;
            }

            Assert.Greater(destroyed, 5, "Los barriles deben aparecer, recorrer el nivel y eliminarse");
            Assert.LessOrEqual(peak, maxAlive + 2, "El número de barriles vivos debe mantenerse acotado");
            Assert.AreEqual(GameManager.GameState.Playing, Object.FindFirstObjectByType<GameManager>().State);
        }
    }
}
#endif
