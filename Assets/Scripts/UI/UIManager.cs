using System.Collections;
using BarrelAscent.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BarrelAscent.UI
{
    /// <summary>
    /// Interfaz de GameLevel: HUD (puntuación, vidas, zona), aviso de zona y paneles de pausa,
    /// Game Over y victoria. Solo escucha eventos del GameManager/LevelManager y llama a sus métodos
    /// públicos desde los botones; no contiene reglas de juego.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] GameManager gameManager;
        [SerializeField] LevelManager levelManager;

        [Header("HUD")]
        [SerializeField] TMP_Text scoreText;
        [SerializeField] Image[] hearts;
        [SerializeField] Color heartLostColor = new Color(0.2f, 0.25f, 0.3f, 0.6f);
        [SerializeField] TMP_Text zoneText;
        [SerializeField] Button pauseButton;

        [Header("Aviso de zona")]
        [SerializeField] CanvasGroup zoneBanner;
        [SerializeField] TMP_Text zoneBannerText;
        [SerializeField] float bannerDuration = 2f;
        [SerializeField] string[] zoneNames = { "ZONA 1 · BASE INDUSTRIAL", "ZONA 2 · MAQUINARIA", "ZONA 3 · LA CIMA" };

        [Header("Paneles")]
        [SerializeField] GameObject pausePanel;
        [SerializeField] Selectable pauseFirstSelected;
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] TMP_Text gameOverScoreText;
        [SerializeField] Selectable gameOverFirstSelected;
        [SerializeField] GameObject victoryPanel;
        [SerializeField] TMP_Text victoryScoreText;
        [SerializeField] Selectable victoryFirstSelected;

        Coroutine bannerRoutine;
        bool started;

        public GameObject PausePanel => pausePanel;
        public GameObject GameOverPanel => gameOverPanel;
        public GameObject VictoryPanel => victoryPanel;
        public TMP_Text ScoreText => scoreText;

        void Start()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            Subscribe(true);
            started = true;

            HideAllPanels();
            if (zoneBanner != null) zoneBanner.alpha = 0f;
            if (gameManager != null)
            {
                OnScoreChanged(gameManager.Score);
                OnLivesChanged(gameManager.Lives);
            }
            if (levelManager != null && levelManager.CurrentZone >= 0) SetZoneLabel(levelManager.CurrentZone);
        }

        void OnEnable()
        {
            if (started) Subscribe(true);
        }

        void OnDisable()
        {
            Subscribe(false);
        }

        void Subscribe(bool on)
        {
            if (gameManager != null)
            {
                gameManager.ScoreChanged -= OnScoreChanged;
                gameManager.LivesChanged -= OnLivesChanged;
                gameManager.StateChanged -= OnStateChanged;
                if (on)
                {
                    gameManager.ScoreChanged += OnScoreChanged;
                    gameManager.LivesChanged += OnLivesChanged;
                    gameManager.StateChanged += OnStateChanged;
                }
            }
            if (levelManager != null)
            {
                levelManager.ZoneChanged -= OnZoneChanged;
                if (on) levelManager.ZoneChanged += OnZoneChanged;
            }
        }

        // ---------- HUD ----------

        static string FormatScore(int score) => Mathf.Clamp(score, 0, 999999).ToString("D6");

        void OnScoreChanged(int score)
        {
            if (scoreText != null) scoreText.text = FormatScore(score);
        }

        void OnLivesChanged(int lives)
        {
            for (int i = 0; i < hearts.Length; i++)
                if (hearts[i] != null) hearts[i].color = i < lives ? Color.white : heartLostColor;
        }

        void OnZoneChanged(int zone)
        {
            SetZoneLabel(zone);
            if (zoneBanner == null || zoneBannerText == null) return;
            zoneBannerText.text = ZoneName(zone);
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(ShowBanner());
        }

        void SetZoneLabel(int zone)
        {
            if (zoneText != null) zoneText.text = $"ZONA {zone + 1}";
        }

        string ZoneName(int zone) => zone >= 0 && zone < zoneNames.Length ? zoneNames[zone] : $"ZONA {zone + 1}";

        IEnumerator ShowBanner()
        {
            zoneBanner.alpha = 1f;
            yield return new WaitForSecondsRealtime(bannerDuration);
            const float fade = 0.4f;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                zoneBanner.alpha = 1f - t / fade;
                yield return null;
            }
            zoneBanner.alpha = 0f;
            bannerRoutine = null;
        }

        // ---------- Paneles ----------

        void OnStateChanged(GameManager.GameState state)
        {
            HideAllPanels();
            switch (state)
            {
                case GameManager.GameState.Paused:
                    Show(pausePanel, pauseFirstSelected);
                    break;
                case GameManager.GameState.Lost:
                    if (gameOverScoreText != null) gameOverScoreText.text = FormatScore(gameManager.Score);
                    Show(gameOverPanel, gameOverFirstSelected);
                    break;
                case GameManager.GameState.Won:
                    if (victoryScoreText != null) victoryScoreText.text = FormatScore(gameManager.Score);
                    Show(victoryPanel, victoryFirstSelected);
                    break;
                default:
                    Select(null); // de vuelta al juego: que Espacio no pulse ningún botón
                    break;
            }
            if (pauseButton != null) pauseButton.interactable = state == GameManager.GameState.Playing;
        }

        void HideAllPanels()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
        }

        static void Show(GameObject panel, Selectable first)
        {
            if (panel == null) return;
            panel.SetActive(true);
            Select(first != null ? first.gameObject : null);
        }

        static void Select(GameObject go)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
        }

        // ---------- Botones (conectados en el Inspector) ----------

        public void OnPauseButton() => gameManager?.Pause();
        public void OnResumeButton() => gameManager?.Resume();
        public void OnRestartButton() => gameManager?.Restart();
        public void OnMainMenuButton() => gameManager?.LoadMainMenu();
    }
}
