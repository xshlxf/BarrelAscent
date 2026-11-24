using System.Collections.Generic;
using BarrelAscent.Environment;
using UnityEngine;

namespace BarrelAscent.Enemies
{
    /// <summary>
    /// Punto de aparición de barriles de una zona. Mientras está activo genera un barril cada
    /// <see cref="ZoneConfig.spawnInterval"/> segundos (LevelManager activa solo el de la zona del jugador).
    /// </summary>
    public class BarrelSpawner : MonoBehaviour
    {
        [SerializeField] BarrelController barrelPrefab;
        [SerializeField] ZoneConfig zone;
        [Tooltip("Sentido inicial de rodadura: 1 = derecha, -1 = izquierda.")]
        [SerializeField] int direction = 1;
        [Tooltip("Espera antes del primer barril al activarse (s).")]
        [SerializeField] float firstSpawnDelay = 1f;
        [SerializeField] bool spawnOnStart = false;

        readonly List<BarrelController> alive = new List<BarrelController>();
        float timer;

        public bool IsSpawning { get; private set; }
        public ZoneConfig Zone => zone;
        public int Direction => direction;
        public int AliveCount { get { alive.RemoveAll(b => b == null); return alive.Count; } }

        void Start()
        {
            if (spawnOnStart) SetSpawning(true);
        }

        void Update()
        {
            if (!IsSpawning || zone == null || barrelPrefab == null) return;

            timer += Time.deltaTime;
            if (timer < zone.spawnInterval) return;
            timer = 0f;
            if (AliveCount < zone.maxBarrels) Spawn();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.72f, 0.23f, 0.21f); // #B83B36
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.4f, 0.4f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.4f, transform.position + new Vector3(direction * 1f, 0.4f));
        }

        /// <summary>Activa o detiene la generación. Los barriles ya creados siguen su recorrido.</summary>
        public void SetSpawning(bool value)
        {
            if (value && !IsSpawning) timer = Mathf.Max(0f, (zone != null ? zone.spawnInterval : 4f) - firstSpawnDelay);
            IsSpawning = value;
        }

        /// <summary>Crea un barril ahora mismo en este punto (apoyado sobre la plataforma).</summary>
        public BarrelController Spawn()
        {
            var barrel = Instantiate(barrelPrefab, transform.position, Quaternion.identity);
            barrel.transform.position = transform.position + Vector3.up * (barrel.Radius + 0.02f);
            barrel.Launch(zone != null ? zone.barrelSpeed : 2f, direction);
            alive.Add(barrel);
            return barrel;
        }

        /// <summary>Elimina los barriles de este spawner (reinicio de partida).</summary>
        public void ClearBarrels()
        {
            foreach (var b in alive)
                if (b != null) Destroy(b.gameObject);
            alive.Clear();
        }
    }
}
