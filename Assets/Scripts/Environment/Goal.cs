using BarrelAscent.Managers;
using BarrelAscent.Player;
using UnityEngine;

namespace BarrelAscent.Environment
{
    /// <summary>Meta final (Collider2D trigger). Avisa al LevelManager cuando la toca el jugador.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class Goal : MonoBehaviour
    {
        [SerializeField] LevelManager levelManager;

        void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            if (levelManager == null) levelManager = FindFirstObjectByType<LevelManager>();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (levelManager != null && other.GetComponentInParent<PlayerController>() != null)
                levelManager.ReachGoal();
        }
    }
}
