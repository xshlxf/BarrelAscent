#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using BarrelAscent.Environment;
using BarrelAscent.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BarrelAscent.Tests
{
    /// <summary>
    /// Pruebas sobre la escena real GameLevel: colisiones del Tilemap, todas las escaleras,
    /// los saltos obligatorios del recorrido y la cámara vertical. Juntas demuestran que las tres zonas
    /// son accesibles desde el inicio hasta la meta.
    /// </summary>
    public class GameLevelTests : InputTestFixture
    {
        const string ActionsPath = "Assets/Settings/BarrelAscentControls.inputactions";

        Keyboard keyboard;
        PlayerController player;
        CameraFollow cameraFollow;
        Camera cam;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        /// <param name="withBarrels">false: se detienen los spawners para probar solo el escenario.</param>
        IEnumerator LoadLevel(bool withBarrels = false)
        {
            SceneManager.LoadScene("GameLevel");
            yield return null;
            yield return new WaitForFixedUpdate();

            // Estas pruebas cubren solo escenario, cámara y barriles: sin reglas de partida
            // (la meta daría la victoria y congelaría el tiempo, y los golpes restarían vidas).
            var gameManager = Object.FindFirstObjectByType<BarrelAscent.Managers.GameManager>();
            if (gameManager != null) gameManager.enabled = false;
            foreach (var goal in Object.FindObjectsByType<Goal>(FindObjectsSortMode.None))
                goal.GetComponent<Collider2D>().enabled = false;

            if (!withBarrels)
            {
                var lm = Object.FindFirstObjectByType<BarrelAscent.Managers.LevelManager>();
                if (lm != null)
                {
                    lm.StopAllSpawners();
                    lm.enabled = false;
                }
            }

            player = Object.FindFirstObjectByType<PlayerController>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow>();
            cam = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : null;
            Assert.IsNotNull(player, "GameLevel debe contener el jugador");
            Assert.IsNotNull(cameraFollow, "La Main Camera debe tener CameraFollow");

            var actions = ScriptableObject.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            player.SetInputActions(actions);
            yield return WaitSeconds(0.3f);
        }

        /// <summary>Descarga GameLevel para que no interfiera con otras pruebas.</summary>
        [UnityTearDown]
        public IEnumerator UnloadLevel()
        {
            var level = SceneManager.GetSceneByName("GameLevel");
            if (!level.IsValid() || !level.isLoaded) yield break;
            var empty = SceneManager.CreateScene("EmptyTestScene_" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(level);
        }

        static IEnumerator WaitSeconds(float s)
        {
            float end = Time.time + s;
            while (Time.time < end)
            {
                if (Time.timeScale == 0f) Assert.Fail("El tiempo está detenido (timeScale = 0): la espera no terminaría.");
                yield return new WaitForFixedUpdate();
            }
        }

        float Feet => player.GetComponent<CapsuleCollider2D>().bounds.min.y;
        float X => player.transform.position.x;

        static Vector3 Marker(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, $"Falta el marcador {name}");
            return go.transform.position;
        }

        IEnumerator PlaceAt(float x, float feetY)
        {
            player.Teleport(new Vector2(x, feetY + 0.05f));
            yield return WaitSeconds(0.25f);
        }

        [UnityTest]
        public IEnumerator Spawn_StandsOnTilemapFloor_AndCameraStartsAtBottom()
        {
            yield return LoadLevel();
            Assert.IsTrue(player.IsGrounded, "El jugador debe apoyarse en el Tilemap");
            Assert.AreEqual(Marker("PlayerSpawn").y, Feet, 0.05f);
            Assert.AreEqual(cameraFollow.MinY + cam.orthographicSize, cam.transform.position.y, 0.05f,
                "La cámara no debe mostrar nada por debajo del suelo");
        }

        [UnityTest]
        public IEnumerator EveryLadder_CanBeClimbedToItsPlatform()
        {
            yield return LoadLevel();
            var ladders = Object.FindObjectsByType<Ladder>(FindObjectsSortMode.None).OrderBy(l => l.BottomY).ToArray();
            Assert.GreaterOrEqual(ladders.Length, 10);

            foreach (var ladder in ladders)
            {
                yield return PlaceAt(ladder.CenterX, ladder.BottomY);
                Assert.IsTrue(player.IsGrounded, $"{ladder.name}: no hay suelo en la base");

                Press(keyboard.wKey);
                float timeout = Time.time + 4f;
                yield return WaitSeconds(0.2f);
                Assert.IsTrue(player.IsClimbing, $"{ladder.name}: W no inicia la escalada");
                while (player.IsClimbing && Time.time < timeout) yield return new WaitForFixedUpdate();
                Release(keyboard.wKey);
                yield return WaitSeconds(0.3f);

                Assert.IsFalse(player.IsClimbing, $"{ladder.name}: no termina la escalada");
                Assert.AreEqual(ladder.TopY, Feet, 0.08f, $"{ladder.name}: no queda sobre la plataforma superior");
                Assert.IsTrue(player.IsGrounded, $"{ladder.name}: no queda apoyado arriba");
            }
        }

        // Saltos obligatorios del recorrido (coordenadas de LevelLayout.txt: x = columna - 10 + 0.5).
        static readonly (string name, float startX, float feetY, int dir, float minX, float maxX, float landFeetY)[] RequiredJumps =
        {
            ("Z2 F4 escalón",         -0.5f, 17f, +1,  1f, 10f, 18f),
            ("Z2 F5 hueco",            1.5f, 21f, -1, -10f, -1f, 21f),
            ("Z3 F8 hueco izquierdo", -3.5f, 33f, +1, -1f, 4f, 33f),
            ("Z3 F8 hueco derecho",    3.5f, 33f, +1,  6f, 10f, 33f),
            ("Z3 F9 hueco",            2.5f, 37f, -1, -6f, 0f, 37f),
        };

        [UnityTest]
        public IEnumerator RequiredJumps_LandOnTheNextPlatform()
        {
            yield return LoadLevel();
            foreach (var j in RequiredJumps)
            {
                yield return PlaceAt(j.startX, j.feetY);
                Assert.IsTrue(player.IsGrounded, $"{j.name}: punto de salida sin suelo");

                var key = j.dir > 0 ? keyboard.dKey : keyboard.aKey;
                Press(key);
                Press(keyboard.spaceKey);
                yield return WaitSeconds(0.1f);
                Release(keyboard.spaceKey);
                yield return WaitSeconds(0.55f);
                Release(key);
                yield return WaitSeconds(0.6f);

                Assert.IsTrue(player.IsGrounded, $"{j.name}: no aterriza");
                Assert.AreEqual(j.landFeetY, Feet, 0.08f, $"{j.name}: aterriza a una altura inesperada");
                Assert.That(X, Is.InRange(j.minX, j.maxX), $"{j.name}: aterriza fuera de la plataforma destino");
            }
        }

        [UnityTest]
        public IEnumerator Camera_FollowsVertically_AndClampsAtTop()
        {
            yield return LoadLevel();
            float half = cam.orthographicSize;

            Vector3 goal = Marker("Goal");
            yield return PlaceAt(goal.x, goal.y);
            yield return WaitSeconds(1.5f);
            Assert.AreEqual(cameraFollow.MaxY - half, cam.transform.position.y, 0.05f, "Debe detenerse en el límite superior");
            Assert.That(player.transform.position.y, Is.InRange(cam.transform.position.y - half, cam.transform.position.y + half),
                "La meta debe quedar dentro del encuadre");
            Assert.AreEqual(0f, cam.transform.position.x, 0.01f, "La X de la cámara es fija");

            yield return PlaceAt(-8.5f, 13f); // F3, columna 1 (pies en y = 13)
            yield return WaitSeconds(1.5f);
            Assert.AreEqual(13f + 1.5f, cam.transform.position.y, 0.1f, "En zonas intermedias sigue al jugador con el desplazamiento configurado");
        }

        [UnityTest]
        public IEnumerator LevelManager_ActivatesOnlyTheSpawnerOfThePlayersZone()
        {
            yield return LoadLevel(withBarrels: true);
            var lm = Object.FindFirstObjectByType<BarrelAscent.Managers.LevelManager>();
            Assert.IsNotNull(lm, "GameLevel debe tener LevelManager");
            var spawners = new[]
            {
                GameObject.Find("BarrelSpawn_Z1").GetComponent<BarrelAscent.Enemies.BarrelSpawner>(),
                GameObject.Find("BarrelSpawn_Z2").GetComponent<BarrelAscent.Enemies.BarrelSpawner>(),
                GameObject.Find("BarrelSpawn_Z3").GetComponent<BarrelAscent.Enemies.BarrelSpawner>(),
            };

            Assert.AreEqual(0, lm.CurrentZone);
            CollectionAssert.AreEqual(new[] { true, false, false }, spawners.Select(s => s.IsSpawning).ToArray());

            yield return PlaceAt(-8.5f, 29f); // F7, zona 2
            Assert.AreEqual(1, lm.CurrentZone);
            CollectionAssert.AreEqual(new[] { false, true, false }, spawners.Select(s => s.IsSpawning).ToArray());

            yield return PlaceAt(-1.5f, 41f); // F10, zona 3
            Assert.AreEqual(2, lm.CurrentZone);
            CollectionAssert.AreEqual(new[] { false, false, true }, spawners.Select(s => s.IsSpawning).ToArray());

            // Dificultad progresiva: más rápido y más frecuente en cada zona.
            for (int i = 1; i < 3; i++)
            {
                Assert.Greater(spawners[i].Zone.barrelSpeed, spawners[i - 1].Zone.barrelSpeed);
                Assert.Less(spawners[i].Zone.spawnInterval, spawners[i - 1].Zone.spawnInterval);
            }
        }

        [UnityTest]
        public IEnumerator Barrels_FromEachZone_ZigzagDownThroughTheLevel()
        {
            yield return LoadLevel();
            var lm = Object.FindFirstObjectByType<BarrelAscent.Managers.LevelManager>();
            lm.StopAllSpawners();
            lm.enabled = false;
            player.gameObject.SetActive(false); // sin interferencias del jugador

            for (int z = 1; z <= 3; z++)
            {
                var spawner = GameObject.Find($"BarrelSpawn_Z{z}").GetComponent<BarrelAscent.Enemies.BarrelSpawner>();
                var barrel = spawner.Spawn();
                float startY = barrel.transform.position.y;
                float minY = startY;
                int falls = 0;
                var last = barrel.CurrentState;
                float end = Time.time + 75f; // el recorrido completo de la zona 2 dura ~45 s
                while (barrel != null && Time.time < end)
                {
                    minY = Mathf.Min(minY, barrel.transform.position.y);
                    if (barrel.CurrentState != last && barrel.CurrentState == BarrelAscent.Enemies.BarrelController.State.Falling) falls++;
                    last = barrel.CurrentState;
                    yield return new WaitForFixedUpdate();
                }

                Assert.GreaterOrEqual(falls, 2, $"Zona {z}: el barril debe caer al menos dos pisos");
                Assert.Less(minY, startY - 7.5f, $"Zona {z}: el barril debe descender por el nivel");
                Assert.IsTrue(barrel == null, $"Zona {z}: el barril debe terminar su recorrido y desaparecer (sin atascarse)");
            }
        }

        [UnityTest]
        public IEnumerator Level_HasThreeZones_ThreeBarrelSpawns_AndAGoal()
        {
            yield return LoadLevel();
            float z2 = Marker("ZoneStart_2").y, z3 = Marker("ZoneStart_3").y;
            Assert.Less(0f, z2);
            Assert.Less(z2, z3);
            Assert.Less(Marker("BarrelSpawn_Z1").y, z2);
            Assert.That(Marker("BarrelSpawn_Z2").y, Is.InRange(z2, z3));
            Assert.Greater(Marker("BarrelSpawn_Z3").y, z3);
            Assert.Greater(Marker("Goal").y, z3);
        }
    }
}
#endif
