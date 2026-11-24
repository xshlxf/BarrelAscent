# Barrel Ascent — Handoff (estado al final de la Fase 6)

Leer esto primero. Detalle por fase en `Docs/PLAN.md` y `Docs/PHASE2.md` … `Docs/PHASE6.md`.

## Estado
- Fases 1–6 terminadas y confirmadas (git local, rama `master`, sin remoto). 33/33 pruebas PlayMode pasan.
- Siguiente: **Fase 7 — arte y audio** (luego Fase 8: pruebas finales + build independiente).
- Unity **6000.4.0f1**, plantilla 2D, render pipeline **Built-in** (no URP), Input System (Project-wide Actions = `Assets/Settings/BarrelAscentControls.inputactions`), TextMeshPro esenciales importados.
- Resolución de referencia **640×360**, 32 PPU, tiles 32 px, Pixel Perfect Camera (`com.unity.2d.pixel-perfect`).

## Forma de trabajar (importante)
- Todo se construye con **constructores de editor reejecutables** en `Assets/Editor/` (menú *Barrel Ascent › Setup*), en batchmode:
  ```
  U=/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/MacOS/Unity
  $U -batchmode -quit -projectPath . -executeMethod BarrelAscent.EditorTools.Phase5Builder.Build -logFile <log>
  $U -batchmode -quit -projectPath . -executeMethod BarrelAscent.EditorTools.Phase6Builder.Build -logFile <log>
  $U -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>   # ~3 min, ejecutar en segundo plano
  ```
  Cadena: `Phase5Builder.Build` → `Phase4Builder.Build` → `Phase3Builder.Build` (nivel + jugador + barriles + reglas). `Phase6Builder.Build` solo rehace la UI de ambas escenas. Tras reconstruir el nivel hay que volver a ejecutar la Fase 6 (la UI referencia GameManager/LevelManager).
- Batchmode falla si el usuario tiene Unity abierto con el proyecto (`Temp/UnityLockfile`): pedirle que lo cierre.
- Los comandos de Unity necesitan `dangerouslyDisableSandbox: true`.
- Capturas para revisar visualmente: `ScenePreview.Capture`, `CaptureFullLevel`, `CaptureUi` → `Docs/Screenshots/`.
- Commit por fase cuando el usuario lo aprueba; mensajes en inglés con `Co-Authored-By`. Docs y comentarios en español.

## Trampas ya encontradas
- `EditorSceneManager.OpenScene` descarga assets sin referencias → cargar assets **después** de abrir la escena.
- `AssetDatabase.ImportPackage` no termina en batchmode → usar el flag `-importPackage`.
- `SetNativeSize` en el editor da tamaños ×3 (Canvas aún sin `referencePixelsPerUnit` 32) → tamaño = `sprite.rect.size`.
- Con `Time.timeScale = 0` (pausa/fin) las esperas por `WaitForFixedUpdate` nunca terminan; los helpers de prueba ya fallan en ese caso. Las pruebas de nivel desactivan GameManager y la meta.
- `InputSystem.actions` no se puede asignar en Play; las pruebas usan `PlayerController.SetInputActions` / `GameManager.SetInputActions`.
- `InputSystemUIInputModule.AssignDefaultActions` falla en batchmode → se usan las referencias del asset `DefaultInputActions` del paquete.

## Arte (Fase 7)
- Originales en `ArtSource/` (copiados de `~/Desktop/BarrelAscent_Assets`). Son pixel art de IA a alta resolución (1 píxel de arte ≈ 8–10 px), fondos blancos/transparentes/negros, sin rejilla.
- Herramienta: `Assets/Editor/Art/SpriteExtractor.cs` (`Load` quita fondo por color de esquina y separa fotogramas por columnas vacías; `Downscale`; `Resize` para fondos).
- **Ya integrados**: barril (`Assets/Sprites/Objects/Barrel`), coleccionables, UI (`Assets/UI/Sprites`), fondo del menú, fuente pixel propia `BarrelPixel` (`PixelFontBuilder`).
- **Pendiente**:
  - Personaje (`ArtSource/01_Character`: idle, walk L→R/R→L, jump, climb, GoingDown, damage; 6 fotogramas por tira, alturas 331–424 px → alinear pies). Sustituye `PH_Player`. El Animator `Assets/Animations/Player/Player.controller` ya tiene estados Idle/Run/Jump/Climb sin clips y parámetros `Speed`, `Grounded`, `Climbing`, `ClimbSpeed`, `VelocityY`. Collider del jugador: cápsula 0.6×0.94, pivote en los pies. El personaje real lleva gorra roja y mono azul.
  - Tileset (`02_Environment/factory_tileset.png`, 2400×1792, sin rejilla: recortar por rectángulos) → sustituir `PH_Platform` (tile) y `PH_Ladder`. Las plataformas son de 1 tile de alto.
  - Fondos por zona (`factory_background_zone_01..03.jpg`) → sustituir los paneles `PH_WallPanel` con tinte (`Level/Backgrounds`); fondo vertical de 46 tiles, considerar parallax suave o mosaico.
  - Decoraciones (`factory_decorations.png`), efectos (`05_Effects`: polvo, explosión, chispas, impacto) para salto/aterrizaje, barril destruido, recogida, daño.
  - Audio: no hay recursos de audio en el paquete; pedir al usuario o dejar el sistema preparado (AudioManager simple + eventos ya existentes).
  - La bandera de meta es placeholder (`PH_GoalFlag`); el coleccionable "componente" se ve poco en la zona 3.
- Mantener la paleta del documento (#182B38, #354B55, #77858A, #875333, #D97732, #B83B36, #E6C66B, #D7D5C7, #101820) y 32 PPU / Point / sin compresión.

## Eventos disponibles para efectos/audio
`GameManager`: `ScoreChanged`, `LivesChanged`, `StateChanged`, `PointsAwarded`. `LevelManager`: `ZoneChanged`, `ZoneCompleted`, `GoalReached`. `PlayerCollision`: `Damaged`, `BarrelSmashed`. `BarrelController.AnyDestroyed` (estático). `Collectible.Collect()`.
