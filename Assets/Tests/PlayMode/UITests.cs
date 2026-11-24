#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using BarrelAscent.Enemies;
using BarrelAscent.Managers;
using BarrelAscent.Player;
using BarrelAscent.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BarrelAscent.Tests
{
    /// <summary>Pruebas de la Fase 6: menú principal, HUD y paneles de pausa, Game Over y victoria.</summary>
    public class UITests : InputTestFixture
    {
        const string ActionsPath = "Assets/Settings/BarrelAscentControls.inputactions";

        Keyboard keyboard;
        GameManager gm;
        LevelManager lm;
        UIManager ui;
        PlayerController player;

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
        public IEnumerator UnloadScenes()
        {
            Time.timeScale = 1f;
            var empty = SceneManager.CreateScene("EmptyUiScene_" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != empty && (s.name == "GameLevel" || s.name == "MainMenu"))
                    yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        IEnumerator LoadGameLevel()
        {
            SceneManager.LoadScene("GameLevel");
            yield return null;
            yield return null;
            Acquire();
        }

        void Acquire()
        {
            gm = Object.FindFirstObjectByType<GameManager>();
            lm = Object.FindFirstObjectByType<LevelManager>();
            ui = Object.FindFirstObjectByType<UIManager>();
            player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(ui, "GameLevel debe tener UIManager");
            var actions = ScriptableObject.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            player.SetInputActions(actions);
            gm.SetInputActions(actions);
            foreach (var s in Object.FindObjectsByType<BarrelSpawner>(FindObjectsSortMode.None)) s.enabled = false;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator PressKey(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            Press(key);
            yield return Frames(2);
            Release(key);
            yield return Frames(2);
        }

        static Button FindButton(GameObject root, string name) =>
            root.GetComponentsInChildren<Button>(true).First(b => b.name == name);

        int LitHearts() =>
            Object.FindObjectsByType<Image>(FindObjectsSortMode.None).Count(i => i.name.StartsWith("Heart") && i.color == Color.white);

        [UnityTest]
        public IEnumerator AllUiTexts_UseThePixelFont_AndHaveEveryGlyph()
        {
            foreach (var scene in new[] { "MainMenu", "GameLevel" })
            {
                SceneManager.LoadScene(scene);
                yield return Frames(2);
                foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>().Where(t => t.gameObject.scene.name == scene))
                {
                    Assert.AreEqual("BarrelPixel", t.font.name, $"{scene}/{t.name} debe usar la fuente pixel");
                    bool ok = t.font.HasCharacters(t.text, out System.Collections.Generic.List<char> missing);
                    Assert.IsTrue(ok, $"{scene}/{t.name}: faltan glifos para '{(missing != null ? new string(missing.ToArray()) : "")}'");
                }
            }
        }

        [UnityTest]
        public IEnumerator MainMenu_HasTitle_PlayLoadsGameLevel_ExitIsWired()
        {
            SceneManager.LoadScene("MainMenu");
            yield return Frames(2);
            var canvas = Object.FindFirstObjectByType<Canvas>().gameObject;
            Assert.IsTrue(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Any(t => t.text == "BARREL ASCENT"));
            Assert.AreEqual("PlayButton", EventSystem.current.currentSelectedGameObject?.name, "Jugar seleccionado para usar el teclado");

            var exit = FindButton(canvas, "ExitButton");
            Assert.AreEqual(1, exit.onClick.GetPersistentEventCount());
            Assert.AreEqual(nameof(MainMenu.Exit), exit.onClick.GetPersistentMethodName(0));

            FindButton(canvas, "PlayButton").onClick.Invoke();
            yield return Frames(3);
            Assert.AreEqual("GameLevel", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Hud_UpdatesScoreAndLives_Immediately()
        {
            yield return LoadGameLevel();
            Assert.AreEqual("000000", ui.ScoreText.text);
            Assert.AreEqual(3, LitHearts());

            gm.AddScore(150);
            Assert.AreEqual("000150", ui.ScoreText.text, "La puntuación se actualiza en el mismo frame");

            player.GetComponent<PlayerCollision>().TakeHit(player.transform.position.x - 1f);
            Assert.AreEqual(2, LitHearts());
        }

        [UnityTest]
        public IEnumerator Escape_ShowsPausePanel_ResumeButtonHidesIt()
        {
            yield return LoadGameLevel();
            Assert.IsFalse(ui.PausePanel.activeSelf);

            yield return PressKey(keyboard.escapeKey);
            Assert.IsTrue(ui.PausePanel.activeSelf);
            Assert.AreEqual("ResumeButton", EventSystem.current.currentSelectedGameObject?.name);

            FindButton(ui.PausePanel, "ResumeButton").onClick.Invoke();
            yield return Frames(1);
            Assert.IsFalse(ui.PausePanel.activeSelf);
            Assert.AreEqual(GameManager.GameState.Playing, gm.State);
            Assert.IsNull(EventSystem.current.currentSelectedGameObject, "Al volver al juego no queda ningún botón seleccionado");

            FindButton(ui.gameObject, "PauseButton").onClick.Invoke();
            Assert.AreEqual(GameManager.GameState.Paused, gm.State, "El botón de pausa del HUD también pausa");
            Assert.IsTrue(ui.PausePanel.activeSelf);
        }

        [UnityTest]
        public IEnumerator GameOver_ShowsScore_AndRestartButtonReloads()
        {
            yield return LoadGameLevel();
            gm.AddScore(300);
            gm.Lose();
            Assert.IsTrue(ui.GameOverPanel.activeSelf);
            Assert.IsFalse(ui.PausePanel.activeSelf);
            var score = ui.GameOverPanel.GetComponentsInChildren<TMP_Text>().First(t => t.name == "Score");
            Assert.AreEqual("000300", score.text);

            var oldGm = gm;
            FindButton(ui.GameOverPanel, "RestartButton").onClick.Invoke();
            yield return Frames(3);
            Acquire();
            Assert.AreNotSame(oldGm, gm);
            Assert.AreEqual("000000", ui.ScoreText.text);
            Assert.IsFalse(ui.GameOverPanel.activeSelf);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Victory_ShowsFinalScore_AndMenuButtonReturnsToMainMenu()
        {
            yield return LoadGameLevel();
            lm.ReachGoal();
            Assert.IsTrue(ui.VictoryPanel.activeSelf);
            var score = ui.VictoryPanel.GetComponentsInChildren<TMP_Text>().First(t => t.name == "Score");
            Assert.AreEqual(gm.Score.ToString("D6"), score.text);
            Assert.GreaterOrEqual(gm.Score, 1250);

            FindButton(ui.VictoryPanel, "MenuButton").onClick.Invoke();
            yield return Frames(3);
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale, "El tiempo vuelve a la normalidad en el menú");
        }
    }
}
#endif
