using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BarrelAscent.UI
{
    /// <summary>Menú principal: Jugar carga GameLevel; Salir cierra el juego.</summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] string gameScene = "GameLevel";
        [SerializeField] Selectable firstSelected;

        void Start()
        {
            Time.timeScale = 1f;
            if (firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
        }

        public void Play()
        {
            SceneManager.LoadScene(gameScene);
        }

        public void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
