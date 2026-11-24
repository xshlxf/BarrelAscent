using System;
using System.Collections;
using BarrelAscent.Managers;
using BarrelAscent.Player;
using UnityEngine;

namespace BarrelAscent.Environment
{
    /// <summary>
    /// Objeto recogible: al tocarlo el jugador suma puntos (una sola vez) y desaparece.
    /// Flota suavemente para distinguirse del escenario.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Collectible : MonoBehaviour
    {
        /// <summary>Se lanza cuando cualquier coleccionable se recoge (efectos y sonido).</summary>
        public static event Action<Collectible> AnyCollected;

        [SerializeField] int points = 100;
        [SerializeField] Transform visual;
        [SerializeField] float bobAmplitude = 0.08f;
        [SerializeField] float bobSpeed = 3f;
        [SerializeField] float collectDuration = 0.2f;

        Collider2D area;
        Vector3 visualBase;
        float phase;

        public bool IsCollected { get; private set; }
        public int Points => points;

        void Awake()
        {
            area = GetComponent<Collider2D>();
            area.isTrigger = true;
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null) visualBase = visual.localPosition;
            phase = transform.position.x * 1.7f; // desfase para que no floten todos a la vez
        }

        void Update()
        {
            if (IsCollected || visual == null) return;
            float offset = Mathf.Round(Mathf.Sin(Time.time * bobSpeed + phase) * bobAmplitude * 32f) / 32f; // a píxel
            visual.localPosition = visualBase + new Vector3(0f, offset, 0f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null) Collect();
        }

        /// <summary>Recoge el objeto. Devuelve false si ya estaba recogido.</summary>
        public bool Collect()
        {
            if (IsCollected) return false;
            IsCollected = true;
            area.enabled = false;
            GameManager.Instance?.AddScore(points, "Coleccionable");
            AnyCollected?.Invoke(this);
            StartCoroutine(PopAndRemove());
            return true;
        }

        IEnumerator PopAndRemove()
        {
            float t = 0f;
            Vector3 start = visual != null ? visual.localScale : Vector3.one;
            while (t < collectDuration && visual != null)
            {
                t += Time.deltaTime;
                float k = t / collectDuration;
                visual.localScale = start * (1f + 0.5f * k);
                visual.localPosition += Vector3.up * Time.deltaTime * 2f;
                var sr = visual.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
