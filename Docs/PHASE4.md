# Fase 4 — Barriles

## Objetivo
Barriles autónomos que aparecen por zona, ruedan, detectan el final de la plataforma, caen al piso
inferior y siguen en zigzag; dañan al jugador de lado y se destruyen si el jugador aterriza encima.

## Archivos
| Archivo | Qué hace |
|---|---|
| `Assets/Scripts/Enemies/BarrelController.cs` | Máquina de estados, movimiento, bordes, paredes, destrucción. |
| `Assets/Scripts/Enemies/BarrelSpawner.cs` | Aparición periódica con la velocidad/frecuencia de su zona. |
| `Assets/Scripts/Environment/ZoneConfig.cs` | ScriptableObject de dificultad por zona. |
| `Assets/Scripts/Managers/LevelManager.cs` | Zona actual del jugador → activa solo el spawner de esa zona. (La Fase 5 añade bonus y meta.) |
| `Assets/Scripts/Player/PlayerCollision.cs` | Daño lateral, invulnerabilidad con parpadeo, pisotón y rebote. |
| `Assets/Scripts/Player/PlayerController.cs` | Añadidos `Bounce()` y `Knockback()`. |
| `Assets/Editor/Art/SpriteExtractor.cs` | Convierte el arte de IA de `ArtSource/` a sprites de 32 PPU. |
| `Assets/Editor/Phase4Builder.cs` | Arte del barril, ZoneConfig, prefab, capas y escena. |
| `Assets/Prefabs/Barrels/Barrel.prefab` | Barril listo para usar. |
| `Assets/Settings/Zones/Zone1..3.asset` | Velocidad 2 / 2.75 / 3.5 · cada 4 / 3 / 2 s · máx. 6 vivos. |
| `Assets/Sprites/Objects/Barrel/*.png` | Sprites reales del barril (rodando 28×26, cayendo, destruido). |
| `Assets/Tests/PlayMode/BarrelTests.cs` | 6 pruebas nuevas + 2 en `GameLevelTests.cs`. |

## Máquina de estados del barril
```
SPAWNED ──(0.3 s y apoyado)──▶ ROLLING ──(sin suelo bajo el centro)──▶ FALLING
                                  ▲                                         │
                                  └──(aterriza: invierte el sentido)────────┘
cualquier estado ──▶ DESTROYED   (pisotón del jugador · pared/escalón delante ·
                                   atascado 1 s · cae bajo el nivel · 90 s de vida)
```
- **Suelo**: `OverlapBox` estrecho bajo el centro (capa Ground). Se cae en cuanto el centro sobrepasa el borde.
- **Pared**: `Raycast` horizontal de 0.06 u desde el borde del círculo → el barril termina su recorrido.
- **Caída**: la gravedad del Rigidbody2D; el código solo mantiene la mitad de la velocidad horizontal.
- **DESTROYED**: se desactiva el collider (ya no puede dañar), se lanza `BarrelController.AnyDestroyed`
  una única vez (`byPlayer` = true solo con pisotón; la Fase 5 sumará 50 puntos) y desaparece.
- La rotación del sprite se calcula a partir de la velocidad (rueda sin deslizar); el Rigidbody2D no gira.

## Recorrido en el nivel
Cada spawner rueda hacia el extremo más lejano de su plataforma (calculado del layout). Los extremos
abiertos alternos hacen que el barril baje en zigzag:
- Zona 1: F3 → F2 → F1 → suelo → pared.
- Zona 2: F7 → F6 → F5 → hueco → F4 → F3 → … → suelo.
- Zona 3: F10 (junto a la meta, hacia la izquierda) → F8 → F7 → pared.
El punto de aparición de la zona 3 se movió a la columna 9 para que el barril no choque con la
esquina de la plataforma F9 al caer.

## Jugador ⇄ barril
Capas: Barrel ignora Barrel, Player y Ladder en la matriz de colisiones 2D, así que los barriles no
empujan al jugador ni chocan entre sí. `PlayerCollision` consulta cada paso físico qué barriles
solapan con su cápsula:
- **Pisotón**: pies por encima del centro del barril, sin estar subiendo rápido ni escalando →
  `Smash()` + rebote a 9 u/s (el rebote no da un salto extra).
- **Impacto lateral**: daño → retroceso (3, 5) con 0.25 s sin control, sale de la escalera,
  invulnerable 1.5 s con parpadeo. Lanza `Damaged` (la Fase 5 restará la vida y gestionará la reaparición).

## Inspector
- **Barrel**: Rigidbody2D Dynamic (gravedad 3, sin rotación, Interpolate, Continuous, NoFriction) ·
  CircleCollider2D r = 0.4 · BarrelController (Ground Mask = Ground, sprites, hijo Visual) · capa Barrel.
- **Player**: + PlayerCollision (Barrel Mask = Barrel, Invulnerability 1.5, Bounce 9).
- **BarrelSpawn_Z1..3** (en `Level/Markers`): BarrelSpawner (Barrel prefab, Zone1..3, Direction).
- **Managers/LevelManager**: Player, Zone Start Y = 0 / 14 / 30, Zone Spawners = Z1..Z3.

Para regenerar todo: **Barrel Ascent › Setup › Build Phase 4 (Barrels)** (también reconstruye el nivel).

## Pruebas — 18/18 pasan
Nuevas:
1. Aparece en SPAWNED y luego rueda a 2 u/s en su sentido, girando el sprite.
2. Secuencia SPAWNED → ROLLING → FALLING → ROLLING, aterriza abajo e invierte el sentido.
3. Desaparece contra una pared sin recompensa.
4. Impacto lateral: un solo daño aunque lleguen dos barriles seguidos; tras 1.5 s vuelve a poder recibir daño.
5. Aterrizar encima: destruye el barril una vez, rebote > 5 u/s, sin daño, el barril no vuelve a dañar y desaparece.
6. El spawner respeta el intervalo y la velocidad de su zona y solo genera mientras está activo.
7. (GameLevel) El LevelManager activa solo el spawner de la zona del jugador; velocidad y frecuencia aumentan por zona.
8. (GameLevel) Un barril de cada zona baja al menos dos pisos y termina su recorrido sin atascarse.

**Manual**: Play en GameLevel → esperar al primer barril (~3 s), saltarlo, recibir un golpe
(parpadeo) y caer encima de otro (rebote).

## Errores frecuentes
| Síntoma | Causa / solución |
|---|---|
| El barril empuja al jugador | Falta ignorar Barrel–Player en Project Settings › Physics 2D › Layer Collision Matrix. |
| El barril flota al llegar al borde | Ground Mask vacío o caja de suelo demasiado ancha. |
| Daño repetido en cada frame | Invulnerabilidad a 0 o PlayerCollision duplicado. |
| El barril se queda en una esquina | Revisa el layout: no debe haber una plataforma justo bajo el borde de caída. |
| Barriles en todas las zonas a la vez | El LevelManager no tiene asignados los spawners o el jugador. |

## Criterios de finalización
Criterios 7–11 del documento maestro: aparecen solos ✅ · ruedan y caen ✅ · dañan ✅ ·
se destruyen al aterrizar encima ✅ · rebote ✅. Criterio 14 (invulnerabilidad) ✅. La dificultad por zona ya
está activa (criterio 16, se completará con la progresión de la Fase 5).

---

## Revisión de los recursos de `BarrelAscent_Assets`
Copiados a `ArtSource/` (fuera de `Assets/`, para que Unity no importe los originales de 21 MB).

**Estado general**: estilo pixel art coherente entre sí, pero generado a alta resolución
(cada "píxel" de arte ≈ 8–10 px reales), con fondos blancos o transparentes y fotogramas sin
rejilla. Por eso no se pueden importar tal cual a 32 PPU: hay que quitar el fondo, separar los
fotogramas y reducirlos. `SpriteExtractor` lo hace (ya usado para el barril).

| Carpeta | Contenido | Notas para la Fase 7 |
|---|---|---|
| 01_Character | idle, walk (izq./der.), jump, climb, GoingDown, damage, referencia | 6 fotogramas por tira. Solo hace falta una dirección de caminar (la otra con `flipX`). Las alturas de las tiras varían (331–424 px): habrá que alinear los pies. El personaje lleva **gorra roja y mono azul**, no el casco amarillo/mono naranja del placeholder. |
| 02_Environment | tileset, decoraciones, 3 fondos JPG 2752×1536 | El tileset no está en rejilla: se recortará por rectángulos. Incluye vigas con franjas de advertencia, escaleras, tuberías y maquinaria. Los fondos se reducirán a ~640×360 (artefactos JPG). |
| 03_Enemies | barril rodando (4 vistas), cayendo, destruido, referencia | Las 4 vistas no forman un ciclo de rodadura; se usa la vista frontal y se gira por código. **Integrado.** |
| 04_Collectibles | engranaje, componente, cápsula | Un fotograma cada uno. |
| 05_Effects | polvo, explosión, chispas, impacto | Tiras de fotogramas. |
| 06_UI | panel grande/pequeño, corazón, botones pausa/reanudar/repetir/salir | Para HUD y menús (Fase 6). |
| 07_References | vacía | — |
