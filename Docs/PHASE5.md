# Fase 5 — Interacciones y reglas de juego

## Objetivo
Coleccionables, puntuación, vidas, reaparición, progresión por zonas, dificultad y meta final,
con un GameManager que centraliza el estado de la partida (incluidos pausa, fin y reinicio).

## Archivos
| Archivo | Qué hace |
|---|---|
| `Assets/Scripts/Managers/GameManager.cs` | Estado (Playing/Paused/Won/Lost), vidas, puntuación, pausa (ESC), reinicio (R tras terminar), reaparición por caída. |
| `Assets/Scripts/Managers/LevelManager.cs` | + bonus por zona completada (una vez), punto de reaparición por zona, meta → victoria. |
| `Assets/Scripts/Environment/Collectible.cs` | Trigger: +100 una sola vez, animación de flotación y de recogida. |
| `Assets/Scripts/Environment/Goal.cs` | Trigger de meta → `LevelManager.ReachGoal()`. |
| `Assets/Editor/Phase5Builder.cs` | Arte de coleccionables, bandera, prefabs, reapariciones y GameManager. |
| `Assets/Prefabs/Collectibles/Collectible.prefab` · `Assets/Prefabs/Environment/Goal.prefab` | Prefabs. |
| `Assets/Sprites/Objects/Collectibles/*.png` | Engranaje, componente y cápsula (de `ArtSource/04_Collectibles`, 20 px). |
| `Assets/Sprites/Placeholders/PH_GoalFlag.png` | Bandera de meta (no hay recurso de meta en el paquete de arte). |
| `Assets/Tests/PlayMode/GameRulesTests.cs` | 9 pruebas de reglas sobre GameLevel. |

## Reglas implementadas
| Evento | Efecto | Dónde |
|---|---|---|
| Recoger coleccionable | +100 (una vez; el collider se desactiva) | Collectible → GameManager.AddScore |
| Destruir barril (pisotón) | +50 | PlayerCollision.BarrelSmashed → GameManager |
| Entrar por primera vez en la zona 2 / 3 | +250 por la zona superada | LevelManager |
| Tocar la meta | +250 (zona 3) + 1000 (cima) → **Victoria** | Goal → LevelManager → GameManager.Win |
| Golpe lateral de barril | −1 vida, retroceso, 1.5 s invulnerable | PlayerCollision.Damaged → GameManager |
| Caer por debajo de y = −3 | −1 vida y reaparece en el punto seguro de la zona más alta alcanzada | GameManager |
| 0 vidas | **Game Over** | GameManager.Lose |
| ESC | Pausa / reanuda (timeScale 0, sin control) | GameManager |
| R (solo tras victoria o derrota) | Recarga GameLevel desde cero | GameManager.Restart |

Victoria y derrota: se bloquea el control, se detienen los spawners y `Time.timeScale = 0`.
La puntuación solo cambia en estado Playing, así ninguna recompensa se registra dos veces ni después de terminar.
Una partida completa sin coleccionables ni barriles da 1750 puntos; con todo, 1750 + 10 × 100 + 50 por barril.

**Decisión de diseño**: un golpe de barril no teletransporta al jugador (retroceso + invulnerabilidad,
estilo arcade continuo); la reaparición en un punto seguro se usa al caer fuera del nivel.
Si se prefiere reaparecer también tras cada golpe, basta con `LoseLife(respawn: true)` en `OnPlayerDamaged`.

Puntos de reaparición (`Level/Markers/Respawn_Z1..3`): inicio del nivel y lo alto de la primera escalera
de las zonas 2 y 3.

## Eventos para la interfaz (Fase 6)
`GameManager`: `ScoreChanged(int)`, `LivesChanged(int)`, `StateChanged(GameState)`, `PointsAwarded(int, string)`.
`LevelManager`: `ZoneChanged(int)`, `ZoneCompleted(int)`, `GoalReached`.
Métodos para botones: `Pause()`, `Resume()`, `Restart()`, `LoadMainMenu()`.

## Inspector
- **Managers/GameManager**: Starting Lives 3 · Barrel Points 50 · Respawn Invulnerability 1.5 ·
  Fall Limit Y −3 · Player, Player Collision, Level Manager, Camera Follow · Main Menu Scene "MainMenu".
- **Managers/LevelManager**: + Respawn Points (Z1..Z3) · Zone Completion Points 250 · Goal Points 1000.
- **Collectible**: CircleCollider2D trigger r 0.35 · Points 100. **Goal**: BoxCollider2D trigger 1×2.

Regenerar: **Barrel Ascent › Setup › Build Phase 5 (Game Rules)** (reconstruye también las fases 3–4).

## Errores frecuentes
| Síntoma | Causa / solución |
|---|---|
| Los coleccionables no se recogen | El jugador necesita Rigidbody2D y el coleccionable un collider *Is Trigger*. |
| La puntuación no cambia | Falta el GameManager en la escena (`GameManager.Instance` es null). |
| El juego queda congelado al volver al menú | `Time.timeScale` debe volver a 1 (lo hacen `Restart`, `LoadMainMenu` y `OnDestroy`). |
| La recompensa de zona se repite | Solo se concede al superar `HighestZoneReached`; no modificarlo desde fuera. |

## Pruebas — 28/28 pasan
Nuevas (`GameRulesTests`, sobre GameLevel):
1. Empieza con 3 vidas, 0 puntos, en Playing; hay 10 coleccionables y una meta.
2. Coleccionable: +100 una sola vez, un único aviso de puntuación, desaparece.
3. Pisotón de barril: +50 sin perder vidas.
4. Daño: −1 vida, sin daño repetido durante la invulnerabilidad; tras 3 golpes → Game Over, tiempo detenido, sin control, spawners parados.
5. Caer fuera del nivel: −1 vida y reaparición en el punto seguro de la zona alcanzada, con invulnerabilidad.
6. Progresión: +250 al completar la zona 1 y la 2, sin repetir al volver a entrar.
7. Meta: victoria con 250 × 3 + 1000 = 1750, tiempo detenido, sin control ni spawners; no puntúa dos veces.
8. ESC pausa (el jugador no se mueve) y reanuda.
9. R no hace nada durante la partida; tras terminar recarga la escena con 0 puntos y 3 vidas.

Además, todas las esperas de las pruebas fallan de inmediato si el tiempo está detenido, en lugar de bloquear la ejecución.

**Manual**: Play en GameLevel. Hasta la Fase 6 no hay HUD; la puntuación y las vidas se ven en el
Inspector del GameManager (modo Debug) o con los eventos.

## Criterios de finalización
Criterios del documento maestro: 12 coleccionables ✅ · 13 vidas ✅ · 14 invulnerabilidad ✅ ·
15 tres zonas accesibles ✅ (Fase 3) · 16 dificultad progresiva ✅ · 17 meta → victoria ✅ ·
18 tres vidas perdidas → Game Over ✅ · 19 pausa ✅ (lógica; panel en la Fase 6) · 20 reinicio ✅ (tecla R; botones en la Fase 6).

Observación para la Fase 7: el coleccionable "componente" es oscuro y se distingue poco sobre los fondos de la zona 3.
