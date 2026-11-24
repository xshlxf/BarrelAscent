#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using BarrelAscent.Enemies;
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
    /// Pruebas de la Fase 4 en un escenario propio:
    /// suelo (superficie y = 0) de x = −15 a 15 con paredes, y una plataforma superior
    /// (superficie y = 4) de x = −10 a 0, abierta por la derecha.
    /// </summary>
    public class BarrelTests : InputTestFixture
    {
        const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        const string BarrelPrefabPath = "Assets/Prefabs/Barrels/Barrel.prefab";
        const string ActionsPath = "Assets/Settings/BarrelAscentControls.inputactions";

        GameObject world;
        readonly List<Object> spawned = new List<Object>();

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            BuildWorld();
        }

        public override void TearDown()
        {
            foreach (var o in spawned) if (o != null) Object.Destroy(o is Component c ? c.gameObject : o);
            spawned.Clear();
            foreach (var b in Object.FindObjectsByType<BarrelController>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
            if (world != null) Object.Destroy(world);
            base.TearDown();
        }

        void BuildWorld()
        {
            world = new GameObject("BarrelTestWorld");
            int ground = LayerMask.NameToLayer("Ground");
            Box("Floor", new Vector2(0f, -0.5f), new Vector2(30f, 1f), ground);
            Box("Upper", new Vector2(-5f, 3.5f), new Vector2(10f, 1f), ground);
            Box("WallL", new Vector2(-15.5f, 5f), new Vector2(1f, 12f), ground);
            Box("WallR", new Vector2(15.5f, 5f), new Vector2(1f, 12f), ground);
        }

        void Box(string name, Vector2 pos, Vector2 size, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(world.transform);
            go.transform.position = pos;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        BarrelController SpawnBarrel(float x, float surfaceY, float speed, int dir)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
            var b = Object.Instantiate(prefab).GetComponent<BarrelController>();
            b.transform.position = new Vector3(x, surfaceY + b.Radius + 0.02f, 0f);
            b.Launch(speed, dir);
            spawned.Add(b);
            return b;
        }

        PlayerController SpawnPlayer(Vector2 feet)
        {
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath), feet, Quaternion.identity, holder.transform);
            var pc = go.GetComponent<PlayerController>();
            var so = new SerializedObject(pc);
            so.FindProperty("inputActions").objectReferenceValue =
                ScriptableObject.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            so.ApplyModifiedPropertiesWithoutUndo();
            go.transform.SetParent(null);
            go.SetActive(true);
            Object.Destroy(holder);
            spawned.Add(pc);
            return pc;
        }

        static IEnumerator WaitSeconds(float s)
        {
            float end = Time.time + s;
            while (Time.time < end) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Barrel_StartsSpawned_ThenRollsInLaunchDirection()
        {
            var b = SpawnBarrel(-12f, 0f, 2f, +1);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(BarrelController.State.Spawned, b.CurrentState);

            yield return WaitSeconds(0.4f);
            Assert.AreEqual(BarrelController.State.Rolling, b.CurrentState);
            float x0 = b.transform.position.x;
            Quaternion r0 = b.transform.GetChild(0).rotation;
            yield return WaitSeconds(1f);
            Assert.AreEqual(2f, b.transform.position.x - x0, 0.3f, "Velocidad de zona 1 = 2 u/s hacia la derecha");
            Assert.AreNotEqual(r0, b.transform.GetChild(0).rotation, "El sprite debe girar al rodar");
        }

        [UnityTest]
        public IEnumerator Barrel_FallsAtPlatformEnd_LandsBelow_AndRollsBack()
        {
            var b = SpawnBarrel(-2f, 4f, 2f, +1);
            var seen = new List<BarrelController.State>();
            float end = Time.time + 4f;
            while (Time.time < end)
            {
                if (seen.Count == 0 || seen[seen.Count - 1] != b.CurrentState) seen.Add(b.CurrentState);
                yield return new WaitForFixedUpdate();
            }

            CollectionAssert.AreEqual(
                new[] { BarrelController.State.Spawned, BarrelController.State.Rolling, BarrelController.State.Falling, BarrelController.State.Rolling },
                seen, "Secuencia de estados esperada");
            Assert.AreEqual(-1, b.Direction, "Tras caer debe rodar en sentido contrario");
            Assert.AreEqual(b.Radius, b.transform.position.y, 0.06f, "Debe quedar rodando sobre el suelo inferior");
            Assert.Less(b.transform.position.x, 0.5f, "Ha vuelto hacia la izquierda por debajo de la plataforma");
        }

        [UnityTest]
        public IEnumerator Barrel_DespawnsAgainstWall_WithoutReward()
        {
            bool? byPlayer = null;
            void Handler(BarrelController _, bool p) => byPlayer = p;
            BarrelController.AnyDestroyed += Handler;
            try
            {
                var b = SpawnBarrel(13.5f, 0f, 2f, +1);
                yield return WaitSeconds(2f);
                Assert.IsTrue(b == null, "El barril debe desaparecer al chocar con la pared");
                Assert.AreEqual(false, byPlayer, "Sin recompensa");
            }
            finally { BarrelController.AnyDestroyed -= Handler; }
        }

        [UnityTest]
        public IEnumerator Barrel_SideHit_DamagesOnce_ThenInvulnerability_ThenCanBeHitAgain()
        {
            var player = SpawnPlayer(new Vector2(3f, 0.05f));
            var collision = player.GetComponent<PlayerCollision>();
            int damage = 0;
            collision.Damaged += () => damage++;
            yield return WaitSeconds(0.3f);

            // Primer impacto hacia t ≈ 1.1 s; el segundo barril llega ≈ 0.35 s después.
            SpawnBarrel(0f, 0f, 3f, +1);
            SpawnBarrel(-1f, 0f, 3f, +1);
            yield return WaitSeconds(2.0f);
            Assert.AreEqual(1, damage, "Solo un daño durante la invulnerabilidad");
            Assert.IsTrue(collision.IsInvulnerable);

            yield return WaitSeconds(1.2f);
            Assert.IsFalse(collision.IsInvulnerable, "La invulnerabilidad dura 1.5 s");
            float px = player.transform.position.x;
            SpawnBarrel(px - 3f, 0f, 3f, +1);
            yield return WaitSeconds(1.5f);
            Assert.AreEqual(2, damage, "Pasada la invulnerabilidad vuelve a recibir daño");
        }

        [UnityTest]
        public IEnumerator Player_LandsOnBarrel_DestroysIt_Bounces_AndTakesNoDamage()
        {
            var b = SpawnBarrel(5f, 0f, 0f, +1); // barril quieto
            yield return WaitSeconds(0.4f);

            var player = SpawnPlayer(new Vector2(5f, 3f));
            var collision = player.GetComponent<PlayerCollision>();
            int damage = 0, smashed = 0;
            bool? byPlayer = null;
            float maxUpVelocity = 0f;
            collision.Damaged += () => damage++;
            collision.BarrelSmashed += _ => smashed++;
            void Handler(BarrelController _, bool p) => byPlayer = p;
            BarrelController.AnyDestroyed += Handler;
            try
            {
                float end = Time.time + 1.5f;
                while (Time.time < end)
                {
                    if (smashed > 0) maxUpVelocity = Mathf.Max(maxUpVelocity, player.Body.linearVelocity.y);
                    yield return new WaitForFixedUpdate();
                }

                Assert.AreEqual(1, smashed, "Debe destruir el barril exactamente una vez");
                Assert.AreEqual(true, byPlayer, "La destrucción cuenta como del jugador");
                Assert.Greater(maxUpVelocity, 5f, "Debe recibir un impulso vertical");
                Assert.AreEqual(0, damage, "Aterrizar encima no causa daño");
                Assert.IsTrue(b == null || !b.IsDangerous, "El barril destruido ya no es peligroso");

                yield return WaitSeconds(1f);
                Assert.AreEqual(0, damage, "El barril destruido no vuelve a causar daño");
                Assert.IsTrue(b == null, "El barril destruido desaparece");
            }
            finally { BarrelController.AnyDestroyed -= Handler; }
        }

        [UnityTest]
        public IEnumerator Spawner_SpawnsAtZoneInterval_OnlyWhileActive()
        {
            var zone = ScriptableObject.CreateInstance<ZoneConfig>();
            zone.barrelSpeed = 2.75f;
            zone.spawnInterval = 0.5f;
            zone.maxBarrels = 10;

            var go = new GameObject("Spawner");
            go.transform.position = new Vector3(-12f, 0f, 0f);
            go.SetActive(false);
            var spawner = go.AddComponent<BarrelSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("barrelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath).GetComponent<BarrelController>();
            so.FindProperty("zone").objectReferenceValue = zone;
            so.FindProperty("firstSpawnDelay").floatValue = 0.1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
            spawned.Add(go);

            yield return WaitSeconds(1f);
            Assert.AreEqual(0, spawner.AliveCount, "Inactivo: no genera barriles");

            spawner.SetSpawning(true);
            yield return WaitSeconds(1.2f);
            Assert.That(spawner.AliveCount, Is.InRange(2, 3), "Uno cada 0.5 s");
            foreach (var b in Object.FindObjectsByType<BarrelController>(FindObjectsSortMode.None))
                Assert.AreEqual(2.75f, b.Speed, 1e-4f, "Usa la velocidad de la zona");

            spawner.SetSpawning(false);
            int count = spawner.AliveCount;
            yield return WaitSeconds(1f);
            Assert.AreEqual(count, spawner.AliveCount, "Detenido: no genera más");
        }
    }
}
#endif
