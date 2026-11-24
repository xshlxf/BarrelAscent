using System.Collections.Generic;
using BarrelAscent.Enemies;
using BarrelAscent.Environment;
using BarrelAscent.Player;
using UnityEngine;

namespace BarrelAscent.Effects
{
    /// <summary>
    /// Efectos visuales del juego. Escucha los eventos existentes (salto, aterrizaje, daño,
    /// barril destruido, coleccionable) y reproduce secuencias de sprites con un pool pequeño.
    /// Ningún sistema de juego llama a este componente: si se elimina, el juego sigue igual.
    /// </summary>
    public class EffectsManager : MonoBehaviour
    {
        [System.Serializable]
        public class EffectSequence
        {
            public Sprite[] frames;
            public float fps = 14f;
            public Vector2 offset;
        }

        [SerializeField] PlayerController player;
        [SerializeField] PlayerCollision playerCollision;

        [Header("Secuencias")]
        [SerializeField] EffectSequence dust = new EffectSequence();
        [SerializeField] EffectSequence barrelSmash = new EffectSequence();
        [SerializeField] EffectSequence collect = new EffectSequence();
        [SerializeField] EffectSequence damage = new EffectSequence();

        [SerializeField] int sortingOrder = 15;
        [SerializeField] int poolSize = 8;

        readonly List<SpriteEffect> pool = new List<SpriteEffect>();

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (playerCollision == null && player != null) playerCollision = player.GetComponent<PlayerCollision>();
            for (int i = 0; i < poolSize; i++) CreateEffect();
        }

        void OnEnable()
        {
            if (player != null)
            {
                player.Jumped += OnJumped;
                player.Landed += OnLanded;
            }
            if (playerCollision != null) playerCollision.Damaged += OnDamaged;
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
            BarrelController.AnyDestroyed -= OnBarrelDestroyed;
            Collectible.AnyCollected -= OnCollected;
        }

        void OnJumped() => Spawn(dust, player.transform.position, !player.FacingRight);
        void OnLanded() => Spawn(dust, player.transform.position, false);
        void OnDamaged() => Spawn(damage, player.transform.position, false);

        void OnBarrelDestroyed(BarrelController barrel, bool byPlayer)
        {
            if (byPlayer) Spawn(barrelSmash, barrel.transform.position, false);
        }

        void OnCollected(Collectible c) => Spawn(collect, c.transform.position, false);

        /// <summary>Reproduce una secuencia en una posición del mundo. Público para pruebas y usos futuros.</summary>
        public SpriteEffect Spawn(EffectSequence sequence, Vector3 position, bool flipX)
        {
            if (sequence == null || sequence.frames == null || sequence.frames.Length == 0) return null;
            var fx = GetFree();
            fx.Play(sequence.frames, sequence.fps, position + (Vector3)sequence.offset, flipX, sortingOrder);
            return fx;
        }

        public EffectSequence Dust => dust;
        public EffectSequence BarrelSmash => barrelSmash;
        public EffectSequence Collect => collect;
        public EffectSequence Damage => damage;

        SpriteEffect GetFree()
        {
            foreach (var fx in pool)
                if (!fx.IsPlaying) return fx;
            return CreateEffect(); // el pool crece si hacen falta más a la vez
        }

        SpriteEffect CreateEffect()
        {
            var go = new GameObject("Effect");
            go.transform.SetParent(transform, false);
            go.AddComponent<SpriteRenderer>();
            var fx = go.AddComponent<SpriteEffect>();
            go.SetActive(false);
            pool.Add(fx);
            return fx;
        }
    }
}
