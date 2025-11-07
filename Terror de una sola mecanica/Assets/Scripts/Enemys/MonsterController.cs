using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Nueva versión de MonsterController compatible con BlendTree 1D (float) y con detección de tipos:
/// - monsterType == "Monster" -> reproduce runClip SÓLO cuando corre
/// - monsterType == "Anomaly" -> no se mueve (solo anima)
/// - otros tipos -> si ambientClip está asignado, suenan siempre
///
/// Reemplaza al original o úsalo temporalmente renombrando el componente en el prefab.
/// </summary>
[RequireComponent(typeof(Animator))]
public class MonsterController : MonoBehaviour
{
    [Tooltip("Identificador del monstruo (ej: 'Monster','Anomaly','Persistent')")]
    public string monsterType = "Monster";

    [Tooltip("Nombre de la pastilla correcta para este monstruo (case-insensitive)")]
    public string requiredPillName = "Pill";

    [Tooltip("Referencia al Animator del prefab.")]
    public Animator animator;

    public enum BlendMode { OneD, TwoD }

    [Tooltip("Selecciona si tu Blend Tree es 1D (Speed) o 2D (Simple Directional).")]
    public BlendMode blendMode = BlendMode.OneD;

    [Tooltip("Nombre del parámetro que controla el Blend Tree 1D (float preferido, ej: 'Speed'). Si tu Animator usa int, también es soportado.")]
    public string animatorBlendParam = "Speed";

    [Tooltip("Nombre del parámetro X usado por Blend Tree 2D (ej: 'MoveX') - solo si BlendMode == TwoD.")]
    public string animatorBlendParamX = "MoveX";

    [Tooltip("Nombre del parámetro Y usado por Blend Tree 2D (ej: 'MoveY') - solo si BlendMode == TwoD.")]
    public string animatorBlendParamY = "MoveY";

    [Tooltip("Número de estados o rango esperado del Blend (usado por ActivateRandomBlendState).")]
    public int blendTreeStatesCount = 1;

    [Header("Movimiento")]
    [Tooltip("Transform objetivo hacia el que el monstruo se moverá (por ejemplo el jugador). Si se deja vacío se intentará usar Camera.main o CameraMovement.playerBody.")]
    public Transform target;
    [Tooltip("Velocidad base de movimiento (unidades por segundo). Se multiplica por el valor del blend si procede.")]
    public float moveSpeed = 2f;
    [Tooltip("Valor mínimo del parámetro de blend para considerar que el monstruo está corriendo y debe avanzar.")]
    public float runThreshold = 1.0f;
    [Tooltip("Retardo (segundos) desde que el objeto despierta hasta que puede empezar a moverse. Útil si hay una Timeline al inicio del día.")]
    public float moveDelay = 0f;

    [Tooltip("Si true, el enemigo puede moverse (usar EnableMovement()/DisableMovement() para control manual).")]
    public bool movementEnabled = true;

    [Header("Audio")]
    public AudioClip runClip;
    public AudioClip ambientClip;
    [Header("Timeline (Anomaly)")]
    [Tooltip("PlayableDirector que contiene la Timeline de la anomalía. Se reproducirá cuando se llame a OnTurnStart() para el tipo Anomaly.")]
    public PlayableDirector anomalyDirector;
    [Tooltip("(Opcional) Si quieres que al tocar la anomalía se active un GameObject vacío de la escena (por ejemplo con Timeline vinculado), asigna aquí la referencia.")]
    public GameObject anomalySceneTriggerObject;
    [Tooltip("(Opcional) En lugar de referencia, puedes indicar el nombre del GameObject en escena a activar cuando se toque la anomalía.")]
    public string anomalySceneTriggerName;
    [Tooltip("Si true, permitir que el trigger se ejecute varias veces; si false solo se ejecutará la primera vez.")]
    public bool allowRepeatAnomalyTrigger = false;

    // internals
    private AnimatorControllerParameterType blendParamType = AnimatorControllerParameterType.Float;
    private int blendParamHash = 0;
    private int blendParamHashX = 0;
    private int blendParamHashY = 0;
    private System.Random rng = new System.Random();

    private AudioSource ambientSource;
    private AudioSource runSource;
    private Rigidbody2D rb2d;
    private Vector3 initialLocalScale;
    // internal: marca si ya disparamos el trigger en escena
    private bool anomalyTriggered = false;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();

        blendParamHash = Animator.StringToHash(animatorBlendParam);
        blendParamHashX = Animator.StringToHash(animatorBlendParamX);
        blendParamHashY = Animator.StringToHash(animatorBlendParamY);
        if (animator != null)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == animatorBlendParam)
                {
                    blendParamType = p.type;
                }
                // for 2D we don't need to infer types for X/Y - we assume floats in Animator
            }
        }

        if (target == null)
        {
            var cm = FindObjectOfType<CameraMovement>();
            if (cm != null && cm.playerBody != null) target = cm.playerBody;
            else if (Camera.main != null) target = Camera.main.transform;
        }

        var sources = GetComponents<AudioSource>();
        if (sources.Length >= 1) ambientSource = sources[0];
        if (sources.Length >= 2) runSource = sources[1];

        if (ambientSource == null && ambientClip != null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.clip = ambientClip;
        }

        if (runSource == null && runClip != null)
        {
            runSource = gameObject.AddComponent<AudioSource>();
            runSource.playOnAwake = false;
            runSource.loop = true;
            runSource.clip = runClip;
        }

        rb2d = GetComponent<Rigidbody2D>();
        initialLocalScale = transform.localScale;

        // Intentar auto-asignar PlayableDirector para la anomalía si no se configuró en el Inspector
        if (anomalyDirector == null)
        {
            anomalyDirector = GetComponent<PlayableDirector>();
            if (anomalyDirector == null)
                anomalyDirector = GetComponentInChildren<PlayableDirector>();
            if (anomalyDirector != null)
            {
                Debug.Log($"[MonsterController:{name}] anomalyDirector auto-asignado: {anomalyDirector.name}");
                // Auto-bind tracks to prefab-local components so prefab works when instantiated in a scene
                TryAutoBindAnomalyDirector(anomalyDirector);
            }
        }
    }

    void Start()
    {
        // Inicializar el permiso de movimiento respectando moveDelay
        if (moveDelay > 0f)
        {
            movementEnabled = false;
            // programar habilitación después del delay
            movementEnableTime = Time.time + moveDelay;
        }
        else
        {
            movementEnabled = true;
            movementEnableTime = 0f;
        }
    }

    // internal time at which movement will be enabled (0 if already enabled)
    private float movementEnableTime = 0f;

    public void ActivateRandomBlendState()
    {
        if (animator == null || blendTreeStatesCount <= 0) return;
        if (blendParamType == AnimatorControllerParameterType.Float)
        {
            float v = (float)rng.NextDouble() * Mathf.Max(1, blendTreeStatesCount - 1);
            animator.SetFloat(blendParamHash, v);
        }
        else
        {
            int v = UnityEngine.Random.Range(0, blendTreeStatesCount);
            animator.SetInteger(blendParamHash, v);
        }
    }

    public bool CheckPill(string pillName)
    {
        if (string.IsNullOrWhiteSpace(pillName)) return false;
        return string.Equals(pillName.Trim(), requiredPillName?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    void Update()
    {
        if (animator == null) return;
        // If this monster is an anomaly (Timeline-driven), do not move automatically in Update.
        if (string.Equals(monsterType, "Anomaly", StringComparison.OrdinalIgnoreCase))
        {
            // Timeline activation should be triggered externally via OnTurnStart(). We avoid forcing audio changes here.
            return;
        }

        // If movement is disabled by delay or external control, check timer and return early.
        if (!movementEnabled)
        {
            if (movementEnableTime > 0f && Time.time >= movementEnableTime)
            {
                movementEnabled = true;
                movementEnableTime = 0f;
            }
            else
            {
                return;
            }
        }

        // Determine movement direction toward target (2D: X/Y). If no target, do nothing.
        if (target == null) return;

        Vector3 selfPos = transform.position;
        Vector3 targetPos = target.position;
        // Use X/Y plane for 2D movement
        Vector2 dir2 = new Vector2(targetPos.x - selfPos.x, targetPos.y - selfPos.y);
        float dist = dir2.magnitude;
        bool shouldMove = dist > 0.05f;
        Vector2 moveDir = shouldMove ? dir2.normalized : Vector2.zero;

        // Apply movement using Rigidbody2D if present, otherwise transform
        Vector3 newPos = transform.position;
        if (shouldMove)
        {
            Vector2 moveVec2 = moveDir * moveSpeed;
            if (rb2d != null)
            {
                Vector2 next = rb2d.position + moveVec2 * Time.deltaTime;
                rb2d.MovePosition(next);
            }
            else
            {
                newPos = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
                transform.position = newPos;
            }

            // Flip sprite horizontally based on movement direction (2D)
            if (Mathf.Abs(moveDir.x) > 0.01f)
            {
                Vector3 s = transform.localScale;
                s.x = Mathf.Sign(moveDir.x) * Mathf.Abs(initialLocalScale.x);
                transform.localScale = s;
            }
        }

        // Update Animator parameters from movement so BlendTree receives values and animations play.
        float currentBlendValue = 0f;
        if (blendMode == BlendMode.OneD)
        {
            // Set the 1D float to current speed (units/sec). Designers typically map speed to walk/run thresholds.
            float speedForAnim = shouldMove ? moveSpeed : 0f;
            if (blendParamType == AnimatorControllerParameterType.Float)
                animator.SetFloat(blendParamHash, speedForAnim);
            else
                animator.SetInteger(blendParamHash, Mathf.RoundToInt(speedForAnim));

            currentBlendValue = (blendParamType == AnimatorControllerParameterType.Float) ? animator.GetFloat(animatorBlendParam) : animator.GetInteger(animatorBlendParam);
        }
        else // TwoD
        {
            animator.SetFloat(blendParamHashX, moveDir.x);
            animator.SetFloat(blendParamHashY, moveDir.y);
            float x = animator.GetFloat(animatorBlendParamX);
            float y = animator.GetFloat(animatorBlendParamY);
            currentBlendValue = Mathf.Sqrt(x * x + y * y);
        }

        bool isRunning = currentBlendValue >= runThreshold;

        // Audio handling: Monster plays runClip only when running, other types may have ambient sound.
        if (string.Equals(monsterType, "Monster", StringComparison.OrdinalIgnoreCase))
        {
            if (isRunning) PlayRunAudio(); else StopRunAudio();
            if (ambientSource != null && ambientSource.isPlaying) ambientSource.Stop();
        }
        else
        {
            if (ambientSource != null && ambientClip != null)
            {
                if (!ambientSource.isPlaying) ambientSource.Play();
            }

            if (runSource != null && runClip != null)
            {
                if (isRunning)
                {
                    if (!runSource.isPlaying) runSource.Play();
                }
                else
                {
                    if (runSource.isPlaying) runSource.Stop();
                }
            }
        }
    }

    /// <summary>
    /// Llamar desde el controlador de turno cuando sea el turno de este enemigo.
    /// - Si es Anomaly, reproducirá la Timeline (si se configuró).
    /// - Si no, puede usarse para forzar comportamiento puntual (activar blend aleatorio, etc.).
    /// </summary>
    public void OnTurnStart()
    {
        if (string.Equals(monsterType, "Anomaly", StringComparison.OrdinalIgnoreCase))
        {
            // Cuando este enemigo es la anomalía, activar el empty de la escena que contiene la Timeline
            // (el objeto puede estar desactivado en la escena hasta que sea su turno).
            TriggerSceneAnomaly();
            return;
        }

        // Para otros enemigos podemos forzar un estado del blend (opcional)
        ActivateRandomBlendState();
    }

    /// <summary>
    /// Reproduce la Timeline de la anomalía si existe.
    /// </summary>
    public void PlayAnomalyTimeline()
    {
        // Try to ensure director is present
        if (anomalyDirector == null)
        {
            anomalyDirector = GetComponent<PlayableDirector>() ?? GetComponentInChildren<PlayableDirector>();
            if (anomalyDirector == null)
            {
                Debug.LogWarning($"[MonsterController:{name}] anomalyDirector no asignado para Anomaly. Asigna un PlayableDirector con una Timeline en el Inspector o como hijo.");
                return;
            }
            else
            {
                Debug.Log($"[MonsterController:{name}] anomalyDirector encontrado en PlayAnomalyTimeline: {anomalyDirector.name}");
            }
        }

        if (anomalyDirector.playableAsset == null)
        {
            Debug.LogWarning($"[MonsterController:{name}] anomalyDirector tiene 'playableAsset' nulo. Asigna una Timeline en el PlayableDirector.");
            return;
        }

        try
        {
            // Reiniciar y reproducir desde el inicio para asegurar que la Timeline se vea siempre
            anomalyDirector.time = 0d;
            anomalyDirector.Stop();
            anomalyDirector.Play();
            Debug.Log($"[MonsterController:{name}] Reproduciendo Timeline de anomalía: {anomalyDirector.playableAsset.name}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MonsterController:{name}] Error al reproducir anomalyDirector: {ex.Message}");
        }
    }

    /// <summary>
    /// Asignar el PlayableDirector por código (útil si el Timeline se crea en runtime).
    /// </summary>
    public void AssignAnomalyDirector(PlayableDirector director)
    {
        anomalyDirector = director;
        if (anomalyDirector != null)
        {
            TryAutoBindAnomalyDirector(anomalyDirector);
        }
    }

    /// <summary>
    /// Trigger llamado cuando el jugador entra en contacto con la anomalía.
    /// Soporta 2D y 3D (OnTriggerEnter2D / OnTriggerEnter).
    /// Si se asignó `anomalySceneTriggerObject` o `anomalySceneTriggerName`, activará ese objeto
    /// o reproducirá su PlayableDirector. Si no, reproducirá la Timeline local (PlayAnomalyTimeline).
    /// </summary>
    private void TriggerSceneAnomaly()
    {
        if (anomalyTriggered && !allowRepeatAnomalyTrigger) return;

        // Preferir referencia directa en escena
        if (anomalySceneTriggerObject != null)
        {
            anomalySceneTriggerObject.SetActive(true);
            var pd = anomalySceneTriggerObject.GetComponent<PlayableDirector>() ?? anomalySceneTriggerObject.GetComponentInChildren<PlayableDirector>();
            if (pd != null)
            {
                pd.time = 0d;
                pd.Stop();
                pd.Play();
                Debug.Log($"[MonsterController:{name}] Reproduciendo PlayableDirector de escena: {pd.playableAsset?.name}");
            }
            anomalyTriggered = true;
            return;
        }

        // Buscar por nombre en la escena
        if (!string.IsNullOrEmpty(anomalySceneTriggerName))
        {
            var go = GameObject.Find(anomalySceneTriggerName);
            if (go != null)
            {
                go.SetActive(true);
                var pd = go.GetComponent<PlayableDirector>() ?? go.GetComponentInChildren<PlayableDirector>();
                if (pd != null)
                {
                    pd.time = 0d;
                    pd.Stop();
                    pd.Play();
                    Debug.Log($"[MonsterController:{name}] Reproduciendo PlayableDirector encontrado por nombre: {pd.playableAsset?.name}");
                }
                anomalyTriggered = true;
                return;
            }
            else
            {
                Debug.LogWarning($"[MonsterController:{name}] No se encontró GameObject en escena con nombre '{anomalySceneTriggerName}'.");
            }
        }

        // Fallback: reproducir el director local (prefab) si existe
        PlayAnomalyTimeline();
        anomalyTriggered = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!gameObject.activeInHierarchy) return;
        if (other == null) return;
        if (string.Equals(monsterType, "Anomaly", StringComparison.OrdinalIgnoreCase) && other.CompareTag("Player"))
        {
            TriggerSceneAnomaly();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!gameObject.activeInHierarchy) return;
        if (other == null) return;
        if (string.Equals(monsterType, "Anomaly", StringComparison.OrdinalIgnoreCase) && other.CompareTag("Player"))
        {
            TriggerSceneAnomaly();
        }
    }

    /// <summary>
    /// Intenta enlazar automáticamente las pistas del Timeline a componentes del prefab.
    /// Esto soluciona el problema de que los bindings de Timeline no se guardan con prefabs de escena.
    /// </summary>
    private void TryAutoBindAnomalyDirector(PlayableDirector director)
    {
        if (director == null || director.playableAsset == null) return;

        var timeline = director.playableAsset as TimelineAsset;
        if (timeline == null) return;

        foreach (var track in timeline.GetOutputTracks())
        {
            // Animation tracks -> bind to local Animator
            if (track is AnimationTrack)
            {
                if (animator != null)
                    director.SetGenericBinding(track, animator.gameObject);
                continue;
            }

            // Activation tracks -> bind to this gameObject
            if (track is ActivationTrack)
            {
                director.SetGenericBinding(track, gameObject);
                continue;
            }

            // Audio tracks -> try to bind to an existing AudioSource (ambientSource or runSource) or add one
            if (track is AudioTrack)
            {
                AudioSource audioTarget = ambientSource ?? runSource ?? GetComponent<AudioSource>();
                if (audioTarget == null)
                {
                    audioTarget = gameObject.AddComponent<AudioSource>();
                }
                director.SetGenericBinding(track, audioTarget);
                continue;
            }

            // Control tracks or others: try to bind to the GameObject itself
            director.SetGenericBinding(track, gameObject);
        }
    }

    private void PlayRunAudio()
    {
        if (runClip == null) return;
        if (runSource == null)
        {
            runSource = gameObject.AddComponent<AudioSource>();
            runSource.playOnAwake = false;
            runSource.loop = true;
            runSource.clip = runClip;
        }
        if (!runSource.isPlaying) runSource.Play();
    }

    private void StopRunAudio()
    {
        if (runSource != null && runSource.isPlaying) runSource.Stop();
    }
}
