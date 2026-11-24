using BarrelAscent.Enemies;
using BarrelAscent.Environment;
using BarrelAscent.Player;
using UnityEngine;

namespace BarrelAscent.Managers
{
    /// <summary>
    /// Sonido del juego. Escucha los eventos existentes y reproduce el clip asignado a cada uno.
    /// Todas las ranuras son opcionales: el paquete de recursos no incluye audio, así que un clip
    /// vacío simplemente no suena. Para añadir sonidos basta con asignarlos en el Inspector.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Referencias (se buscan solas si están vacías)")]
        [SerializeField] GameManager gameManager;
        [SerializeField] LevelManager levelManager;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerCollision playerCollision;

        [Header("Música")]
        [SerializeField] AudioClip music;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.5f;

        [Header("Efectos de sonido")]
        [SerializeField] AudioClip jump;
        [SerializeField] AudioClip land;
        [SerializeField] AudioClip barrelSmash;
        [SerializeField] AudioClip collect;
        [SerializeField] AudioClip damage;
        [SerializeField] AudioClip zoneComplete;
        [SerializeField] AudioClip victory;
        [SerializeField] AudioClip gameOver;
        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.8f;

        AudioSource musicSource;
        AudioSource sfxSource;

        /// <summary>Último clip pedido (aunque esté vacío). Útil para pruebas.</summary>
        public string LastRequested { get; private set; }

        void Awake()
        {
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
            if (levelManager == null) levelManager = FindFirstObjectByType<LevelManager>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (playerCollision == null && player != null) playerCollision = player.GetComponent<PlayerCollision>();

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = musicVolume;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        void Start()
        {
            if (music != null)
            {
                musicSource.clip = music;
                musicSource.Play();
            }
        }

        void OnEnable()
        {
            if (player != null)
            {
                player.Jumped += OnJumped;
                player.Landed += OnLanded;
            }
            if (playerCollision != null) playerCollision.Damaged += OnDamaged;
            if (levelManager != null) levelManager.ZoneCompleted += OnZoneCompleted;
            if (gameManager != null) gameManager.StateChanged += OnStateChanged;
            BarrelController.AnyDestroyed += OnBarrelDestroyed;
            Collectible.AnyCollected += OnCollected;
        }

        void OnDisable()
        {
            if (player != null)
            {
                player.Jumped -= OnJumped;
                player.Landed -= OnLanded;
            }
            if (playerCollision != null) playerCollision.Damaged -= OnDamaged;
            if (levelManager != null) levelManager.ZoneCompleted -= OnZoneCompleted;
            if (gameManager != null) gameManager.StateChanged -= OnStateChanged;
            BarrelController.AnyDestroyed -= OnBarrelDestroyed;
            Collectible.AnyCollected -= OnCollected;
        }

        void OnJumped() => Play(jump, nameof(jump));
        void OnLanded() => Play(land, nameof(land));
        void OnDamaged() => Play(damage, nameof(damage));
        void OnZoneCompleted(int zone) => Play(zoneComplete, nameof(zoneComplete));
        void OnCollected(Collectible c) => Play(collect, nameof(collect));

        void OnBarrelDestroyed(BarrelController barrel, bool byPlayer)
        {
            if (byPlayer) Play(barrelSmash, nameof(barrelSmash));
        }

        void OnStateChanged(GameManager.GameState state)
        {
            switch (state)
            {
                case GameManager.GameState.Paused: musicSource.Pause(); break;
                case GameManager.GameState.Playing: musicSource.UnPause(); break;
                case GameManager.GameState.Won: musicSource.Stop(); Play(victory, nameof(victory)); break;
                case GameManager.GameState.Lost: musicSource.Stop(); Play(gameOver, nameof(gameOver)); break;
            }
        }

        void Play(AudioClip clip, string slot)
        {
            LastRequested = slot;
            if (clip != null) sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }
}
