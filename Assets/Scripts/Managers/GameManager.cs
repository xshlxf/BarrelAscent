using System;
using BarrelAscent.Enemies;
using BarrelAscent.Environment;
using BarrelAscent.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BarrelAscent.Managers
{
    /// <summary>
    /// Estado general de la partida: puntuación, vidas, pausa, victoria, derrota y reinicio.
    /// Los demás sistemas le notifican y la interfaz escucha sus eventos; nunca toca la UI directamente.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum GameState { Playing, Paused, Won, Lost }

        [Header("Reglas")]
        [SerializeField] int startingLives = 3;
        [SerializeField] int barrelPoints = 50;
        [Tooltip("Invulnerabilidad extra al reaparecer tras caer (s).")]
        [SerializeField] float respawnInvulnerability = 1.5f;
        [Tooltip("Si el jugador cae por debajo de esta altura pierde una vida y reaparece.")]
        [SerializeField] float fallLimitY = -3f;

        [Header("Referencias")]
        [SerializeField] PlayerController player;
        [SerializeField] PlayerCollision playerCollision;
        [SerializeField] LevelManager levelManager;
        [SerializeField] CameraFollow cameraFollow;
        [Tooltip("Opcional. Si está vacío se usan las Project-wide Actions.")]
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] string mainMenuScene = "MainMenu";

        InputAction pauseAction;
        InputAction restartAction;
        Vector3 fallbackRespawn;

        /// <summary>Instancia de la escena actual (null fuera de GameLevel).</summary>
        public static GameManager Instance { get; private set; }

        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int StartingLives => startingLives;
        public GameState State { get; private set; } = GameState.Playing;
        public bool IsGameOver => State == GameState.Won || State == GameState.Lost;

        public event Action<int> ScoreChanged;
        public event Action<int> LivesChanged;
        public event Action<GameState> StateChanged;
        /// <summary>Cada recompensa concedida (cantidad, motivo).</summary>
        public event Action<int, string> PointsAwarded;

        void Awake()
        {
            Instance = this;
            Lives = startingLives;
            Score = 0;
            Time.timeScale = 1f;
            if (player != null) fallbackRespawn = player.transform.position;
        }

        void OnEnable()
        {
            BindActions(inputActions != null ? inputActions : InputSystem.actions);
            if (playerCollision != null)
            {
                playerCollision.Damaged += OnPlayerDamaged;
                playerCollision.BarrelSmashed += OnBarrelSmashed;
            }
        }

        void Update()
        {
            if (pauseAction != null && pauseAction.WasPressedThisFrame()) TogglePause();
            if (restartAction != null && restartAction.WasPressedThisFrame() && IsGameOver) Restart();

            if (State == GameState.Playing && player != null && player.transform.position.y < fallLimitY)
                OnPlayerFell();
        }

        void OnDisable()
        {
            if (playerCollision != null)
            {
                playerCollision.Damaged -= OnPlayerDamaged;
                playerCollision.BarrelSmashed -= OnBarrelSmashed;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }

        /// <summary>Sustituye las acciones de entrada (pruebas automáticas).</summary>
        public void SetInputActions(InputActionAsset asset)
        {
            inputActions = asset;
            BindActions(asset);
        }

        /// <summary>Suma puntos. Solo cuenta durante la partida (no en pausa ni tras terminar).</summary>
        public void AddScore(int amount, string reason = "")
        {
            if (State != GameState.Playing || amount == 0) return;
            Score += amount;
            PointsAwarded?.Invoke(amount, reason);
            ScoreChanged?.Invoke(Score);
        }

        /// <summary>Coloca al jugador en el punto seguro de la zona más alta alcanzada.</summary>
        public void Respawn()
        {
            if (player == null) return;
            Vector3 point = levelManager != null ? levelManager.RespawnPoint : fallbackRespawn;
            player.Teleport(point);
            if (cameraFollow != null) cameraFollow.SnapToTarget();
            if (playerCollision != null) playerCollision.GrantInvulnerability(respawnInvulnerability);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) Pause();
            else if (State == GameState.Paused) Resume();
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            Time.timeScale = 0f;
            if (player != null) player.SetControlEnabled(false);
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            Time.timeScale = 1f;
            if (player != null) player.SetControlEnabled(true);
            SetState(GameState.Playing);
        }

        /// <summary>Victoria: la llama LevelManager al alcanzar la meta.</summary>
        public void Win()
        {
            if (State != GameState.Playing) return;
            EndGame(GameState.Won);
        }

        public void Lose()
        {
            if (State != GameState.Playing) return;
            EndGame(GameState.Lost);
        }

        /// <summary>Recarga GameLevel desde cero.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuScene);
        }

        void BindActions(InputActionAsset asset)
        {
            pauseAction = restartAction = null;
            if (asset == null) return;
            pauseAction = asset.FindAction("Game/Pause", throwIfNotFound: true);
            restartAction = asset.FindAction("Game/Restart", throwIfNotFound: true);
            pauseAction.Enable();
            restartAction.Enable();
        }

        void OnBarrelSmashed(BarrelController barrel) => AddScore(barrelPoints, "Barril destruido");

        void OnPlayerDamaged()
        {
            if (State != GameState.Playing) return;
            LoseLife(respawn: false); // golpe de barril: retroceso + invulnerabilidad, sin teletransporte
        }

        void OnPlayerFell()
        {
            LoseLife(respawn: true);
        }

        void LoseLife(bool respawn)
        {
            Lives = Mathf.Max(0, Lives - 1);
            LivesChanged?.Invoke(Lives);
            if (Lives == 0)
            {
                Lose();
                return;
            }
            if (respawn) Respawn();
        }

        void EndGame(GameState result)
        {
            if (player != null) player.SetControlEnabled(false);
            if (levelManager != null) levelManager.StopAllSpawners();
            Time.timeScale = 0f; // se detiene la partida (barriles y física)
            SetState(result);
        }

        void SetState(GameState next)
        {
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
