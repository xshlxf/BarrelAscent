# Barrel Ascent — Handoff (estado al final de la Fase 7)

Leer esto primero. Detalle por fase en `Docs/PLAN.md` y `Docs/PHASE2.md` … `Docs/PHASE7.md`.

## Estado
- Fases 1–7 terminadas (git local, rama `master`, sin remoto). 37/37 pruebas PlayMode pasan.
- Siguiente: **Fase 8 — pruebas finales + build independiente (macOS)**.
- Unity **6000.4.0f1**, plantilla 2D, render pipeline **Built-in** (no URP), Input System (Project-wide Actions = `Assets/Settings/BarrelAscentControls.inputactions`), TextMeshPro esenciales importados.
- Resolución de referencia **640×360**, 32 PPU, tiles 32 px, Pixel Perfect Camera (`com.unity.2d.pixel-perfect`).

## Forma de trabajar (importante)
- Todo se construye con **constructores de editor reejecutables** en `Assets/Editor/` (menú *Barrel Ascent › Setup*), en batchmode:
  ```
  U=/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/MacOS/Unity
  $U -batchmode -quit -projectPath . -executeMethod BarrelAscent.EditorTools.Phase7Builder.Build -logFile <log>   # cadena completa
  $U -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>   # ~3 min, ejecutar en segundo plano
  ```
  Cadena: `Phase7Builder.Build` = arte → `Phase5Builder.Build` (→ 4 → 3: nivel + jugador + barriles + reglas) → arte en escena + EffectsManager + AudioManager → `Phase6Builder.Build` (UI). Ejecutar solo la Fase 5 deja el nivel sin el arte de la Fase 7; usar siempre la 7. Reejecutar la Fase 6 cambia fileIDs de `BarrelPixel.asset`/`MainMenu.unity` (ruido normal).
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

## Arte (Fase 7, hecho)
- Originales en `ArtSource/`; procesados con `Assets/Editor/Art/SpriteExtractor.cs` desde `Phase7Builder` (detalle y decisiones en `Docs/PHASE7.md`).
- Integrado: personaje animado (Idle/Run/Jump/Climb/Hurt), vigas y escalera del tileset, fondos por zona, efectos, meta con baliza, contorno en coleccionables.
- Sin usar: `character_GoingDown`, decoraciones (salvo la baliza).
- Audio: no hay archivos. `AudioManager` (Managers/AudioManager en GameLevel) tiene ranuras vacías para música y 8 efectos; basta con asignar clips en el Inspector (o en el builder si se quieren reconstruibles).
- Mantener la paleta del documento (#182B38, #354B55, #77858A, #875333, #D97732, #B83B36, #E6C66B, #D7D5C7, #101820) y 32 PPU / Point / sin compresión.

## Eventos disponibles para efectos/audio
`GameManager`: `ScoreChanged`, `LivesChanged`, `StateChanged`, `PointsAwarded`. `LevelManager`: `ZoneChanged`, `ZoneCompleted`, `GoalReached`. `PlayerCollision`: `Damaged`, `BarrelSmashed`. `BarrelController.AnyDestroyed` (estático). `Collectible.AnyCollected` (estático). `PlayerController`: `Jumped`, `Landed`.
