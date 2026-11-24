using System;
using BarrelAscent.Enemies;
using UnityEngine;

namespace BarrelAscent.Managers
{
    /// <summary>
    /// Zonas, progresión y meta: determina la zona del jugador por su altura, activa solo el
    /// spawner de esa zona, concede +250 al superar cada zona (una vez), guarda el punto de
    /// reaparición y, en la meta, suma +1000 y declara la victoria.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] Transform player;
        [Tooltip("Altura Y donde empieza cada zona, en orden (la primera suele ser 0).")]
        [SerializeField] float[] zoneStartY = { 0f, 14f, 30f };
        [Tooltip("Spawner de cada zona, en el mismo orden que zoneStartY.")]
        [SerializeField] BarrelSpawner[] zoneSpawners;
        [Tooltip("Punto seguro de reaparición de cada zona, en el mismo orden.")]
        [SerializeField] Transform[] respawnPoints;

        [Header("Recompensas")]
        [SerializeField] int zoneCompletionPoints = 250;
        [SerializeField] int goalPoints = 1000;

        /// <summary>Índice de zona (0, 1, 2) en la que está ahora el jugador.</summary>
        public event Action<int> ZoneChanged;
        /// <summary>Zona completada por primera vez (índice de la zona superada).</summary>
        public event Action<int> ZoneCompleted;
        public event Action GoalReached;

        public int CurrentZone { get; private set; } = -1;
        public int HighestZoneReached { get; private set; }
        public bool IsGoalReached { get; private set; }
        public int ZoneCount => zoneStartY.Length;

        public Vector3 RespawnPoint
        {
            get
            {
                if (respawnPoints == null || respawnPoints.Length == 0)
                    return player != null ? player.position : Vector3.zero;
                int i = Mathf.Clamp(HighestZoneReached, 0, respawnPoints.Length - 1);
                return respawnPoints[i] != null ? respawnPoints[i].position : respawnPoints[0].position;
            }
        }

        void Start()
        {
            if (player != null) HighestZoneReached = ZoneAt(player.position.y);
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

        /// <summary>La llama Goal cuando el jugador toca la meta. Solo tiene efecto una vez.</summary>
        public void ReachGoal()
        {
            if (IsGoalReached) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing) return;

            IsGoalReached = true;
            CompleteZone(ZoneCount - 1);
            HighestZoneReached = ZoneCount - 1;
            GameManager.Instance?.AddScore(goalPoints, "Cima alcanzada");
            StopAllSpawners();
            GoalReached?.Invoke();
            GameManager.Instance?.Win();
        }

        /// <summary>Detiene todos los spawners (victoria, game over).</summary>
        public void StopAllSpawners()
        {
            foreach (var s in zoneSpawners)
                if (s != null) s.SetSpawning(false);
        }

        void UpdateZone(bool force)
        {
            if (player == null || IsGoalReached) return;
            int zone = ZoneAt(player.position.y);
            if (!force && zone == CurrentZone) return;

            CurrentZone = zone;
            while (HighestZoneReached < zone)
            {
                CompleteZone(HighestZoneReached);
                HighestZoneReached++;
            }

            for (int i = 0; i < zoneSpawners.Length; i++)
                if (zoneSpawners[i] != null) zoneSpawners[i].SetSpawning(i == zone);
            ZoneChanged?.Invoke(zone);
        }

        void CompleteZone(int zoneIndex)
        {
            GameManager.Instance?.AddScore(zoneCompletionPoints, $"Zona {zoneIndex + 1} completada");
            ZoneCompleted?.Invoke(zoneIndex);
        }
    }
}
