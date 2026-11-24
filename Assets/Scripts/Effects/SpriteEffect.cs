using UnityEngine;

namespace BarrelAscent.Effects
{
    /// <summary>
    /// Efecto visual de un solo uso: reproduce una secuencia de sprites y se desactiva al terminar
    /// (el EffectsManager lo reutiliza). Usa Time.deltaTime, así que se congela con la pausa.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteEffect : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        Sprite[] frames;
        float frameTime;
        float elapsed;

        public bool IsPlaying => frames != null && gameObject.activeSelf;

        void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        public void Play(Sprite[] sequence, float fps, Vector3 position, bool flipX, int sortingOrder)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            frames = sequence;
            frameTime = 1f / Mathf.Max(1f, fps);
            elapsed = 0f;
            transform.position = position;
            spriteRenderer.flipX = flipX;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.sprite = frames.Length > 0 ? frames[0] : null;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (frames == null) return;
            elapsed += Time.deltaTime;
            int index = Mathf.FloorToInt(elapsed / frameTime);
            if (index >= frames.Length)
            {
                frames = null;
                gameObject.SetActive(false);
                return;
            }
            spriteRenderer.sprite = frames[index];
        }
    }
}
