using UnityEngine;

namespace BarrelAscent.Environment
{
    /// <summary>
    /// Zona de escalera. El BoxCollider2D (trigger) va desde la superficie del suelo inferior
    /// hasta <see cref="topExtension"/> unidades por encima de la plataforma superior,
    /// para que el jugador de pie arriba también la detecte y pueda bajar.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Ladder : MonoBehaviour
    {
        [Tooltip("Cuánto sobresale el trigger por encima de la plataforma superior (unidades).")]
        [SerializeField] float topExtension = 0.5f;

        BoxCollider2D area;

        /// <summary>X del centro de la escalera (el jugador se alinea aquí al escalar).</summary>
        public float CenterX => area.bounds.center.x;

        /// <summary>Altura de los pies en el extremo inferior (superficie del suelo inferior).</summary>
        public float BottomY => area.bounds.min.y;

        /// <summary>Altura de los pies en el extremo superior (superficie de la plataforma superior).</summary>
        public float TopY => area.bounds.max.y - topExtension;

        public float TopExtension => topExtension;

        void Awake()
        {
            area = GetComponent<BoxCollider2D>();
            area.isTrigger = true;
        }

        void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            var b = box.bounds;
            Gizmos.color = new Color(0.9f, 0.78f, 0.42f, 0.8f); // #E6C66B
            Gizmos.DrawWireCube(b.center, b.size);
            float top = b.max.y - topExtension;
            Gizmos.DrawLine(new Vector3(b.min.x, top), new Vector3(b.max.x, top));
        }
    }
}
