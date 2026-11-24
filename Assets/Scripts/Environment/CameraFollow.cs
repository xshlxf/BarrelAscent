using UnityEngine;

namespace BarrelAscent.Environment
{
    /// <summary>
    /// Seguimiento vertical suave del jugador. La X queda fija en el centro del nivel y la Y
    /// se limita para no mostrar nada por debajo de <see cref="minY"/> ni por encima de <see cref="maxY"/>.
    /// Se ejecuta en LateUpdate, después de que el jugador se haya movido.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Header("Límites del nivel (unidades de mundo)")]
        [SerializeField] float levelCenterX = 0f;
        [SerializeField] float minY = 0f;
        [SerializeField] float maxY = 46f;

        [Header("Seguimiento")]
        [Tooltip("Desplaza el encuadre hacia arriba para ver más de lo que viene (el jugador queda en el tercio inferior).")]
        [SerializeField] float lookAheadY = 1.5f;
        [Tooltip("Tiempo aproximado para alcanzar al objetivo (s). Menor = más rígido.")]
        [SerializeField] float smoothTime = 0.18f;
        [Tooltip("Redondea la posición a la rejilla de píxeles para evitar temblores en pixel art.")]
        [SerializeField] bool snapToPixelGrid = true;
        [SerializeField] int pixelsPerUnit = 32;

        Camera cam;
        float velocityY;
        float smoothedY;

        public Transform Target { get => target; set => target = value; }
        public float MinY => minY;
        public float MaxY => maxY;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void Start()
        {
            SnapToTarget();
        }

        void LateUpdate()
        {
            if (target == null) return;

            float desired = ClampY(target.position.y + lookAheadY);
            // El suavizado trabaja sobre una Y sin redondear; solo la posición final se ajusta a píxel.
            // Si se suavizara desde la posición ya redondeada, con FPS altos cada paso sería menor
            // que medio píxel y la cámara se quedaría atascada.
            smoothedY = ClampY(Mathf.SmoothDamp(smoothedY, desired, ref velocityY, smoothTime));
            SetPosition(smoothedY);
        }

        /// <summary>Coloca la cámara directamente sobre el objetivo, sin suavizado (inicio, reaparición).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            velocityY = 0f;
            smoothedY = ClampY(target.position.y + lookAheadY);
            SetPosition(smoothedY);
        }

        /// <summary>Define los límites verticales del nivel en tiempo de ejecución.</summary>
        public void SetBounds(float centerX, float bottom, float top)
        {
            levelCenterX = centerX;
            minY = bottom;
            maxY = top;
        }

        float HalfHeight => cam != null ? cam.orthographicSize : 5.625f;

        float ClampY(float y)
        {
            float low = minY + HalfHeight;
            float high = maxY - HalfHeight;
            if (high < low) return (minY + maxY) * 0.5f; // nivel más bajo que la vista
            return Mathf.Clamp(y, low, high);
        }

        void SetPosition(float y)
        {
            float x = levelCenterX;
            if (snapToPixelGrid && pixelsPerUnit > 0)
            {
                x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit;
                y = Mathf.Round(y * pixelsPerUnit) / pixelsPerUnit;
            }
            transform.position = new Vector3(x, y, transform.position.z);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(levelCenterX - 10f, minY), new Vector3(levelCenterX + 10f, minY));
            Gizmos.DrawLine(new Vector3(levelCenterX - 10f, maxY), new Vector3(levelCenterX + 10f, maxY));
        }
    }
}
