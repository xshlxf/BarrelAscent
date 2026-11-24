using System;
using BarrelAscent.Enemies;
using UnityEngine;

namespace BarrelAscent.Managers
{
    /// <summary>
    /// Control de zonas: determina en qué zona está el jugador según su altura y activa
    /// únicamente el spawner de barriles de esa zona (dificultad progresiva).
    /// Fase 5 añadirá la progresión (bonus por zona) y la meta.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] Transform player;
        [Tooltip("Altura Y donde empieza cada zona, en orden (la primera suele ser 0).")]
        [SerializeField] float[] zoneStartY = { 0f, 14f, 30f };
        [Tooltip("Spawner de cada zona, en el mismo orden que zoneStartY.")]
        [SerializeField] BarrelSpawner[] zoneSpawners;

        /// <summary>Índice de zona (0, 1, 2) al que acaba de entrar el jugador.</summary>
        public event Action<int> ZoneChanged;

        public int CurrentZone { get; private set; } = -1;
        public int ZoneCount => zoneStartY.Length;

        void Start()
        {
            UpdateZone(force: true);
        }

        void Update()
        {
            UpdateZone(force: false);
        }

        public int ZoneAt(float y)
        {
            int zone = 0;
            for (int i = 0; i < zoneStartY.Length; i++)
                if (y >= zoneStartY[i]) zone = i;
            return zone;
        }

        void UpdateZone(bool force)
        {
            if (player == null) return;
            int zone = ZoneAt(player.position.y);
            if (!force && zone == CurrentZone) return;

            CurrentZone = zone;
            for (int i = 0; i < zoneSpawners.Length; i++)
                if (zoneSpawners[i] != null) zoneSpawners[i].SetSpawning(i == zone);
            ZoneChanged?.Invoke(zone);
        }

        /// <summary>Detiene todos los spawners (victoria, game over, pausa larga).</summary>
        public void StopAllSpawners()
        {
            foreach (var s in zoneSpawners)
                if (s != null) s.SetSpawning(false);
        }
    }
}
