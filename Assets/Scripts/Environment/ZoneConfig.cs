using UnityEngine;

namespace BarrelAscent.Environment
{
    /// <summary>
    /// Valores de dificultad de una zona. Se editan en el Inspector sin tocar código
    /// (Assets/Settings/Zones/Zone1..3.asset).
    /// </summary>
    [CreateAssetMenu(fileName = "ZoneConfig", menuName = "Barrel Ascent/Zone Config")]
    public class ZoneConfig : ScriptableObject
    {
        public string displayName = "Zona";
        [Tooltip("Velocidad horizontal de los barriles (unidades/s).")]
        public float barrelSpeed = 2f;
        [Tooltip("Segundos entre apariciones de barriles.")]
        public float spawnInterval = 4f;
        [Tooltip("Máximo de barriles vivos de este spawner a la vez.")]
        public int maxBarrels = 6;
    }
}
