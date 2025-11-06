# Instrucciones rápidas para integrar los scripts añadidos

Estos scripts implementan la lógica básica solicitada: control de cámara con mouse, resaltado de pastillas, consumo por click, triggers de Timeline/cinemática y utilidades para cámaras. Algunas tareas requieren trabajo manual en el Editor de Unity (por ejemplo asignar AnimationClips, Virtual Cameras y referencias de Timeline). Abajo está la guía paso a paso.

## Archivos añadidos

- `Assets/Scripts/Player/CameraMovement.cs`
  - Ahora expone la propiedad `CurrentHighlighted` para obtener el objeto resaltado.
- `Assets/Scripts/Objetcts/Highlightable.cs`
- `Assets/Scripts/Objetcts/ConsumablePill.cs`
  - Componente para las pastillas. Llamar `Consume()` las consume (play particle, desactiva render/collider y dispara `onConsume`).
- `Assets/Scripts/Player/InteractionHandler.cs`
  - Escucha click izquierdo y consume la pastilla resaltada si existe.
- `Assets/Scripts/Scene/CinematicTrigger.cs`
  - Reproduce un `PlayableDirector` al entrar en el trigger y desactiva el control del jugador mientras la Timeline esté en reproducción.
- `Assets/Scripts/Camera/CinemachineSwitcher.cs`
  - Switcher simple para activar/desactivar cámaras virtuales por índice.
- `Assets/Editor/AnimatorControllerBuilder.cs`
  - Utility para generar un `AnimatorController` con un BlendTree 1D sobre `Speed`. Requiere Unity Editor (menú Tools -> Build Simple Animator Controller).

## Pasos recomendados en Unity Editor

1. Asignar scripts en escena
   - Añade `InteractionHandler` a tu Player (o a un GameObject central). Asigna `CameraMovement` si no lo encuentra automáticamente.
   - Asegúrate de que `CameraMovement` esté en la cámara o en un objeto relacionado.

2. Preparar pastillas (botes)
   - Añade el componente `Highlightable` al prefab/objeto de la pastilla.
   - Añade `ConsumablePill` y configura `pillName` y `consumeEffect` (opcional).
   - Asegúrate de que el collider esté marcado y que esté en la layer incluida por `CameraMovement.highlightLayerMask`.

3. Cinemáticas (Timeline)
   - Crea una `PlayableDirector` y una `Timeline` asset para la cinemática de inicio y otra para la durante-juego.
   - Asigna la `PlayableDirector` al campo `director` de un `CinematicTrigger` (añade un collider con IsTrigger al GameObject y añade el script).
   - El `CinematicTrigger` desactivará automáticamente `CameraMovement` y `InteractionHandler` y los reactivará cuando la Timeline termine.

4. Cinemachine
   - Crea Virtual Cameras y agrégalas a un GameObject con `CinemachineSwitcher` (arrastra los GameObjects de las VCams al arreglo `virtualCameras`).
   - El switcher activa/desactiva GameObjects; alternativamente usa las prioridades de Cinemachine si prefieres blends automáticos.

5. Mecanim y Blend Trees
   - En el menú `Tools -> Build Simple Animator Controller` puedes crear un controlador base.
   - Abre el `AnimatorController` generado y verifica que los clips `Idle/Walk/Run` estén asignados.
   - Crea parámetros: `Speed` (float) y configúralos desde tu controlador de movimiento (por ejemplo desde un script que actualice `animator.SetFloat("Speed", speed)`).

6. Integración final / UI
   - Ajusta la `highlightDistance` en `CameraMovement` para que las pastillas se resalten a la distancia correcta.
   - Asigna partículas y eventos `onConsume` en `ConsumablePill` para efectos o lógica de juego (ej: si consumes la pastilla correcta, llamar a tu lógica para "pasar el día").

## Notas y limitaciones

- El `AnimatorControllerBuilder` intenta buscar clips llamados "idle/walk/run" y usar los que encuentre. Si tus nombres difieren, asigna los clips manualmente.

## Próximos pasos sugeridos

- Añadir efectos sonoros y feedback visual al consumir la pastilla correcta/incorrecta.
- Implementar la lógica de victoria/derrota según la pastilla consumida (ej: pasar el día, avances de historia, aparición de enemigos).
- Añadir tests sencillos en PlayMode para validar que la interacción y las cinemáticas deshabilitan/reactivan control.
