# Fase 8 — Pruebas finales y build independiente (macOS)

## Objetivo
Cerrar el proyecto: pruebas de extremo a extremo y de resistencia, comprobaciones estáticas del
proyecto y un ejecutable de macOS que funcione sin el editor.

## Cómo se construye y se prueba
```
U=/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/MacOS/Unity
$U -batchmode -quit -projectPath . -executeMethod BarrelAscent.EditorTools.BuildScript.BuildMacOS -logFile <log>
$U -batchmode -projectPath . -runTests -testPlatform EditMode -testResults <xml> -logFile <log>   # segundos
$U -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>   # ~4 min
```
Resultado: `Builds/macOS/Barrel Ascent.app` (113 MB, Intel + Apple silicon, Mono). `Builds/` está en `.gitignore`.
También desde el menú *Barrel Ascent › Build › macOS*.

## Archivos
| Archivo | Qué hace |
|---|---|
| `Assets/Editor/BuildScript.cs` | Ajustes del reproductor y build de macOS; en batchmode sale con código 1 si falla. |
| `Assets/Tests/PlayMode/FinalTests.cs` | Sesión completa y prueba de resistencia (2 pruebas). |
| `Assets/Tests/EditMode/ProjectChecks.cs` | Comprobaciones estáticas del proyecto (4 pruebas). |
| `Assets/Tests/EditMode/BarrelAscent.Tests.EditMode.asmdef` | Ensamblado de pruebas de editor. |

## Pruebas nuevas
- **Sesión completa** (`FullSession_…`): Menú → botón Jugar → GameLevel → zona 2 → zona 3 → meta física
  (trigger real) → Victoria → botón Menú → Menú. Se repite dos veces para comprobar que la segunda partida
  empieza limpia (puntos 0, 3 vidas, GameManager nuevo, `timeScale` 1).
- **Resistencia** (`Soak_…`): 60 s de juego (×3) con los barriles activos y el jugador sin colisión: sin errores
  en el log, los barriles aparecen y se eliminan, y el número de barriles vivos no crece sin límite.
- **Proyecto** (EditMode): escenas del build en orden (MainMenu, GameLevel); ninguna escena usa arte de
  `Sprites/Placeholders`; todas las texturas pixel art con 32 PPU / Point / sin compresión / sin mipmaps;
  todos los estados del Animator del jugador tienen animación.

Total: **41 PlayMode + 4 EditMode = 45 pruebas, todas pasan** (incluye 2 de audio en `ArtAudioTests`).

## Ajustes del reproductor
| Ajuste | Valor |
|---|---|
| Nombre / compañía | Barrel Ascent |
| Identificador | `com.barrelascent.game` (antes `com.DefaultCompany.2D-Project`) |
| Versión | 1.0 |
| Arquitectura | Intel 64 + Apple silicon (universal), Mono |
| Pantalla | Pantalla completa en ventana; en modo ventana 1280×720 (×2 de 640×360), redimensionable |

## Verificación del ejecutable
- `lipo`: `x86_64 arm64`. Firma ad hoc (la que pone Unity), sin identidad de desarrollador.
- Arrancado en ventana durante 15 s: carga la primera escena e inicializa el Input System sin errores en el log.

## Notas para distribuirlo
- La app no está firmada con un certificado de Apple ni notarizada. En otro Mac, Gatekeeper la bloqueará la
  primera vez: clic derecho › Abrir (o *Ajustes del Sistema › Privacidad y seguridad › Abrir igualmente*).
  Para entregarla, comprimir la `.app` en un `.zip` (Finder › Comprimir) para conservar los permisos.
- Controles: A/D o flechas para moverse, W/S o flechas en escaleras, Espacio para saltar, Esc pausa, R reinicia
  al terminar. Salir: botón Salir del menú o Cmd+Q.

## Audio (añadido al final de la fase)
El usuario aportó la música y los efectos; están en `Assets/Audio/` y los asigna `Phase7Builder`
(`GameLevelClips` / `MainMenuClips`), así que sobreviven a cualquier reconstrucción.

| Ranura (`AudioManager`) | Archivo | Cuándo suena |
|---|---|---|
| music (GameLevel) | `Music/GameplayLoop.mp3` (BackgroundMusic, 93 s, en bucle) | Durante la partida; se pausa con la pausa y se detiene al ganar o perder |
| music (MainMenu) | `Music/MainMenuTheme.mp3` (*Forging the Ascent*, 93 s, en bucle) | Menú principal |
| jump | `SFX/jump.mp3` | Al saltar |
| barrelSmash | `SFX/barrel_smash.mp3` | Al pisar un barril |
| collect | `SFX/collect.wav` | Al recoger un objeto (recortado a 0,8 s: el original tenía 3,7 s de silencio final) |
| damage | `SFX/damage.mp3` | Al recibir un golpe |
| zoneComplete | `SFX/zone_complete.mp3` | Al completar una zona |
| victory / gameOver | `SFX/victory.mp3` / `SFX/game_over.mp3` | Al llegar a la meta / al perder la última vida |
| pauseOpen | `SFX/pause_open.mp3` (openoptionmenu) | Al abrir la pausa |
| uiClick | `SFX/ui_click.mp3` (UIClicButton) | Cualquier botón de la interfaz, en ambas escenas |
| land | `SFX/land.mp3` (landing sound effect) | Al aterrizar tras una caída (> 3 u/s) |

- Importación: música en *Streaming* (Vorbis), efectos *Decompress On Load* (ADPCM) para que no haya retardo.
- El clic se reproduce en una fuente persistente (`DontDestroyOnLoad`) para que no se corte al cambiar de escena.
- Volúmenes por defecto: música 0,5, efectos 0,8 (ajustables en el Inspector del `AudioManager`).
- Los MP3 pueden dejar un hueco mínimo al repetir el bucle (relleno del códec); si se nota, usar WAV/OGG.

## Avisos conocidos
- El compilador avisa (CS0618) de que `FindFirstObjectByType` / `FindObjectsByType(SortMode)` están obsoletos en
  Unity 6.4. Son avisos, no errores; no afectan al juego.
