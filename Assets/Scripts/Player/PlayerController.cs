using System;
using System.Collections.Generic;
using BarrelAscent.Environment;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BarrelAscent.Player
{
    /// <summary>
    /// Movimiento del jugador: desplazamiento horizontal, salto, escaleras y parámetros del Animator.
    /// La física la resuelve Rigidbody2D; este script solo fija velocidades.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        const float InputDeadZone = 0.1f;
        const float ClimbStartThreshold = 0.5f;
        const float LadderEdgeMargin = 0.05f;
        const float LandingClearance = 0.02f;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int ClimbingHash = Animator.StringToHash("Climbing");
        static readonly int ClimbSpeedHash = Animator.StringToHash("ClimbSpeed");
        static readonly int VelocityYHash = Animator.StringToHash("VelocityY");
        static readonly int HurtHash = Animator.StringToHash("Hurt");

        [Header("Movimiento")]
        [SerializeField] float moveSpeed = 5f;
        [Tooltip("Velocidad vertical inicial del salto (equivale a un impulso de 10 con masa 1).")]
        [SerializeField] float jumpForce = 10f;
        [SerializeField] float climbSpeed = 3f;
        [SerializeField] float gravityScale = 3f;

        [Header("Sensación de control")]
        [Tooltip("Margen tras salir de un borde en el que aún se permite saltar (s).")]
        [SerializeField] float coyoteTime = 0.08f;
        [Tooltip("Si se pulsa saltar justo antes de aterrizar, el salto se ejecuta al tocar el suelo (s).")]
        [SerializeField] float jumpBufferTime = 0.1f;

        [Header("Detección")]
        [SerializeField] LayerMask groundMask;
        [SerializeField] LayerMask ladderMask;
        [SerializeField] float groundCheckDepth = 0.08f;
        [Tooltip("Velocidad de caída mínima para considerar un aterrizaje (efectos y sonido).")]
        [SerializeField] float landingMinSpeed = 3f;

        [Header("Referencias")]
        [SerializeField] Animator animator;
        [SerializeField] SpriteRenderer spriteRenderer;
        [Tooltip("Opcional. Si está vacío se usan las Project-wide Actions (InputSystem.actions).")]
        [SerializeField] InputActionAsset inputActions;
        [Tooltip("Tiempo que se muestra la pose de daño tras un golpe (s).")]
        [SerializeField] float hurtPoseTime = 0.4f;

        Rigidbody2D body;
        CapsuleCollider2D bodyCollider;
        InputAction moveAction;
        InputAction jumpAction;

        Vector2 moveInput;
        float jumpRequestTime = float.NegativeInfinity;
        float lastGroundedTime = float.NegativeInfinity;
        float knockbackUntil = float.NegativeInfinity;
        Ladder currentLadder;
        float feetOffset; // distancia del pivote del Rigidbody2D a la base del collider
        readonly List<Collider2D> overlapResults = new List<Collider2D>(4);
        ContactFilter2D ladderFilter;
        bool wasGrounded;
        float airborneMinVelocityY;
        float hurtUntil = float.NegativeInfinity;
        bool animatorHasHurt;

        /// <summary>Se lanza al iniciar un salto (efectos y sonido).</summary>
        public event Action Jumped;
        /// <summary>Se lanza al aterrizar tras una caída de cierta velocidad (efectos y sonido).</summary>
        public event Action Landed;

        public bool IsGrounded { get; private set; }
        public bool IsClimbing { get; private set; }
        public bool FacingRight { get; private set; } = true;
        public bool ControlEnabled { get; private set; } = true;
        public Rigidbody2D Body => body;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<CapsuleCollider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            ladderFilter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = ladderMask };

            // "Hurt" es opcional: un Animator sin ese parámetro simplemente no muestra la pose de daño.
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var p in animator.parameters)
                    if (p.nameHash == HurtHash) animatorHasHurt = true;
        }

        void OnEnable()
        {
            BindActions(inputActions != null ? inputActions : InputSystem.actions);
        }

        void Update()
        {
            if (!ControlEnabled || moveAction == null)
            {
                moveInput = Vector2.zero;
                UpdateAnimator();
                return;
            }

            moveInput = moveAction.ReadValue<Vector2>();
            if (Mathf.Abs(moveInput.x) < InputDeadZone) moveInput.x = 0f;
            if (Mathf.Abs(moveInput.y) < InputDeadZone) moveInput.y = 0f;

            if (jumpAction.WasPressedThisFrame())
                jumpRequestTime = Time.time;

            UpdateFacing();
            UpdateAnimator();
        }

        void FixedUpdate()
        {
            float velocityYBefore = body.linearVelocity.y;
            UpdateGrounded();
            DetectLanding(velocityYBefore);

            if (IsClimbing)
            {
                ClimbStep();
                return;
            }

            TryStartClimb();
            if (IsClimbing) return;

            if (Time.time < knockbackUntil) return; // durante el retroceso no hay control horizontal

            body.linearVelocity = new Vector2(moveInput.x * moveSpeed, body.linearVelocity.y);
            TryJump();
        }

        void OnDrawGizmosSelected()
        {
            var c = GetComponent<CapsuleCollider2D>();
            if (c == null) return;
            Bounds b = c.bounds;
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(new Vector3(b.center.x, b.min.y - groundCheckDepth * 0.5f),
                                new Vector3(b.size.x * 0.9f, groundCheckDepth));
        }

        // ---------- API pública (daño, reaparición, fin de partida, pruebas) ----------

        /// <summary>Sustituye las acciones de entrada en tiempo de ejecución (pruebas con teclado simulado).</summary>
        public void SetInputActions(InputActionAsset asset)
        {
            inputActions = asset;
            BindActions(asset);
        }

        /// <summary>Activa o bloquea la entrada del jugador (pausa, victoria, game over, reaparición).</summary>
        public void SetControlEnabled(bool value)
        {
            ControlEnabled = value;
            if (!value)
            {
                moveInput = Vector2.zero;
                jumpRequestTime = float.NegativeInfinity;
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            }
        }

        /// <summary>
        /// Impulso vertical tras destruir un barril. No concede un salto: solo se puede volver a
        /// saltar al tocar el suelo.
        /// </summary>
        public void Bounce(float verticalVelocity)
        {
            StopClimbing();
            body.linearVelocity = new Vector2(body.linearVelocity.x, verticalVelocity);
            lastGroundedTime = float.NegativeInfinity;
            jumpRequestTime = float.NegativeInfinity;
            IsGrounded = false;
        }

        /// <summary>Retroceso al recibir daño: fija la velocidad y bloquea el control horizontal un instante.</summary>
        public void Knockback(Vector2 velocity, float duration = 0.25f)
        {
            StopClimbing();
            body.linearVelocity = velocity;
            knockbackUntil = Time.time + duration;
            hurtUntil = Time.time + Mathf.Max(duration, hurtPoseTime);
            lastGroundedTime = float.NegativeInfinity;
            IsGrounded = false;
        }

        /// <summary>Coloca al jugador en un punto, sin velocidad y fuera de cualquier escalera.</summary>
        public void Teleport(Vector2 position)
        {
            StopClimbing();
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            lastGroundedTime = float.NegativeInfinity;
            hurtUntil = float.NegativeInfinity;
        }

        /// <summary>Sale de la escalera y restaura gravedad y colisiones. Seguro de llamar siempre.</summary>
        public void StopClimbing()
        {
            if (!IsClimbing) return;
            IsClimbing = false;
            currentLadder = null;
            body.gravityScale = gravityScale;
            body.excludeLayers = 0;
            body.linearVelocity = Vector2.zero;
        }

        // ---------- Entrada ----------

        void BindActions(InputActionAsset asset)
        {
            moveAction = null;
            jumpAction = null;
            if (asset == null)
            {
                Debug.LogWarning("[PlayerController] No hay Input Actions asignadas ni Project-wide Actions; el jugador no responderá.", this);
                return;
            }

            moveAction = asset.FindAction("Player/Move", throwIfNotFound: true);
            jumpAction = asset.FindAction("Player/Jump", throwIfNotFound: true);
            moveAction.Enable();
            jumpAction.Enable();
        }

        // ---------- Suelo y salto ----------

        void UpdateGrounded()
        {
            Bounds b = bodyCollider.bounds;
            var center = new Vector2(b.center.x, b.min.y - groundCheckDepth * 0.5f);
            var size = new Vector2(b.size.x * 0.9f, groundCheckDepth);
            bool touching = Physics2D.OverlapBox(center, size, 0f, groundMask) != null;

            // Solo cuenta como apoyo si no estamos subiendo: evita "re-aterrizar" en el frame del salto.
            IsGrounded = touching && body.linearVelocity.y <= 0.05f;
            if (IsGrounded && !IsClimbing) lastGroundedTime = Time.time;
        }

        void DetectLanding(float velocityYBefore)
        {
            if (!IsGrounded) airborneMinVelocityY = Mathf.Min(airborneMinVelocityY, velocityYBefore);
            if (IsGrounded && !wasGrounded && !IsClimbing && airborneMinVelocityY <= -landingMinSpeed)
                Landed?.Invoke();
            if (IsGrounded || IsClimbing) airborneMinVelocityY = 0f;
            wasGrounded = IsGrounded;
        }

        void TryJump()
        {
            bool requested = Time.time - jumpRequestTime <= jumpBufferTime;
            bool canJump = Time.time - lastGroundedTime <= coyoteTime;
            if (!requested || !canJump) return;

            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
            jumpRequestTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity; // consume el contacto: sin doble salto
            IsGrounded = false;
            Jumped?.Invoke();
        }

        // ---------- Escaleras ----------

        Ladder FindLadder()
        {
            Bounds b = bodyCollider.bounds;
            var probeCenter = new Vector2(b.center.x, b.min.y + 0.25f);
            var probeSize = new Vector2(0.2f, 0.3f);
            ladderFilter.layerMask = ladderMask;
            int count = Physics2D.OverlapBox(probeCenter, probeSize, 0f, ladderFilter, overlapResults);
            for (int i = 0; i < count; i++)
            {
                if (overlapResults[i].TryGetComponent(out Ladder ladder))
                    return ladder;
            }
            return null;
        }

        void TryStartClimb()
        {
            if (Mathf.Abs(moveInput.y) < ClimbStartThreshold) return;

            Ladder ladder = FindLadder();
            if (ladder == null) return;

            float feet = bodyCollider.bounds.min.y;
            bool climbUp = moveInput.y > 0f && feet < ladder.TopY - LadderEdgeMargin;
            bool climbDown = moveInput.y < 0f && IsGrounded && feet > ladder.BottomY + LadderEdgeMargin;
            if (climbUp || climbDown) StartClimb(ladder);
        }

        void StartClimb(Ladder ladder)
        {
            IsClimbing = true;
            currentLadder = ladder;
            feetOffset = bodyCollider.bounds.min.y - body.position.y;

            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.excludeLayers = groundMask; // atraviesa las plataformas que cruza la escalera

            float dx = ladder.CenterX - bodyCollider.bounds.center.x;
            body.position += new Vector2(dx, 0f);

            jumpRequestTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
        }

        void ClimbStep()
        {
            if (currentLadder == null || FindLadder() != currentLadder)
            {
                StopClimbing();
                return;
            }

            jumpRequestTime = float.NegativeInfinity; // no se puede saltar desde la escalera

            float v = moveInput.y;
            float feet = body.position.y + feetOffset;
            float nextFeet = feet + v * climbSpeed * Time.fixedDeltaTime;

            if (v > 0f && nextFeet >= currentLadder.TopY)
            {
                ExitLadderAt(currentLadder.TopY);
                return;
            }
            if (v < 0f && nextFeet <= currentLadder.BottomY)
            {
                ExitLadderAt(currentLadder.BottomY);
                return;
            }

            body.linearVelocity = new Vector2(0f, v * climbSpeed);
        }

        void ExitLadderAt(float feetY)
        {
            body.position = new Vector2(body.position.x, feetY - feetOffset + LandingClearance);
            StopClimbing();
        }

        // ---------- Presentación ----------

        void UpdateFacing()
        {
            if (IsClimbing || moveInput.x == 0f) return;
            FacingRight = moveInput.x > 0f;
            if (spriteRenderer != null) spriteRenderer.flipX = !FacingRight;
        }

        void UpdateAnimator()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetFloat(SpeedHash, IsClimbing ? 0f : Mathf.Abs(moveInput.x));
            animator.SetBool(GroundedHash, IsGrounded);
            animator.SetBool(ClimbingHash, IsClimbing);
            animator.SetFloat(ClimbSpeedHash, IsClimbing ? Mathf.Abs(moveInput.y) : 0f);
            animator.SetFloat(VelocityYHash, body.linearVelocity.y);
            if (animatorHasHurt) animator.SetBool(HurtHash, Time.time < hurtUntil);
        }
    }
}
