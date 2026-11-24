#if UNITY_EDITOR
using System.Collections;
using BarrelAscent.Environment;
using BarrelAscent.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace BarrelAscent.Tests
{
    /// <summary>
    /// Pruebas de la Fase 2 sobre el prefab real del jugador, con teclado simulado.
    /// Escenario propio: suelo (top y = 0), plataforma superior (top y = 3) y una escalera en x = 0.
    /// </summary>
    public class PlayerControllerTests : InputTestFixture
    {
        const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        const string ActionsPath = "Assets/Settings/BarrelAscentControls.inputactions";
        const float FloorTop = 0f;
        const float UpperTop = 3f;

        Keyboard keyboard;
        GameObject world;
        PlayerController player;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            if (world != null) Object.Destroy(world);
            if (player != null) Object.Destroy(player.gameObject);
            base.TearDown();
        }

        IEnumerator BuildWorld(Vector2 spawn, bool withUpperLevel = false)
        {
            world = new GameObject("TestWorld");
            int ground = LayerMask.NameToLayer("Ground");
            int ladderLayer = LayerMask.NameToLayer("Ladder");

            Box("Floor", new Vector2(0f, FloorTop - 0.5f), new Vector2(40f, 1f), ground, false);
            if (withUpperLevel)
            {
                Box("Upper", new Vector2(-3f, UpperTop - 0.5f), new Vector2(8f, 1f), ground, false);
                var ladder = Box("Ladder", new Vector2(0f, (FloorTop + UpperTop) * 0.5f + 0.25f),
                                 new Vector2(0.8f, UpperTop - FloorTop + 0.5f), ladderLayer, true);
                ladder.AddComponent<Ladder>();
            }

            // Instanciar inactivo para asignar las acciones antes de OnEnable.
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var go = Object.Instantiate(prefab, spawn, Quaternion.identity, holder.transform);
            player = go.GetComponent<PlayerController>();

            var actions = ScriptableObject.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            var so = new SerializedObject(player);
            so.FindProperty("inputActions").objectReferenceValue = actions;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.transform.SetParent(null);
            go.SetActive(true);
            Object.Destroy(holder);

            yield return WaitSeconds(0.5f); // asentarse en el suelo
        }

        GameObject Box(string name, Vector2 pos, Vector2 size, int layer, bool trigger)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(world.transform);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;
            box.isTrigger = trigger;
            return go;
        }

        static IEnumerator WaitSeconds(float s)
        {
            float end = Time.time + s;
            while (Time.time < end) yield return new WaitForFixedUpdate();
        }

        float Feet => player.GetComponent<CapsuleCollider2D>().bounds.min.y;

        [UnityTest]
        public IEnumerator Player_IsGrounded_AfterSpawn()
        {
            yield return BuildWorld(new Vector2(-5f, 0.2f));
            Assert.IsTrue(player.IsGrounded, "Debe estar apoyado en el suelo");
            Assert.AreEqual(FloorTop, Feet, 0.05f);
        }

        [UnityTest]
        public IEnumerator Player_MovesHorizontally_BothDirections()
        {
            yield return BuildWorld(new Vector2(-5f, 0.2f));
            float x0 = player.transform.position.x;

            Press(keyboard.dKey);
            yield return WaitSeconds(0.5f);
            Release(keyboard.dKey);
            float x1 = player.transform.position.x;
            Assert.Greater(x1 - x0, 2.0f, "Con D debe avanzar ~2.5 u en 0.5 s (velocidad 5)");
            Assert.IsTrue(player.FacingRight);

            Press(keyboard.leftArrowKey);
            yield return WaitSeconds(0.5f);
            Release(keyboard.leftArrowKey);
            Assert.Less(player.transform.position.x - x1, -2.0f, "Con flecha izquierda debe retroceder");
            Assert.IsFalse(player.FacingRight);
        }

        [UnityTest]
        public IEnumerator Player_Jumps_WithoutDoubleJump_AndCanJumpAgainAfterLanding()
        {
            yield return BuildWorld(new Vector2(-5f, 0.2f));

            // Salto + segundo intento en el aire.
            Press(keyboard.spaceKey);
            yield return WaitSeconds(0.1f);
            Release(keyboard.spaceKey);
            yield return WaitSeconds(0.15f);
            Press(keyboard.spaceKey);
            yield return WaitSeconds(0.05f);
            Release(keyboard.spaceKey);

            float peak = Feet;
            float end = Time.time + 1.2f;
            while (Time.time < end)
            {
                peak = Mathf.Max(peak, Feet);
                yield return new WaitForFixedUpdate();
            }

            // Altura teórica de un salto: v²/(2g) = 100 / (2 * 9.81 * 3) ≈ 1.70
            Assert.Greater(peak - FloorTop, 1.4f, "El salto debe elevar al jugador");
            Assert.Less(peak - FloorTop, 2.0f, "No debe existir doble salto");
            Assert.IsTrue(player.IsGrounded, "Debe aterrizar");

            // Tras aterrizar, puede volver a saltar.
            Press(keyboard.spaceKey);
            yield return WaitSeconds(0.2f);
            Release(keyboard.spaceKey);
            Assert.Greater(Feet - FloorTop, 1.0f, "Debe recuperar el salto al tocar el suelo");
        }

        [UnityTest]
        public IEnumerator Player_ClimbsUpAndDownLadder_WithoutJumping()
        {
            yield return BuildWorld(new Vector2(0f, 0.2f), withUpperLevel: true);

            Press(keyboard.wKey);
            yield return WaitSeconds(0.3f);
            Assert.IsTrue(player.IsClimbing, "W sobre la escalera debe iniciar la escalada");
            Assert.AreEqual(0f, player.Body.gravityScale, 1e-4f, "Sin gravedad al escalar");

            // Saltar durante la escalada no debe tener efecto.
            float before = Feet;
            Press(keyboard.spaceKey);
            yield return WaitSeconds(0.1f);
            Release(keyboard.spaceKey);
            Assert.Less(player.Body.linearVelocity.y, 5f, "No se puede saltar mientras se escala");
            Assert.Greater(Feet, before);

            yield return WaitSeconds(1.5f); // 3 u a velocidad 3 ≈ 1 s
            Release(keyboard.wKey);
            yield return WaitSeconds(0.3f);
            Assert.IsFalse(player.IsClimbing, "Debe salir arriba de la escalera");
            Assert.Greater(player.Body.gravityScale, 0f, "Gravedad restaurada");
            Assert.AreEqual(UpperTop, Feet, 0.08f, "Debe quedar de pie sobre la plataforma superior");
            Assert.IsTrue(player.IsGrounded);

            // Bajar.
            Press(keyboard.sKey);
            yield return WaitSeconds(0.3f);
            Assert.IsTrue(player.IsClimbing, "S en la parte superior debe iniciar el descenso");
            yield return WaitSeconds(1.5f);
            Release(keyboard.sKey);
            yield return WaitSeconds(0.3f);
            Assert.IsFalse(player.IsClimbing);
            Assert.AreEqual(FloorTop, Feet, 0.08f, "Debe quedar de pie en el suelo inferior");
            Assert.IsTrue(player.IsGrounded);
        }

        [UnityTest]
        public IEnumerator Player_StaysStill_WithoutInput()
        {
            yield return BuildWorld(new Vector2(-5f, 0.2f));
            Vector2 p0 = player.transform.position;
            yield return WaitSeconds(0.5f);
            Assert.Less(Vector2.Distance(p0, player.transform.position), 0.01f, "Sin entrada no hay deriva");
        }
    }
}
#endif
