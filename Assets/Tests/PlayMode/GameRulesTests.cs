#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using BarrelAscent.Enemies;
using BarrelAscent.Environment;
using BarrelAscent.Managers;
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
    /// Reglas de partida sobre GameLevel: coleccionables, puntuación, vidas, reaparición,
    /// progresión por zonas, meta, pausa, derrota y reinicio.
    /// Nota: con Time.timeScale = 0 (pausa / fin) se espera por fotogramas, no por pasos físicos.
    /// </summary>
    public class GameRulesTests : InputTestFixture
    {
        const string ActionsPath = "Assets/Settings/BarrelAscentControls.inputactions";

        Keyboard keyboard;
        GameManager gm;
        LevelManager lm;
        PlayerController player;
        PlayerCollision playerCollision;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        [UnityTearDown]
        public IEnumerator UnloadLevel()
        {
            Time.timeScale = 1f;
            var level = SceneManager.GetSceneByName("GameLevel");
            if (!level.IsValid() || !level.isLoaded) yield break;
            var empty = SceneManager.CreateScene("EmptyRulesScene_" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(level);
        }

        IEnumerator LoadLevel(bool withBarrels = false)
        {
            SceneManager.LoadScene("GameLevel");
            yield return null;
            yield return new WaitForFixedUpdate();
            Acquire(withBarrels);
            yield return WaitSeconds(0.3f);
        }

        void Acquire(bool withBarrels)
        {
            gm = Object.FindFirstObjectByType<GameManager>();
            lm = Object.FindFirstObjectByType<LevelManager>();
            player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(gm, "GameLevel debe tener GameManager");
            Assert.IsNotNull(lm);
            playerCollision = player.GetComponent<PlayerCollision>();
            var actions = ScriptableObject.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            player.SetInputActions(actions);
            gm.SetInputActions(actions);
            if (!withBarrels)
            {
                foreach (var s in Object.FindObjectsByType<BarrelSpawner>(FindObjectsSortMode.None))
                {
                    s.SetSpawning(false);
                    s.enabled = false; // LevelManager puede reactivarlos; deshabilitados no generan
                }
            }
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

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator PlaceAt(Vector3 feet)
        {
            player.Teleport(new Vector2(feet.x, feet.y + 0.05f));
            yield return WaitSeconds(0.3f);
        }

        IEnumerator PressKey(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            Press(key);
            yield return Frames(2);
            Release(key);
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator StartsWithThreeLives_ZeroScore_Playing()
        {
            yield return LoadLevel();
            Assert.AreEqual(3, gm.Lives);
            Assert.AreEqual(0, gm.Score);
            Assert.AreEqual(GameManager.GameState.Playing, gm.State);
            Assert.AreEqual(10, Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None).Length, "Un coleccionable por marcador C");
            Assert.IsNotNull(Object.FindFirstObjectByType<Goal>(), "La meta debe existir");
        }

        [UnityTest]
        public IEnumerator Collectible_Adds100_Once_AndDisappears()
        {
            yield return LoadLevel();
            int changes = 0;
            gm.ScoreChanged += _ => changes++;

            // Coleccionable del suelo (fila 1, columna 9): caminar hasta él.
            var c = Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None).OrderBy(x => x.transform.position.y).First();
            yield return PlaceAt(new Vector3(c.transform.position.x - 2f, 1f));
            Press(keyboard.dKey);
            yield return WaitSeconds(0.8f);
            Release(keyboard.dKey);

            Assert.AreEqual(100, gm.Score);
            Assert.AreEqual(1, changes, "El HUD recibe un único aviso");
            Assert.IsTrue(c == null || c.IsCollected);
            Assert.IsFalse(c != null && c.Collect(), "No se puede recoger dos veces");
            yield return WaitSeconds(0.4f);
            Assert.IsTrue(c == null, "Desaparece");
            Assert.AreEqual(100, gm.Score);
        }

        [UnityTest]
        public IEnumerator StompingABarrel_Adds50()
        {
            yield return LoadLevel();
            var spawner = GameObject.Find("BarrelSpawn_Z1").GetComponent<BarrelSpawner>();
            var barrel = spawner.Spawn();
            barrel.Launch(0f, 1); // quieto
            // En el suelo de la zona 1, para que el salto no cruce a la zona 2 (+250).
            var groundPos = new Vector3(-5f, 1f + barrel.Radius + 0.02f, 0f);
            barrel.transform.position = groundPos;
            barrel.Body.position = groundPos;
            yield return WaitSeconds(0.5f);
            yield return PlaceAt(new Vector3(barrel.transform.position.x, barrel.transform.position.y + 2f));
            yield return WaitSeconds(0.6f);
            Assert.AreEqual(50, gm.Score);
            Assert.AreEqual(3, gm.Lives, "Aterrizar encima no quita vidas");
        }

        [UnityTest]
        public IEnumerator Damage_LosesOneLife_ThenGameOver_AfterThree()
        {
            yield return LoadLevel();
            int livesEvents = 0;
            gm.LivesChanged += _ => livesEvents++;

            Assert.IsTrue(playerCollision.TakeHit(player.transform.position.x - 1f));
            Assert.IsFalse(playerCollision.TakeHit(player.transform.position.x - 1f), "Invulnerable: sin daño repetido");
            Assert.AreEqual(2, gm.Lives);

            yield return WaitSeconds(1.6f);
            Assert.IsTrue(playerCollision.TakeHit(player.transform.position.x - 1f));
            Assert.AreEqual(1, gm.Lives);
            Assert.AreEqual(GameManager.GameState.Playing, gm.State);

            yield return WaitSeconds(1.6f);
            Assert.IsTrue(playerCollision.TakeHit(player.transform.position.x - 1f));
            Assert.AreEqual(0, gm.Lives);
            Assert.AreEqual(3, livesEvents);
            Assert.AreEqual(GameManager.GameState.Lost, gm.State, "Sin vidas: Game Over");
            Assert.AreEqual(0f, Time.timeScale, "La partida se detiene");
            Assert.IsFalse(player.ControlEnabled);
            Assert.IsFalse(Object.FindObjectsByType<BarrelSpawner>(FindObjectsSortMode.None).Any(s => s.IsSpawning));
        }

        [UnityTest]
        public IEnumerator FallingOutOfTheLevel_LosesLife_AndRespawnsAtCheckpoint()
        {
            yield return LoadLevel();
            // Alcanzar la zona 2 fija la reaparición en su primer punto seguro.
            var respawnZ2 = GameObject.Find("Respawn_Z2").transform.position;
            yield return PlaceAt(respawnZ2);
            Assert.AreEqual(1, lm.HighestZoneReached);

            player.Teleport(new Vector2(0f, -4f)); // por debajo del nivel
            yield return WaitSeconds(0.3f);
            Assert.AreEqual(2, gm.Lives);
            Assert.AreEqual(respawnZ2.x, player.transform.position.x, 0.05f);
            Assert.AreEqual(respawnZ2.y, player.transform.position.y, 0.15f, "Reaparece en el punto seguro de la zona alcanzada");
            Assert.IsTrue(playerCollision.IsInvulnerable, "Invulnerable al reaparecer");
        }

        [UnityTest]
        public IEnumerator ZoneProgression_Awards250_OncePerZone()
        {
            yield return LoadLevel();
            yield return PlaceAt(GameObject.Find("Respawn_Z2").transform.position);
            Assert.AreEqual(250, gm.Score, "Completar la zona 1");

            yield return PlaceAt(GameObject.Find("PlayerSpawn").transform.position);
            yield return PlaceAt(GameObject.Find("Respawn_Z2").transform.position);
            Assert.AreEqual(250, gm.Score, "Volver a entrar no repite la recompensa");

            yield return PlaceAt(GameObject.Find("Respawn_Z3").transform.position);
            Assert.AreEqual(500, gm.Score, "Completar la zona 2");
            Assert.AreEqual(2, lm.CurrentZone);
        }

        [UnityTest]
        public IEnumerator ReachingTheGoal_Wins_WithTopBonus_AndStopsTheGame()
        {
            yield return LoadLevel();
            var goal = Object.FindFirstObjectByType<Goal>().transform.position;
            yield return PlaceAt(GameObject.Find("Respawn_Z3").transform.position); // +250 (zona 2) +250 (zona 1)
            // A 1 u de la meta: lejos del coleccionable de la columna 6 (x = −3.5) para no sumar sus 100.
            player.Teleport(new Vector2(goal.x - 1.0f, goal.y + 0.05f));
            yield return WaitSeconds(0.2f);
            Press(keyboard.dKey);
            float end = Time.realtimeSinceStartup + 2f;
            while (gm.State == GameManager.GameState.Playing && Time.realtimeSinceStartup < end) yield return null;
            Release(keyboard.dKey);

            Assert.AreEqual(GameManager.GameState.Won, gm.State, "Tocar la meta da la victoria");
            Assert.AreEqual(250 + 250 + 250 + 1000, gm.Score, "Zonas 1–3 + cima");
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(player.ControlEnabled, "Se detiene el movimiento");
            Assert.IsFalse(Object.FindObjectsByType<BarrelSpawner>(FindObjectsSortMode.None).Any(s => s.IsSpawning));

            int score = gm.Score;
            lm.ReachGoal();
            Assert.AreEqual(score, gm.Score, "La meta no puntúa dos veces");
        }

        [UnityTest]
        public IEnumerator Escape_PausesAndResumes()
        {
            yield return LoadLevel();
            yield return PressKey(keyboard.escapeKey);
            Assert.AreEqual(GameManager.GameState.Paused, gm.State);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(player.ControlEnabled);

            float y = player.transform.position.y;
            Press(keyboard.spaceKey);
            yield return Frames(10);
            Release(keyboard.spaceKey);
            Assert.AreEqual(y, player.transform.position.y, 1e-4f, "En pausa no se mueve");

            yield return PressKey(keyboard.escapeKey);
            Assert.AreEqual(GameManager.GameState.Playing, gm.State);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(player.ControlEnabled);
        }

        [UnityTest]
        public IEnumerator R_RestartsOnlyAfterTheGameEnds()
        {
            yield return LoadLevel();
            gm.AddScore(100);
            yield return PressKey(keyboard.rKey);
            Assert.AreEqual(100, gm.Score, "Durante la partida R no reinicia");

            gm.Lose();
            Assert.AreEqual(GameManager.GameState.Lost, gm.State);
            var oldGm = gm;
            yield return PressKey(keyboard.rKey);
            yield return Frames(3);

            Acquire(withBarrels: false);
            Assert.AreNotSame(oldGm, gm, "La escena se recargó");
            Assert.AreEqual(0, gm.Score);
            Assert.AreEqual(3, gm.Lives);
            Assert.AreEqual(GameManager.GameState.Playing, gm.State);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
#endif
