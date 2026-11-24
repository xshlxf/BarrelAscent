# Barrel Ascent — Plan de implementación

## 1. Arquitectura resumida

| Script | Responsabilidad | Depende de |
|---|---|---|
| `GameManager` (singleton de escena) | Estado (Playing/Paused/Won/Lost), vidas, puntuación, pausa, reinicio. Expone eventos C# (`ScoreChanged`, `LivesChanged`, `StateChanged`). | — |
| `PlayerController` | Movimiento, salto, suelo (OverlapBox), escaleras (gravedad 0), parámetros del Animator. | Input Actions |
| `PlayerCollision` | Daño lateral, invulnerabilidad, reaparición, pisotón + rebote. | GameManager, PlayerController |
| `BarrelController` | FSM SPAWNED→ROLLING→FALLING→DESTROYED, raycast de suelo/borde. | — |
| `BarrelSpawner` | Intervalo y velocidad según zona activa. | LevelManager |
| `Collectible` | Trigger → `GameManager.AddScore(100)`, una sola vez. | GameManager |
| `LevelManager` | Zona actual (por triggers de zona), bonus de zona (250, una vez), meta (1000 → victoria). | GameManager |
| `CameraFollow` | Seguimiento vertical suavizado y limitado (minY/maxY). | — |
| `UIManager` | HUD (TMP), paneles Pausa / Game Over / Victoria; escucha eventos del GameManager. | GameManager |
| `MainMenu` | Botones Play / Exit. | — |

Comunicación: los sistemas **notifican** al GameManager; la UI **escucha** eventos. Ningún script de gameplay toca la UI directamente.

Datos de balance por zona en un `ScriptableObject` `ZoneConfig` (velocidad barril, intervalo) → ajustable sin tocar código.

## 2. Dependencias / paquetes (Unity 6000.4.0f1)
- `com.unity.feature.2d` (Tilemap, Sprite, 2D Pixel Perfect, Aseprite/PSD importers).
- `com.unity.inputsystem` — Active Input Handling = Input System Package.
- `com.unity.ugui` + TextMeshPro (incluido en uGUI en Unity 6).
- Render pipeline integrado (Built-in) de la plantilla 2D → componente **Pixel Perfect Camera** de `com.unity.2d.pixel-perfect`.

## 3. Decisiones pendientes de confirmar
1. Render pipeline: Built-in 2D (plantilla por defecto), sin URP.
2. Resolución: **640x360** (confirmado). 20x11,25 tiles visibles; x3 exacto en 1080p.
3. Arte: recursos en `ArtSource/` (copiados del Escritorio), integrados en las Fases 4–7. Sin audio en el paquete.
4. Control de versiones: git local en la carpeta del proyecto (sin remoto).

## 4. Estructura inicial
Ver sección 16 del documento maestro — creada tal cual bajo `Assets/`, más `Assets/Settings/` (Input Actions, ZoneConfig).

## 5. Tareas pequeñas (orden recomendado → versión jugable rápida)
1. F1: proyecto, paquetes, carpetas, escenas, Input Actions, git. ✅
2. F2: PlayerController (mover/saltar/suelo) sobre una plataforma de prueba. ✅ (ver PHASE2.md)
3. F3a: Tilemap de Zona 1 + CameraFollow. → **primer jugable** ✅ (ver PHASE3.md)
4. F4a: Barril (FSM + bordes) + daño + pisotón. → **prototipo con barril** ✅ (ver PHASE4.md)
5. F2b: escaleras. ✅ (adelantado a la Fase 2)
6. F4b: spawner + ZoneConfig. ✅
7. F5: coleccionables, puntuación, vidas, reaparición, LevelManager, meta. ✅ (ver PHASE5.md)
8. F3b: Zonas 2 y 3. ✅ (adelantado a la Fase 3)
9. F6: menú, HUD, pausa, game over, victoria, reinicio. ✅ (ver PHASE6.md)
10. F7: arte/audio definitivos. ✅ (ver PHASE7.md; audio preparado sin clips)
11. F8: pruebas finales + build independiente (macOS). ✅ (ver PHASE8.md)

## 6. Trabajo paralelizable
- Generación de arte (IA) en paralelo a todo el código.
- `UIManager`/MainMenu en paralelo a barriles (solo dependen de los eventos de GameManager).
- Diseño de las Zonas 2–3 en paralelo a F5.

## 7. Recursos visuales por fase
| Fase | Recursos |
|---|---|
| F2 | Sprite trabajador: idle, run (6–8 f), jump, climb (4 f). 32x32. |
| F3 | Tileset industrial 32x32 (plataforma, viga, escalera), fondos ×3 zonas. |
| F4 | Barril: roll (4 f), destrucción (3–4 f). |
| F5 | Coleccionable (llave inglesa/tuerca, 4 f), bandera/meta. |
| F6 | Fuente pixel, paneles, iconos de vida, logo. |
| F7 | Partículas (polvo, chispa), SFX, música. |
