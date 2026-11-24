#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using BarrelAscent.Effects;
using BarrelAscent.Enemies;
using BarrelAscent.Environment;
using BarrelAscent.Managers;
using BarrelAscent.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BarrelAscent.Tests
{
    /// <summary>
    /// Pruebas de la Fase 7: el arte definitivo está conectado (sin placeholders en el jugador,
    /// Animator con clips) y los eventos de juego disparan efectos y peticiones de sonido.
    /// </summary>
    public class ArtAudioTests : InputTestFixture
    {
        const string ActionsPath = "Assets/Settings/BarrelAscentControls.inputactions";

        Keyboard keyboard;
        PlayerController player;
        EffectsManager effects;
        AudioManager audio;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [UnityTearDown]
        public IEnumerator UnloadLevel()
        {
            Time.timeScale = 1f;
            var level = SceneManager.GetSceneByName("GameLevel");
            if (!level.IsValid() || !level.isLoaded) yield break;
            var empty = SceneManager.CreateScene("EmptyArtScene_" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(level);
        }

        IEnumerator LoadLevel()
        {
            SceneManager.LoadScene("GameLevel");
            yield return null;
            yield return new WaitForFixedUpdate();
            player = Object.FindFirstObjectByType<PlayerController>();
            effects = Object.FindFirstObjectByType<EffectsManager>();
            audio = Object.FindFirstObjectByType<AudioManager>();
            Assert.IsNotNull(effects, "GameLevel debe tener EffectsManager");
            Assert.IsNotNull(audio, "GameLevel debe tener AudioManager");
            var actions = ScriptableObject.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            player.SetInputActions(actions);
            Object.FindFirstObjectByType<GameManager>().SetInputActions(actions);
            foreach (var s in Object.FindObjectsByType<BarrelSpawner>(FindObjectsSortMode.None))
            {
                s.SetSpawning(false);
                s.enabled = false;
            }
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
        }

        int ActiveEffects() => effects.GetComponentsInChildren<SpriteEffect>(false).Count(e => e.IsPlaying);

        [UnityTest]
        public IEnumerator Player_UsesFinalArt_AndAnimatedController()
        {
            yield return LoadLevel();
            var sr = player.GetComponentInChildren<SpriteRenderer>();
            Assert.IsNotNull(sr.sprite);
            StringAssert.DoesNotContain("PH_", sr.sprite.texture.name, "El jugador no debe usar el placeholder");
            var anim = player.GetComponentInChildren<Animator>();
            foreach (var clip in new[] { "Player_Idle", "Player_Run", "Player_Climb", "Player_Hurt" })
                Assert.IsTrue(anim.runtimeAnimatorController.animationClips.Any(c => c.name == clip), $"Falta el clip {clip}");
        }

        [UnityTest]
        public IEnumerator Collecting_SpawnsEffect_AndRequestsSound()
        {
            yield return LoadLevel();
            var c = Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None).First();
            Assert.IsTrue(c.Collect());
            yield return null;
            Assert.Greater(ActiveEffects(), 0, "Recoger debe mostrar un efecto");
            Assert.AreEqual("collect", audio.LastRequested);
        }

        [UnityTest]
        public IEnumerator Jumping_SpawnsDust_AndRequestsSound()
        {
            yield return LoadLevel();
            Assert.IsTrue(player.IsGrounded, "El jugador debe empezar en el suelo");
            Press(keyboard.spaceKey);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Release(keyboard.spaceKey);
            Assert.Greater(ActiveEffects(), 0, "Saltar debe levantar polvo");
            Assert.AreEqual("jump", audio.LastRequested);
        }

        [UnityTest]
        public IEnumerator Damage_SpawnsImpact_ShowsHurtPose_AndRequestsSound()
        {
            yield return LoadLevel();
            Assert.IsTrue(player.GetComponent<PlayerCollision>().TakeHit(player.transform.position.x - 1f));
            yield return null;
            yield return null;
            Assert.Greater(ActiveEffects(), 0, "El daño debe mostrar un impacto");
            Assert.AreEqual("damage", audio.LastRequested);
            var anim = player.GetComponentInChildren<Animator>();
            Assert.IsTrue(anim.GetCurrentAnimatorStateInfo(0).IsName("Hurt") || anim.GetNextAnimatorStateInfo(0).IsName("Hurt"),
                          "Durante el retroceso se muestra la pose de daño");
        }
    }
}
#endif
