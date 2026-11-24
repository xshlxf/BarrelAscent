using System;
using System.Collections;
using System.Collections.Generic;
using BarrelAscent.Enemies;
using UnityEngine;

namespace BarrelAscent.Player
{
    /// <summary>
    /// Impactos del jugador con barriles: caer encima destruye el barril y hace rebotar al jugador;
    /// un impacto lateral causa daño, retroceso e invulnerabilidad temporal (parpadeo).
    /// Jugador y barriles no colisionan físicamente (matriz de capas): el contacto se detecta con
    /// un solapamiento de la cápsula, así un barril nunca empuja al jugador.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(CapsuleCollider2D))]
    public class PlayerCollision : MonoBehaviour
    {
        [SerializeField] LayerMask barrelMask;

        [Header("Daño")]
        [SerializeField] float invulnerabilityTime = 1.5f;
        [SerializeField] Vector2 knockback = new Vector2(3f, 5f);
        [SerializeField] float blinkInterval = 0.1f;

        [Header("Rebote al destruir un barril")]
        [SerializeField] float bounceVelocity = 9f;
        [Tooltip("Los pies deben estar al menos esta distancia por encima del centro del barril.")]
        [SerializeField] float stompMargin = 0.05f;
        [Tooltip("Velocidad vertical máxima para contar como aterrizaje (subiendo rápido no cuenta).")]
        [SerializeField] float stompMaxVerticalSpeed = 2f;

        [Header("Referencias")]
        [SerializeField] SpriteRenderer spriteRenderer;

        PlayerController controller;
        CapsuleCollider2D capsule;
        readonly List<Collider2D> hits = new List<Collider2D>(8);
        ContactFilter2D barrelFilter;
        float invulnerableUntil = float.NegativeInfinity;
        Coroutine blinkRoutine;

        /// <summary>El jugador recibió daño (una vez por impacto, nunca durante la invulnerabilidad).</summary>
        public event Action Damaged;
        /// <summary>El jugador destruyó un barril al caer encima.</summary>
        public event Action<BarrelController> BarrelSmashed;

        public bool IsInvulnerable => Time.time < invulnerableUntil;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            capsule = GetComponent<CapsuleCollider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        void FixedUpdate()
        {
            barrelFilter = new ContactFilter2D { useLayerMask = true, layerMask = barrelMask, useTriggers = false };
            Bounds b = capsule.bounds;
            int count = Physics2D.OverlapCapsule(b.center, b.size, capsule.direction, 0f, barrelFilter, hits);
            if (count == 0) return;

            bool smashedThisStep = false;
            for (int i = 0; i < count; i++)
            {
                if (!hits[i].TryGetComponent(out BarrelController barrel) || !barrel.IsDangerous) continue;

                if (IsStomp(barrel))
                {
                    if (barrel.Smash())
                    {
                        smashedThisStep = true;
                        BarrelSmashed?.Invoke(barrel);
                    }
                }
                else if (!smashedThisStep)
                {
                    TakeHit(barrel.transform.position.x);
                }
            }

            if (smashedThisStep) controller.Bounce(bounceVelocity);
        }

        void OnDisable()
        {
            if (spriteRenderer != null) spriteRenderer.enabled = true;
        }

        /// <summary>Aplica daño si no está en invulnerabilidad. Devuelve true si el daño se aplicó.</summary>
        public bool TakeHit(float sourceX)
        {
            if (IsInvulnerable) return false;

            invulnerableUntil = Time.time + invulnerabilityTime;
            controller.StopClimbing();
            float away = transform.position.x >= sourceX ? 1f : -1f;
            controller.Knockback(new Vector2(away * knockback.x, knockback.y));

            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            blinkRoutine = StartCoroutine(Blink());

            Damaged?.Invoke();
            return true;
        }

        /// <summary>Concede invulnerabilidad sin daño (p. ej. tras reaparecer).</summary>
        public void GrantInvulnerability(float seconds)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            blinkRoutine = StartCoroutine(Blink());
        }

        bool IsStomp(BarrelController barrel)
        {
            float feet = capsule.bounds.min.y;
            bool above = feet >= barrel.transform.position.y + stompMargin;
            bool notRisingFast = controller.Body.linearVelocity.y <= stompMaxVerticalSpeed;
            return above && notRisingFast && !controller.IsClimbing;
        }

        IEnumerator Blink()
        {
            while (IsInvulnerable)
            {
                if (spriteRenderer != null) spriteRenderer.enabled = !spriteRenderer.enabled;
                yield return new WaitForSeconds(blinkInterval);
            }
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            blinkRoutine = null;
        }
    }
}
