# Diario de Desarrollo (DEVLOG) — Concert Defense AR

**Proyecto:** Trabajo final de Realidad Aumentada — caso práctico "AR Adventures Studios"  
**Motor:** Unity 6 (URP)  
**Paquetes Clave:** AR Foundation 6.6.2, ARCore XR Plug-in 6.6.2, XR Interaction Toolkit 3.6.1, TextMesh Pro, Input System 1.20.0  
**Fecha de inicio:** Octubre 2026  

---

## Registro de Cambios y Decisiones Técnicas

### [2026-10-02] — Inicialización del Proyecto y Definición de Arquitectura

#### 1. Verificación del Entorno
- Se validó la presencia de los paquetes requeridos en `Packages/manifest.json`:
  - `com.unity.xr.arfoundation` (6.6.2)
  - `com.unity.xr.arcore` (6.6.2)
  - `com.unity.render-pipelines.universal` (17.6.0)
  - `com.unity.inputsystem` (1.20.0)
- Configuración de entrada requerida en el editor: **Active Input Handling** debe estar en **Both** (o gestionarse con toques táctiles/raycasts agnósticos) para permitir eventos de puntero y soporte táctil móvil nativo simultáneamente.

#### 2. Estructura de Directorios
Se define la organización modular de scripts en `Assets/Scripts/`:
- `Core/`: Gestión de estado de juego, bucle principal y eventos globales.
- `AR/`: Detección de planos, colocación con retícula, manipulación del campo (gestos) y estimación de iluminación.
- `Player/`: Controlador del avatar (movimiento `MoveTowards`) y pads de teletransporte.
- `Towers/`: Lógica base de torres, especializaciones (Bass, Treble, Echo, Drop), plataformas `BuildSpot` y proyectiles.
- `Enemies/`: Lógica de enemigos comunes (Pixel, Static, Amp), rutas por waypoints y jefes (Distorsión, Feedback, Reina Glitch, Mezcla Final).
- `Rhythm/`: Reloj rítmico (`BeatClock`), registro de aciertos (`RhythmInput`), multiplicadores y controlador de Ultimate (Roller Coaster).
- `UI/`: HUD en Screen Space (monedas, ánimo, combo, botones) y elementos World Space con Billboard (`UIFollow`).

#### 3. Decisiones de Arquitectura
- **Patrón Singleton desacoplado por eventos:** `GameManager` servirá como punto de coordinación para estados de juego (`Scanning`, `Placing`, `Playing`, `GameOver`, `Victory`), economía y salud del escenario ("Ánimo"), notificando cambios a través de `System.Action` / UnityEvents.
- **Relatividad al Battlefield:** El escenario completo (`Battlefield`) es un prefab padre con escala y orientación unificadas. Todos los waypoints, torres y plataformas son hijos directos o indirectos para preservar las métricas de combate si el usuario escala o rota el campo en AR.

### [2026-10-02] — Script 1: GameManager (Core)
- **Ruta:** `Assets/Scripts/Core/GameManager.cs`
- **Funcionalidad:**
  - Singleton de gestión global (`GameManager.Instance`).
  - Máquina de estados: `Scanning`, `Placing`, `Playing`, `GameOver`, `Victory`.
  - Sistema de economía de monedas (inicial: 150) con comprobación atómica `TrySpendCoins` y adición `AddCoins`.
  - Control de salud del escenario ("Ánimo", inicial: 20) con `TakeStageDamage` y transición automática a `GameOver`.
  - Control de oleadas (1 a 4) con transición a `Victory`.
  - Eventos C# desacoplados (`OnGameStateChanged`, `OnHealthChanged`, `OnCoinsChanged`, `OnWaveChanged`) para actualizar la UI y controladores sin dependencias directas.

### [2026-10-02] — Script 2: ARPlacementController (AR)
- **Ruta:** `Assets/Scripts/AR/ARPlacementController.cs`
- **Funcionalidad:**
  - Integración directa con `ARRaycastManager` y `ARPlaneManager` de AR Foundation.
  - Proyección de raycast continuo desde el centro de la pantalla (`Screen.width * 0.5f, Screen.height * 0.5f`) filtrado por `TrackableType.PlaneWithinPolygon`.
  - Retícula visual que sigue la orientación horizontal de la cámara proyectada sobre el plano.
  - Filtro contra toques accidentales sobre la interfaz gráfica (`IsPointerOverUI` con `EventSystem`).
  - Instanciación del prefab padre `Battlefield` en la superficie real y transición automática de `GameState.Placing` a `GameState.Playing`.
  - Ocultación de las mallas de planos AR una vez fijado el escenario para evitar distracciones visuales.

### [2026-10-02] — Script 3: LightEstimationController (AR)
- **Ruta:** `Assets/Scripts/AR/LightEstimationController.cs`
- **Funcionalidad:**
  - Suscripción a eventos `frameReceived` de `ARCameraManager`.
  - Extracción y aplicación de datos de iluminación de ARCore: brillo/intensidad media (`averageBrightness` / `mainLightIntensityLumens`), corrección cromática (`colorCorrection` / `mainLightColor`) y rotación solar/luz principal (`mainLightDirection`).
  - Interpolación suavizada por `Mathf.Lerp` y `Quaternion.Slerp` para eliminar parpadeos de lectura del sensor móvil.
  - Detección de umbral de baja luz con evento público `OnLowLightStateChanged` para activar efectos lumínicos/neón opcionales en el escenario.

### [2026-10-02] — Script 4: FieldManipulator (AR)
- **Ruta:** `Assets/Scripts/AR/FieldManipulator.cs`
- **Funcionalidad:**
  - Manipulación táctil del escenario holográfico (`Battlefield`):
    - Pellizco con dos dedos para escalar entre 0.5× y 2.0×.
    - Giro con dos dedos para rotar suavemente en el eje Y.
    - Arrastre con un solo dedo sobre plano libre para reposicionar en la superficie real.
  - Bloqueo de manipulación durante oleadas activas (`ManipulationAllowed`) para evitar conflictos con toques de combate o avatar.
  - Discriminación contra elementos de UI y objetos interactuables mediante `EventSystem` y tags (`Tower`, `BuildSpot`, `Avatar`).
  - Soporte de simulación en el editor (rueda del ratón para escalar y clic derecho para rotar).

### [2026-10-02] — Script 5: Enemy (Enemies)
- **Ruta:** `Assets/Scripts/Enemies/Enemy.cs`
- **Funcionalidad:**
  - Movimiento guiado por ruta discreta de waypoints (`InitializePath`).
  - Sistema de salud, mitigación de daño y destrucción controlada.
  - Efectos de estado aplicables: ralentización por corrutina (`ApplySlow`, para torre Echo) y empuje físico (`ApplyKnockback`, para torre Drop).
  - Economía: recompensa al jugador con monedas (`GameManager.Instance.AddCoins`) al morir.
  - Penalización: resta de Ánimo al escenario (`GameManager.Instance.TakeStageDamage`) al alcanzar el final del camino.
  - Eventos C# (`OnHealthChanged`, `OnEnemyDeath`, `OnEnemyReachedEnd`) para sincronizar barras de vida flotantes y contadores de oleada.

### [2026-10-02] — Script 6: WaveSpawner (Core)
- **Ruta:** `Assets/Scripts/Core/WaveSpawner.cs`
- **Funcionalidad:**
  - Control de oleadas configurables con subgrupos de enemigos (`EnemyGroup`) e intervalos de aparición (`spawnInterval`).
  - Soporte para instanciación de Jefe con retraso configurable (`delayBeforeBoss`) al final del grupo de esbirros.
  - Extracción automática de waypoints desde el GameObject padre `Path` del prefab `Battlefield`.
  - Contenedor organizado de jerarquía (`Enemies`) para evitar desorden en la escena.
  - Bloqueo y desbloqueo sincronizado de la manipulación del campo AR (`FieldManipulator.ManipulationAllowed`) para que no se mueva el escenario durante el combate.
  - Notificación de progreso (`OnWaveStarted`, `OnWaveCompleted`, `OnRemainingEnemiesChanged`).

### [2026-10-02] — Script 7: Projectile (Towers)
- **Ruta:** `Assets/Scripts/Towers/Projectile.cs`
- **Funcionalidad:**
  - Sistema de proyectiles guiados (Homing) o directos con física en Unity 6 (`linearVelocity`).
  - Daño único y daño en área configurable (`splashRadius` con `Physics.OverlapSphere`).
  - Transmisión de efectos de estado al impactar:
    - Ralentización (`slowFactor`, `slowDuration`) para torre Echo.
    - Empuje físico direccional (`knockbackForce`, `rb.AddForce`) para torre Drop.
  - Autodestrucción por tiempo de vida y generación de partículas de impacto (`impactVfxPrefab`).

### [2026-10-02] — Script 8: Tower (Towers)
- **Ruta:** `Assets/Scripts/Towers/Tower.cs`
- **Funcionalidad:**
  - Componente base para las 4 clases de torres (Bass, Treble, Echo, Drop).
  - Detección radial periódica de enemigos (`Physics.OverlapSphere`) y apuntado horizontal suave hacia el objetivo (`rotatorPart`).
  - Progresión de 3 niveles con escala de daño, alcance y cadencia.
  - Venta de torre con reembolso exacto del 60% de la inversión total (costo base + mejoras).
  - Integración con el compás musical (`SetRhythmMultiplier`) para aplicar el 1.5× de daño en pulsaciones Perfect.
  - Mecánica de silenciado y afinación (`Silence` y `Tune`) requerida para el combate contra el Jefe Feedback.

### [2026-10-02] — Script 9: BuildSpot (Towers)
- **Ruta:** `Assets/Scripts/Towers/BuildSpot.cs`
- **Funcionalidad:**
  - Plataforma de anclaje para torres con control de estado (libre / ocupada).
  - Implementación de la regla de proximidad del avatar (`requireAvatarNearby`, configurable) que exige tener a la heroína cerca para poder construir o mejorar.
  - Indicador visual interactivo que cambia dinámicamente de color (verde turquesa si el avatar está en rango / rojo si está lejos).
  - Instanciación de la torre vinculada, descuento atómico de monedas y método de liberación de plataforma (`ClearSpot`).
  - Emisión del evento estático `OnBuildSpotClicked` para desplegar el selector de torres al tocar la plataforma.

### [2026-10-02] — Script 10: AvatarController (Player)
- **Ruta:** `Assets/Scripts/Player/AvatarController.cs`
- **Funcionalidad:**
  - Desplazamiento lineal y rotación continua de la heroína hacia puntos tocados en el campo (`MoveTowards`) sin dependencia de mallas NavMesh.
  - Teletransporte instantáneo (`Teleport`) entre puntos con soporte para partículas de destello cian.
  - Sistema de afinación automática de torres silenciadas por el Jefe Feedback mediante barrido de proximidad (`tuneProximity`).
  - Integración opcional con máquina de animación Mecanim (`Animator`) con parámetro booleano `IsMoving`.
  - Filtro contra toques accidentales sobre la interfaz de usuario (`EventSystem`).

### [2026-10-02] — Script 11: TeleportPad (Player)
- **Ruta:** `Assets/Scripts/Player/TeleportPad.cs`
- **Funcionalidad:**
  - Red de teletransporte táctil entre plataformas del campo de batalla.
  - Comportamiento bidireccional inteligente: tocar un pad distante atrae al avatar; tocarlo mientras el avatar está encima lo envía al `pairedPad`.
  - Emisión de efectos visuales de destello cian (`cyanFlashVfxPrefab`) tanto en el punto de salida como en el de llegada.
  - Prevención de rebotes infinitos mediante temporizador de enfriamiento (`cooldown`).

### [2026-10-02] — Script 12: UIFollow (UI)
- **Ruta:** `Assets/Scripts/UI/UIFollow.cs`
- **Funcionalidad:**
  - Cumplimiento del requisito de caso práctico "UI que sigue al objeto en el espacio real".
  - Soporte de orientación Billboard (3D completo o eje Y) para que barras de vida y menús siempre miren a la cámara AR.
  - Desplazamiento configurable (`worldOffset`) sobre enemigos o torres.
  - Sistema opcional de escalado compensado por distancia para garantizar legibilidad al caminar alrededor de la mesa.
  - Autodestrucción sincronizada (`destroyWithTarget`) al morir el enemigo seguido.

### [2026-10-02] — Script 13: TowerMenu (UI)
- **Ruta:** `Assets/Scripts/UI/TowerMenu.cs`
- **Funcionalidad:**
  - Menú contextual flotante en World Space activado al tocar cualquier torre (`Tower.OnTowerClicked`).
  - Muestra información en tiempo real con TextMeshPro: nombre, nivel actual (1 a 3 o MAX), costo de mejora y valor de reembolso.
  - Acciones interactivas:
    - **Mejorar:** Invoca `TryUpgrade()` y actualiza el saldo de monedas en `GameManager`.
    - **Vender:** Reembolsa el 60% acumulado, libera la plataforma `BuildSpot` y destruye la torre.
    - **Cerrar:** Oculta el menú y apaga el visualizador de rango de la torre.

### [2026-10-02] — Script 14: BeatClock (Rhythm)
- **Ruta:** `Assets/Scripts/Rhythm/BeatClock.cs`
- **Funcionalidad:**
  - Reloj musical maestro con sincronización de audio de hardware mediante `AudioSettings.dspTime` para prevenir desincronizaciones por tasa de fotogramas.
  - BPM configurable (120 BPM base) con cálculo de compás y emisión de evento periódico `OnBeat`.
  - Emisión de pulso continuo normalizado de 0 a 1 (`OnBeatPulse`) para animar el metrónomo visual de la UI.
  - Evaluación matemática precisa de pulsaciones del jugador (`EvaluateTap`) clasificándolas en:
    - **Perfect:** ±0.08 segundos.
    - **Good:** ±0.15 segundos.
    - **Miss:** fuera de ventana.

### [2026-10-02] — Script 15: RhythmInput (Rhythm)
- **Ruta:** `Assets/Scripts/Rhythm/RhythmInput.cs`
- **Funcionalidad:**
  - Botón táctil Screen Space independiente de los toques sobre el mundo 3D del campo.
  - Gestión de combo rítmico con animaciones visuales (colores anime: turquesa para Perfect, magenta para Good, gris para Miss).
  - Bonificación de daño global: aplica 1.5× a todas las torres de defensa en la escena durante el compás al obtener Perfect.
  - Generación de carga para el ataque especial Roller Coaster (`OnUltimateChargeGenerated`) proporcional a la precisión y el multiplicador de racha.
  - Animación del anillo de pulso (`pulseRing`) sincronizado con el compás para anticipar visualmente el ritmo.

### [2026-10-02] — Script 16: UltimateController (Rhythm / Core)
- **Ruta:** `Assets/Scripts/Rhythm/UltimateController.cs`
- **Funcionalidad:**
  - Control del ataque especial definitivo "Roller Coaster" (requisito de modelo 3D del caso práctico).
  - Acumulación de energía por aciertos de ritmo de `RhythmInput` y recarga pasiva gradual en estado de combate.
  - Indicador de carga radial/lineal (`chargeFillImage`) y botón habilitado con feedback visual al 100%.
  - Recorrido físico acelerado del carrito por la vía de la montaña rusa con trigger (`RollerCoasterCart`), infligiendo daño masivo (300 pts) y empuje físico a todos los glitches en su trayecto.

### [2026-10-02] — Script 17: BossBase (Enemies)
- **Ruta:** `Assets/Scripts/Enemies/BossBase.cs`
- **Funcionalidad:**
  - Clase base para los 4 Jefes de final de oleada (Distorsión, Feedback, Reina Glitch, Mezcla Final).
  - Penalización aumentada: resta 5 puntos de Ánimo si alcanza la meta del escenario (`stageDamage = 5`).
  - Eventos estáticos globales (`OnBossSpawned`, `OnBossDefeated`) para vincular la barra de vida superior de la pantalla.
  - Integración opcional de barra de vida flotante propia mediante `UIFollow`.
  - Estructura base de temporizador para habilidades activas del jefe (`ExecuteBossAbility`).

### [2026-10-02] — Script 18: HUDController (UI)
- **Ruta:** `Assets/Scripts/UI/HUDController.cs`
- **Funcionalidad:**
  - Controlador integral del HUD en Screen Space Overlay.
  - Guía visual interactiva de escaneo y colocación AR (`arGuidancePanel`).
  - Visualización en tiempo real de recursos: monedas, Ánimo del escenario (con slider) y oleada actual.
  - Botón de control de oleada: visible en descansos y oculto durante el combate.
  - Barra de vida de Jefe en la parte superior de la pantalla: se activa con `BossBase.OnBossSpawned` y se desactiva con `BossBase.OnBossDefeated`.
  - Pantallas de Game Over y Victoria con botones de reinicio inmediato (`GameManager.Instance.RestartGame`).

### [2026-10-02] — Script 19: TowerSelectorUI (UI)
- **Ruta:** `Assets/Scripts/UI/TowerSelectorUI.cs`
- **Funcionalidad:**
  - Menú de compra y selección de torres activado al tocar un `BuildSpot` disponible (`BuildSpot.OnBuildSpotClicked`).
  - Tarjetas de personajes anime configurables con costos oficiales:
    - **Bass:** 50 monedas.
    - **Treble:** 40 monedas.
    - **Echo:** 60 monedas.
    - **Drop:** 80 monedas.
  - Validación dinámica de saldo (`RefreshAffordability`) para bloquear botones si el jugador no dispone de fondos suficientes.
  - Instanciación directa en la plataforma seleccionada mediante `targetBuildSpot.BuildTower()`.

### [2026-10-02] — Script 20: BossDistortion (Enemies)
- **Ruta:** `Assets/Scripts/Enemies/BossDistortion.cs`
- **Funcionalidad:**
  - Jefe de la Oleada 1 (Distorsión): diseñado con tanque de vida (600 HP base) y movimiento lento (0.65 m/s).
  - Habilidad periódica de despliegue de Zonas de Ruido (`NoiseZone`) sobre el trazado del camino.
  - Las zonas de ruido detectan proyectiles mediante trigger y reducen su velocidad (`linearVelocity *= 0.4f`) entorpeciendo la cadencia efectiva de las torres.

### [2026-10-02] — Script 21: BossFeedback (Enemies)
- **Ruta:** `Assets/Scripts/Enemies/BossFeedback.cs`
- **Funcionalidad:**
  - Jefe de la Oleada 2 (Feedback): tanque medio de vida (800 HP) y velocidad 0.75 m/s.
  - Habilidad periódica de pulso de interferencia electromagnética / acople acústico en un radio de 2.0 metros.
  - Silencia las torres alcanzadas invocando `tower.Silence(silenceDuration = 6s)`.
  - Integración sinérgica con `AvatarController.CheckForSilencedTowersNearby` y `TeleportPad`, obligando al jugador a trasladar a la heroína al lado de las torres silenciadas para afinarlas y reactivar su disparo.

### [2026-10-02] — Script 22: BossGlitchQueen (Enemies)
- **Ruta:** `Assets/Scripts/Enemies/BossGlitchQueen.cs`
- **Funcionalidad:**
  - Jefe de la Oleada 3 (Reina Glitch): líder de enjambre con 1000 HP y velocidad 0.70 m/s.
  - Mecánica de división por umbrales de daño: al alcanzar el 75%, 50% y 25% de vida, se fragmenta instanciando 2 esbirros (`Enemy_Pixel`) directamente en su posición de combate.
  - Explosión final de división al ser destruida: libera 4 esbirros adicionales que heredan su waypoint actual y continúan avanzando hacia el escenario.
  - Actualización de `Enemy.InitializePath` para soportar índices y posiciones intermedias de instanciación.

### [2026-10-02] — Script 23: BossFinalMix (Enemies)
- **Ruta:** `Assets/Scripts/Enemies/BossFinalMix.cs`
- **Funcionalidad:**
  - Jefe final de la Oleada 4 (Mezcla Final): máximo desafío con 1500 HP base y 250 monedas de recompensa.
  - Combate por 3 fases dinámicas según el porcentaje de vida:
    - **Fase 1 (100% - 66% HP):** Despliega zonas de ruido de Distorsión.
    - **Fase 2 (66% - 33% HP):** Emite pulsos de interferencia electromagnética de Feedback silenciando torres en 2.2 metros.
    - **Fase 3 (33% - 0% HP):** Sobrecarga de furia (aumento de velocidad ×1.35 e intervalo acelerado) combinando zonas de ruido, silenciado global y generación continua de esbirros de enjambre.

### [2026-10-02] — Ensamblaje Completo de Activos, Prefabs, Audio, UI y Escena
- **Tags y Capas:**
  - Registrados en `ProjectSettings/TagManager.asset`: `Enemy`, `Tower`, `BuildSpot`, `Avatar`, `Field`.
- **Configuración de Entrada:**
  - `activeInputHandler` configurado en `2` (Both) en `ProjectSettings/ProjectSettings.asset` para permitir compatibilidad simultánea entre el nuevo Input System y la entrada clásica / toques de mouse en el Editor.
- **Herramienta de Montaje Automatizada:**
  - Implementado `Assets/Scripts/Editor/GameSetupUtility.cs` con acceso desde la barra de menú `ConcertDefense -> Setup Complete Game`.
- **Materiales Neón URP:**
  - 16 materiales creados en `Assets/Materials/` con shaders URP Lit/Unlit y emisión activa (`Mat_NeonCyan`, `Mat_NeonMagenta`, `Mat_NeonYellow`, `Mat_NeonPurple`, `Mat_DarkStage`, `Mat_FieldPath`, `Mat_BuildSpotNormal`, `Mat_BuildSpotInRange`, `Mat_BuildSpotOutOfRange`, `Mat_NoiseZone`, `Mat_Reticle`, `Mat_WhiteMetal`, `Mat_EnemyPixel`, `Mat_EnemyStatic`, `Mat_EnemyAmp`, `Mat_BossDark`).
- **Pista de Audio y SFX Procedurales:**
  - `Assets/Audio/BGM_120BPM_CyberConcert.wav`: Generada pista rítmica continua a 120 BPM con bombo 4/4, hi-hat en octavas y línea de bajo sintetizada de 4 compases.
  - `Assets/Audio/TeleportSFX.wav`: Barrido armónico ascendente para teletransporte.
  - `Assets/Audio/FeedbackSFX.wav`: Tono oscilante de acople electromagnético para la habilidad de Feedback.
- **Prefabs de Proyectiles:**
  - `Projectile_Bass`, `Projectile_Treble`, `Projectile_Echo`, `Projectile_Drop` configurados en `Assets/Prefabs/Projectiles/`.
- **Prefabs de Torres:**
  - `Tower_Bass`, `Tower_Treble`, `Tower_Echo`, `Tower_Drop` configurados en `Assets/Prefabs/Towers/` con base, cabeza giratoria, punto de fuego, visualizador de rango y aviso de silenciado.
- **Prefabs de Enemigos y Jefes:**
  - Esbirros: `Enemy_Pixel`, `Enemy_Static`, `Enemy_Amp` en `Assets/Prefabs/Enemies/`.
  - Jefes: `Boss_Distortion`, `Boss_Feedback`, `Boss_GlitchQueen`, `Boss_FinalMix` en `Assets/Prefabs/Bosses/`.
  - Zona de distorsión: `NoiseZone` en `Assets/Prefabs/Enemies/`.
- **Prefab de Campo de Batalla y Entorno:**
  - `Battlefield` en `Assets/Prefabs/Environment/`: Suelo `Field` (1.6m × 1.6m), ruta `Path` con 8 waypoints y baldosas de luz, 5 plataformas `BuildSpot` interactivas con cambio de color por proximidad del avatar, 2 plataformas `TeleportPad` emparejadas, vía de montaña rusa `RollerCoasterTrack` con 5 waypoints y carrito `RollerCoasterCart`, contenedor de enemigos y heroína `Avatar_Hero` instanciada.
  - `PlacementReticle` en `Assets/Prefabs/Environment/`.
- **Ensamblaje en SampleScene:**
  - Objeto `[GameManagers]` con `GameManager`, `WaveSpawner` (configurado con las 4 oleadas secuenciales y jefes), `BeatClock` (120 BPM) y `UltimateController`.
  - `XR Origin (AR Rig)` configurado con `ARPlacementController`, `FieldManipulator`, `LightEstimationController` y `AudioListener` en la cámara principal.
  - Canvas Screen Space `ConcertDefense_HUD_Canvas` con `HUDController` (monedas, ánimo, oleada, botón de inicio, barra de jefe, game over, victoria, guía AR), `RhythmInput` (botón Beat, anillo pulsante, combos, feedback) y `TowerSelectorUI` (menú de 4 cartas).
  - Canvas World Space `WorldSpace_TowerMenu` con `UIFollow` y `TowerMenu` para mejorar y vender torres en 3D.
- **Atajo de Pruebas en Editor:**
  - Se agregó soporte para pulsar la tecla `Espacio` en el editor de Unity para fijar el escenario inmediatamente a 1.2 m sin requerir escaneo de cámara AR física.

---

### [2026-10-03] — Revisión completa: corrección de errores y cierre del GDD

Se revisó todo el avance contra el GDD. Los scripts se reescribieron conservando nombres y carpetas; escena, prefabs, materiales y audio se regeneran con `ConcertDefense → Setup Complete Game` (`Assets/Scripts/Editor/GameSetupUtility.cs`), que se puede volver a ejecutar sin duplicar nada.

#### Errores corregidos
- **Paquetes:** `Packages/manifest.json` apuntaba a `com.coplaydev.unity-mcp` en `C:/Users/ADMIN/Downloads/...`, una ruta local de otro equipo. En cualquier otra PC fallaba la resolución de paquetes. Se quitó esa dependencia (no la usa el juego).
- **HUD invisible al empezar:** `HUDController` desactivaba `GameplayHUD_Root` en `Awake` y en los estados `Scanning` y `Placing`. Ahora el HUD de juego (monedas, Ánimo, oleada, Iniciar oleada, Beat, Ultimate, Reubicar) está activo desde el primer fotograma; solo aparecen y desaparecen la guía AR, la barra de jefe, los avisos y los paneles de fin de partida.
- **Botón Iniciar oleada:** siempre visible. Si el campo aún no está colocado, lo coloca; durante el combate muestra los glitches restantes.
- **Campo guardado dentro de la escena:** había un `Battlefield(Clone)` desactivado y seis `MenuRoot` duplicados en `WorldSpace_TowerMenu`. El montaje limpia esos restos y el `Object Spawner` de la plantilla.
- **Torres gratis y aplastadas:** `BuildSpot` leía `TotalInvestedCoins` del prefab (0 antes de `Awake`) y colgaba la torre de la plataforma, que tiene escala no uniforme. Ahora cobra `BaseCost` y la torre cuelga del contenedor `Towers`.
- **Roller Coaster sin efecto:** la vía rodeaba el campo a 0.25 m de altura y nunca tocaba enemigos. Ahora baja de una estación elevada y recorre el camino de los glitches.
- **Zonas de ruido sin efecto:** multiplicaban una velocidad que el proyectil reescribía cada fotograma. Ahora usan `Projectile.SpeedMultiplier`.
- **Medidores sin relleno:** `Image.fillAmount` no hace nada sin sprite. Barras de Ánimo, jefe, vida y Ultimate usan relleno por anclas.
- **Jefe que llega a la meta:** la barra superior no se ocultaba. `BossBase.OnBossDefeated` se emite también en ese caso.
- **División de la Reina Glitch en `OnDestroy`:** creaba objetos al descargar la escena. Se movió a `OnRemovedFromField` y los esbirros se registran en `WaveSpawner`.
- **Varios `MonoBehaviour` por archivo:** `NoiseZone` y `RollerCoasterCart` pasan a archivos propios.
- **Caracteres sin glifo** (♪, ★) en botones: se quitaron.
- **Oleadas y enemigos** no seguían el GDD (Static era lento y resistente). Se ajustaron a las tablas de las secciones 5 y 6.

#### Decisiones técnicas
- **Entrada:** `PointerInput` (Input System, `EnhancedTouch` + ratón) sustituye a `Input.*` y a `OnMouseDown`. `TouchInputRouter` hace el raycast propio que pide la sección 0 del GDD: torre → menú, plataforma → selector, pad → teletransporte, suelo → mover a la heroína. Funciona con *Active Input Handling* en *Both* o en *Input System*.
- **Unidades de campo:** `Battlefield.Scale` convierte a metros. Velocidades, alcances y radios se expresan en unidades de campo, así que escalar o rotar el campo no descuadra el juego (GDD 4.1). El prefab mide 1.7 unidades y nace a escala 0.6 (≈ 1 m).
- **Ritmo:** `BeatClock` emite `OnBeat` y `OnHalfBeat`. Cada torre dispara cada N medios tiempos (Treble 1, Bass y Echo 4, Drop 6). El botón Beat se evalúa al apoyar el dedo (`PointerDownRelay`), no al soltar. Perfect da ×1.5 de daño durante un tiempo.
- **Física:** proyectiles con `Rigidbody` y trigger; el empuje de Drop y del carrito vuelve dinámico el `Rigidbody` del enemigo durante 0.35 s.
- **Sin AR:** en el editor (o en un dispositivo sin ARCore) `ARPlacementController` entra en modo de prueba, con cámara fija y el campo en el origen. Atajos: clic o Espacio colocan el campo, Enter inicia oleada, B es el botón Beat, rueda escala y clic derecho rota.
- **Arte:** personajes originales hechos con primitivas (cantantes chibi de coletas largas). Shader propio `ConcertDefense/Toon` (dos tonos + contorno) y `ConcertDefense/UnlitColor` para retícula, alcance y efectos. Los glitches usan rojo y negro.
- **Audio:** pista de 120 BPM y diez efectos generados por código en `Assets/Audio` (autoría propia).
- **Ajustes del proyecto:** tag `TeleportPad` añadido; orientación por defecto *Landscape Left*; `minSdkVersion` de Android bajado de 34 a 29.

#### Scripts nuevos
`Core/PointerInput`, `Core/TouchInputRouter`, `Core/Battlefield`, `Core/GameMessages`, `Core/Sfx`, `Core/PulseEffect`, `Enemies/NoiseZone`, `Rhythm/RollerCoasterCart`, `Rhythm/PointerDownRelay`, `AR/PlacementReticle`, `UI/WorldHealthBar`, `Editor/ConcertDefenseSmokeTest`.

#### Requisitos del caso → dónde verlos
| Requisito | Implementación |
|---|---|
| UI que sigue al objeto | `UIFollow` + `WorldHealthBar` (barras de vida) y `TowerMenu` (menú flotante) |
| Eventos de toque | `TouchInputRouter` + `PointerInput` |
| Movimiento del avatar | `AvatarController.SetDestination` |
| Teletransportación | `TeleportPad` + destello `Vfx_CyanFlash` |
| Roller Coaster | `UltimateController` + `RollerCoasterCart`, vía en `Battlefield/RollerCoaster` |
| Detección de planos | `ARPlacementController` (`ARRaycastManager`, `PlaneWithinPolygon`) |
| Crear / actualizar / eliminar | construir, mejorar y vender torres; enemigos y proyectiles |
| Manipulación | `FieldManipulator` (pellizco, giro, arrastre) |
| Retículas | `PlacementReticle` (verde/rojo) e indicador de `BuildSpot` |
| Iluminación real | `LightEstimationController` en la Directional Light |
| Colisiones y física | `Projectile`, `Enemy.ApplyKnockback`, `NoiseZone` |

#### Verificación (Unity 6000.6.4f1 en modo batch)
- Compilación sin errores ni avisos de C#.
- `ConcertDefenseSmokeTest` entra en Play, comprueba el HUD inicial, coloca el campo, construye torres y juega las cuatro oleadas con un bot: termina en **Victoria con 0 errores en consola**. Resultado y capturas en `Capturas/`.
- Comando: `Unity -batchmode -projectPath . -executeMethod ConcertDefense.EditorTools.ConcertDefenseSmokeTest.Run` con la variable de entorno `CONCERT_SMOKE=1`.
- El bot acierta todos los tiempos, así que la dificultad real para una persona queda por ajustar jugando en el teléfono (valores en los prefabs y en `GameSetupUtility`).
- **No verificado:** ejecución en un teléfono con ARCore (detección de planos reales, gestos con dos dedos, estimación de luz). En esta PC el editor 6000.6.4f1 no tiene instalado *Android Build Support*.

#### Pasos manuales pendientes (en el editor)
0. **Unity Hub → Installs → 6000.6.4f1 → Add modules → Android Build Support** (con OpenJDK y Android SDK & NDK Tools).
1. **File → Build Profiles → Android → Switch Platform.**
2. **Project Settings → XR Plug-in Management → Android:** comprobar que *ARCore* está marcado.
3. Conectar el teléfono con depuración USB y **Build And Run**. Hacer las capturas para el informe.
