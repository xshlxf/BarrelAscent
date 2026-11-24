using System;
using System.Collections;
using UnityEngine;

namespace BarrelAscent.Enemies
{
    /// <summary>
    /// Barril autónomo con máquina de estados SPAWNED → ROLLING ⇄ FALLING → DESTROYED.
    /// El código decide dirección, velocidad y transiciones; Rigidbody2D resuelve gravedad y caídas.
    /// Suelo y paredes se detectan con OverlapBox / Raycast sobre la capa Ground.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class BarrelController : MonoBehaviour
    {
        public enum State { Spawned, Rolling, Falling, Destroyed }

        [Header("Movimiento")]
        [SerializeField] float speed = 2f;
        [SerializeField] int direction = 1;
        [Tooltip("Tiempo quieto tras aparecer antes de empezar a rodar (s).")]
        [SerializeField] float spawnDelay = 0.3f;
        [Tooltip("Fracción de la velocidad horizontal que conserva mientras cae.")]
        [SerializeField] float fallHorizontalFactor = 0.5f;

        [Header("Detección")]
        [SerializeField] LayerMask groundMask;
        [SerializeField] float groundCheckDepth = 0.08f;
        [Tooltip("Por debajo de esta altura el barril se elimina (cayó fuera del nivel).")]
        [SerializeField] float killY = -5f;
        [SerializeField] float maxLifetime = 90f;

        [Header("Presentación")]
        [SerializeField] Transform visual;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Sprite rollingSprite;
        [SerializeField] Sprite fallingSprite;
        [SerializeField] Sprite destroyedSprite;
        [SerializeField] float destroyedDuration = 0.4f;

        Rigidbody2D body;
        CircleCollider2D circle;
        float stateTime;
        float age;
        float stuckTimer;

        /// <summary>Se lanza una sola vez cuando un barril se destruye. El bool indica si fue el jugador.</summary>
        public static event Action<BarrelController, bool> AnyDestroyed;

        public State CurrentState { get; private set; } = State.Spawned;
        public int Direction => direction;
        public float Speed => speed;
        public float Radius => circle.radius * Mathf.Abs(transform.lossyScale.y);
        public bool IsDangerous => CurrentState != State.Destroyed;
        public Rigidbody2D Body => body;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();
            body.freezeRotation = true; // la rotación visual la hace el hijo "visual"
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (visual == null && spriteRenderer != null) visual = spriteRenderer.transform;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            stateTime += dt;
            age += dt;

            if (CurrentState == State.Destroyed) return;

            if (transform.position.y < killY || age > maxLifetime)
            {
                Despawn();
                return;
            }

            bool grounded = IsGrounded();

            switch (CurrentState)
            {
                case State.Spawned:
                    body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                    if (stateTime >= spawnDelay && grounded) EnterState(State.Rolling);
                    break;

                case State.Rolling:
                    if (!grounded)
                    {
                        EnterState(State.Falling); // llegó al final de la plataforma
                        break;
                    }
                    if (IsWallAhead())
                    {
                        Despawn(); // chocó con una pared o un escalón: no hay más recorrido
                        break;
                    }
                    body.linearVelocity = new Vector2(direction * speed, body.linearVelocity.y);
                    CheckStuck(dt);
                    break;

                case State.Falling:
                    body.linearVelocity = new Vector2(direction * speed * fallHorizontalFactor, body.linearVelocity.y);
                    if (grounded && body.linearVelocity.y <= 0.01f)
                    {
                        // Al caer al piso inferior rueda en sentido contrario (recorrido en zigzag).
                        direction = -direction;
                        EnterState(State.Rolling);
                    }
                    break;
            }

            RotateVisual(dt);
        }

        void OnDrawGizmosSelected()
        {
            var c = GetComponent<CircleCollider2D>();
            if (c == null) return;
            Bounds b = c.bounds;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(b.center.x, b.min.y - groundCheckDepth * 0.5f),
                                new Vector3(b.extents.x * 0.5f, groundCheckDepth));
            Gizmos.DrawLine(new Vector3(b.center.x + direction * b.extents.x, b.center.y),
                            new Vector3(b.center.x + direction * (b.extents.x + 0.06f), b.center.y));
        }

        /// <summary>Configura velocidad y sentido al aparecer (lo llama BarrelSpawner).</summary>
        public void Launch(float newSpeed, int newDirection)
        {
            speed = newSpeed;
            direction = newDirection >= 0 ? 1 : -1;
            EnterState(State.Spawned);
        }

        /// <summary>Destrucción por el jugador (aterrizó encima). Solo cuenta una vez.</summary>
        public bool Smash()
        {
            if (CurrentState == State.Destroyed) return false;
            Destroy(byPlayer: true);
            return true;
        }

        /// <summary>Eliminación sin recompensa (fuera del nivel, contra una pared, atascado).</summary>
        public void Despawn()
        {
            if (CurrentState == State.Destroyed) return;
            Destroy(byPlayer: false);
        }

        void EnterState(State next)
        {
            CurrentState = next;
            stateTime = 0f;
            stuckTimer = 0f;
            if (spriteRenderer == null) return;
            if (next == State.Falling && fallingSprite != null) spriteRenderer.sprite = fallingSprite;
            else if ((next == State.Rolling || next == State.Spawned) && rollingSprite != null) spriteRenderer.sprite = rollingSprite;
        }

        bool IsGrounded()
        {
            Bounds b = circle.bounds;
            // Caja estrecha bajo el centro: el barril "cae" en cuanto su centro sobrepasa el borde.
            var center = new Vector2(b.center.x, b.min.y - groundCheckDepth * 0.5f);
            var size = new Vector2(b.extents.x * 0.5f, groundCheckDepth);
            return Physics2D.OverlapBox(center, size, 0f, groundMask) != null;
        }

        bool IsWallAhead()
        {
            Bounds b = circle.bounds;
            var origin = new Vector2(b.center.x + direction * b.extents.x, b.center.y);
            return Physics2D.Raycast(origin, new Vector2(direction, 0f), 0.06f, groundMask).collider != null;
        }

        void CheckStuck(float dt)
        {
            stuckTimer = Mathf.Abs(body.linearVelocity.x) < speed * 0.25f ? stuckTimer + dt : 0f;
            if (stuckTimer > 1f) Despawn();
        }

        void RotateVisual(float dt)
        {
            if (visual == null || CurrentState != State.Rolling) return;
            float angularDeg = -body.linearVelocity.x / Mathf.Max(0.01f, Radius) * Mathf.Rad2Deg;
            visual.Rotate(0f, 0f, angularDeg * dt);
        }

        void Destroy(bool byPlayer)
        {
            EnterState(State.Destroyed);
            circle.enabled = false; // ya no puede causar daño ni colisionar
            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;

            AnyDestroyed?.Invoke(this, byPlayer);

            if (byPlayer && spriteRenderer != null && destroyedSprite != null)
            {
                spriteRenderer.sprite = destroyedSprite;
                if (visual != null) visual.rotation = Quaternion.identity;
                StartCoroutine(FadeAndRemove());
            }
            else
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        IEnumerator FadeAndRemove()
        {
            float t = 0f;
            Color c = spriteRenderer.color;
            while (t < destroyedDuration)
            {
                t += Time.deltaTime;
                spriteRenderer.color = new Color(c.r, c.g, c.b, 1f - t / destroyedDuration);
                yield return null;
            }
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
