# Fase 2 — Jugador

## Objetivo
Movimiento horizontal, salto (uno por contacto con el suelo), detección de suelo, escaleras y
parámetros de animación, probados sobre una zona de pruebas en `GameLevel`.

## Archivos
| Archivo | Qué hace |
|---|---|
| `Assets/Scripts/BarrelAscent.asmdef` | Ensamblado del juego (permite pruebas automáticas). |
| `Assets/Scripts/Player/PlayerController.cs` | Movimiento, salto, suelo, escaleras, Animator. |
| `Assets/Scripts/Environment/Ladder.cs` | Zona de escalera (BoxCollider2D trigger). |
| `Assets/Editor/Phase2Builder.cs` | Crea capas, placeholders, Animator, prefab y zona de pruebas. |
| `Assets/Editor/ScenePreview.cs` | Captura `Docs/Screenshots/GameLevel.png`. |
| `Assets/Prefabs/Player/Player.prefab` | Jugador listo para usar. |
| `Assets/Animations/Player/Player.controller` | Estados Idle / Run / Jump / Climb (sin clips todavía). |
| `Assets/Sprites/Placeholders/PH_*.png` | Arte temporal **PH_** (se sustituye en la Fase 7). |
| `Assets/Settings/Physics/NoFriction.physicsMaterial2D` | Evita que el jugador se quede pegado a las paredes. |
| `Assets/Tests/PlayMode/PlayerControllerTests.cs` | 5 pruebas automáticas con teclado simulado. |

## Capas de física
`6 Ground` (plataformas, paredes) · `7 Ladder` · `8 Player` · `9 Barrel` (Fase 4).

## Configuración del prefab Player (Inspector)
- **Rigidbody2D**: Dynamic, Mass 1, Gravity Scale 3, Freeze Rotation Z, Interpolate, Continuous.
- **CapsuleCollider2D**: Vertical, Size (0.6, 0.94), Offset (0, 0.47) — el pivote está en los pies.
- **PlayerController**: Move Speed 5, Jump Force 10, Climb Speed 3, Gravity Scale 3,
  Coyote Time 0.08, Jump Buffer 0.1, Ground Mask = Ground, Ladder Mask = Ladder,
  Animator y Sprite Renderer → hijo `Visual`. Input Actions vacío ⇒ usa las Project-wide Actions.
- **Visual** (hijo): SpriteRenderer (Order 10) + Animator con `Player.controller`.

Parámetros del Animator: `Speed`, `Grounded`, `Climbing`, `ClimbSpeed`, `VelocityY`.

## Cómo funciona la escalera
- El trigger va desde la superficie del suelo inferior hasta 0.5 u por encima de la plataforma superior.
- **W/↑** dentro de la escalera → escalar (gravedad 0, se alinea al centro, atraviesa la plataforma superior
  mediante `Rigidbody2D.excludeLayers`). Al llegar arriba se coloca de pie sobre la plataforma.
- **S/↓** de pie en la parte superior → bajar. Al llegar abajo se coloca de pie en el suelo.
- No se puede saltar ni moverse lateralmente mientras se escala.

## Escena de pruebas (`Phase2_TestArea`)
Suelo, bloque para saltar, plataforma A (escalera desde el suelo) y plataforma B (escalera desde A).
Se eliminará cuando se construyan las zonas reales con Tilemap en la Fase 3.

## Pruebas
**Automáticas** (Window › General › Test Runner › PlayMode › Run All) — todas pasan:
1. Aparece apoyado en el suelo.
2. Se mueve a ambos lados y gira el sprite.
3. Salta ~1.7 u, un segundo salto en el aire no hace nada, y tras aterrizar vuelve a saltar.
4. Sube y baja la escalera, sin gravedad, sin poder saltar, y queda de pie arriba/abajo.
5. Sin entrada no hay deriva.

**Manuales** (abrir `GameLevel` y pulsar Play):
- A/D y flechas, Space sobre el bloque, mantener Space en el aire (no hay doble salto).
- Subir ambas escaleras con W y bajar con S. Empujar contra la pared en el aire (no se queda pegado).

## Errores frecuentes
| Síntoma | Causa / solución |
|---|---|
| No responde al teclado | Project Settings › Input System Package: Project-wide Actions = `BarrelAscentControls`. Active Input Handling = Input System Package. |
| No salta nunca | Ground Mask vacío o plataforma sin la capa `Ground`. |
| No sube la escalera | Escalera sin capa `Ladder`, collider sin *Is Trigger* o sin componente `Ladder`. |
| Al salir arriba cae a través | La parte superior del trigger debe ser exactamente superficie + 0.5. |
| Sprite borroso | Textura con Filter Mode distinto de Point (el importador lo pone solo en `Sprites/`). |

## Criterios de finalización
Criterios 1–4 del documento maestro cumplidos: movimiento, salto, sin doble salto, escaleras —
comprobados con las pruebas automáticas. Animaciones: el Animator y sus parámetros están conectados;
los clips se añadirán cuando llegue el arte (Fase 7).
