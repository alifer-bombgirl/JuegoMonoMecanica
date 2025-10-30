using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Versión revisada: rotaciones suavizadas y desplazamientos mínimos pensados para un pasillo estrecho.
/// - Usa SmoothDampAngle para suavizar yaw/pitch.
/// - Limita yaw relativo para evitar girar atrás.
/// - Desplazamientos laterales/verticales muy pequeños y suavizados.
/// </summary>
public class CameraMovement : MonoBehaviour
{
    [Header("Referencias")]
    public Transform playerBody; // gira en Y
    [Tooltip("Pivot que Cinemachine usa para LookAt (opcional). Si es null, se utiliza esta cámara.")]
    public Transform cinemachineAim;

    [Header("Sensibilidad y suavizado")]
    [Tooltip("Sensibilidad del look. Reducida para movimientos muy finos en pasillos.")]
    public float sensitivity = 5f;
    [Tooltip("Multiplicador adicional para adaptar el valor de Look (no usar Time.deltaTime). Reduce si el movimiento es muy grande).")]
    public float inputMultiplier = 0.02f;
    [Tooltip("Tiempo de suavizado para yaw (segundos)")]
    public float yawSmoothTime = 0.06f;
    [Tooltip("Tiempo de suavizado para pitch (segundos)")]
    public float pitchSmoothTime = 0.06f;

    [Header("Límites (grados)")]
    [Tooltip("Activa para mostrar ángulos/rajes en Play (útil para ajustar límites)")]
    public bool showAngleDebug = true;
    public float maxPitch = 60f;
    public float minPitch = -60f;
    [Tooltip("Máximo giro horizontal desde la dirección inicial (evita girar atrás).")]
    public float maxYawFromStart = 60f;

    [Header("Pequeño movimiento visual")]
    [Tooltip("Desplazamiento lateral máximo (unidades locales). Muy pequeño para pasillos.")]
    public float sideAmount = 0.02f;
    [Tooltip("Desplazamiento vertical máximo (unidades locales).")]
    public float verticalAmount = 0.01f;
    [Tooltip("Suavizado para el desplazamiento local")]
    public float localPosSmoothTime = 0.08f;

    [Header("Resaltado")]
    public float highlightDistance = 4f;
    public LayerMask highlightLayerMask = ~0;

    private Actions playerControls;
    private Vector2 lookInput;

    // Yaw/pitch targets & smooth state
    private float initialYaw = 0f;
    private float yawRelativeTarget = 0f;
    private float yawCurrent = 0f;
    private float yawVel = 0f;

    private float pitchTarget = 0f;
    private float pitchCurrent = 0f;
    private float pitchVel = 0f;

    // local position smoothing
    private Transform positionTarget;
    private Vector3 defaultLocalPos;
    private Vector3 localPosVel;

    private Highlightable lastHighlighted;

    void Awake()
    {
        playerControls = new Actions();
        playerControls.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        playerControls.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        if (playerBody == null)
            Debug.LogWarning("CameraMovement: playerBody no asignado.");

        positionTarget = cinemachineAim != null ? cinemachineAim : transform;
        defaultLocalPos = positionTarget.localPosition;
    }

    void OnEnable()
    {
        playerControls.Player.Enable();
    }

    void OnDisable()
    {
        playerControls.Player.Disable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        initialYaw = playerBody ? playerBody.eulerAngles.y : 0f;

        // Inicializar estados actuales iguales a los objetivos para evitar saltos
        yawCurrent = initialYaw;
        yawRelativeTarget = 0f;

        pitchCurrent = positionTarget.localEulerAngles.x;
        if (pitchCurrent > 180f) pitchCurrent -= 360f;
        pitchTarget = pitchCurrent;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        // Actualizar targets a partir del input (delta style)
        float dx = lookInput.x * sensitivity * inputMultiplier;
        float dy = lookInput.y * sensitivity * inputMultiplier;

        // calcular el yaw objetivo acumulado desde input
        yawRelativeTarget += dx;
        // calcular el yaw objetivo absoluto (desde initialYaw)
        float desiredYaw = initialYaw + yawRelativeTarget;
        // obtener el yaw relativo firmado usando DeltaAngle (evita wrap-around)
        float signedDesiredRel = Mathf.DeltaAngle(initialYaw, desiredYaw);
        // clamp robusto del relativo firmado
        float clampedRel = Mathf.Clamp(signedDesiredRel, -maxYawFromStart, maxYawFromStart);
        // aplicar yaw final y suavizar
        float finalYaw = initialYaw + clampedRel;
        yawCurrent = Mathf.SmoothDampAngle(yawCurrent, finalYaw, ref yawVel, yawSmoothTime);
        if (playerBody != null)
            playerBody.localRotation = Quaternion.Euler(0f, yawCurrent, 0f);

        yawRelativeTarget = Mathf.DeltaAngle(initialYaw, yawCurrent);

        // Pitch (inverso para que mover el mouse hacia arriba mire hacia arriba)
        pitchTarget -= dy;
        pitchTarget = Mathf.Clamp(pitchTarget, minPitch, maxPitch);
        pitchCurrent = Mathf.SmoothDampAngle(pitchCurrent, pitchTarget, ref pitchVel, pitchSmoothTime);
        positionTarget.localRotation = Quaternion.Euler(pitchCurrent, 0f, 0f);

        // Local position small offset (muy sutil)
        Vector3 desiredLocal = defaultLocalPos + new Vector3(Mathf.Clamp(lookInput.x, -1f, 1f) * sideAmount,
                                                            Mathf.Clamp(-lookInput.y, -1f, 1f) * verticalAmount,
                                                            0f);
        positionTarget.localPosition = Vector3.SmoothDamp(positionTarget.localPosition, desiredLocal, ref localPosVel, localPosSmoothTime);

        // Highlight raycast
        DoHighlightRaycast();
    }

    private void DoHighlightRaycast()
    {
        Transform camTransform = transform;
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = Camera.current;
        }

        if (cam != null)
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            var hits = Physics.RaycastAll(ray, highlightDistance, highlightLayerMask);
            float bestDist = float.MaxValue;
            Highlightable best = null;
            foreach (var hinfo in hits)
            {
                var comp = hinfo.collider.GetComponent<Highlightable>();
                if (comp != null && hinfo.distance < bestDist)
                {
                    bestDist = hinfo.distance;
                    best = comp;
                }
            }

            if (best != null)
            {
                if (lastHighlighted != best)
                {
                    if (lastHighlighted != null) lastHighlighted.SetHighlighted(false);
                    lastHighlighted = best;
                    lastHighlighted.SetHighlighted(true);
                }
                return;
            }
        }

        if (lastHighlighted != null)
        {
            lastHighlighted.SetHighlighted(false);
            lastHighlighted = null;
        }
    }
    
    
    void OnGUI()
    {
        if (!showAngleDebug) return;

        float signedYaw = Mathf.DeltaAngle(initialYaw, playerBody ? playerBody.eulerAngles.y : 0f);
        float pitchLocal = positionTarget.localEulerAngles.x;
        if (pitchLocal > 180f) pitchLocal -= 360f;

        GUILayout.BeginArea(new Rect(10, 10, 300, 120));
        GUILayout.Label($"InitialYaw: {initialYaw:F1}");
        GUILayout.Label($"SignedYaw: {signedYaw:F1} (clamp +/-{maxYawFromStart})");
        GUILayout.Label($"PitchLocal: {pitchLocal:F1} (min {minPitch}, max {maxPitch})");
        GUILayout.Label($"LookInput: {lookInput.x:F2}, {lookInput.y:F2}");
        GUILayout.EndArea();
    }
}