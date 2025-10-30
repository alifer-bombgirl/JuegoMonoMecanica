using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Asigna aquí el Transform del cuerpo del jugador (el objeto padre)")]
    public Transform playerBody;

    [Header("Configuración de Vista")]
    [Tooltip("La velocidad de rotación de la cámara.")]
    public float sensitivity = 200f;

    [Tooltip("Ángulo máximo que se puede mirar hacia arriba.")]
    public float maxPitch = 85f;

    [Tooltip("Ángulo máximo que se puede mirar hacia abajo.")]
    public float minPitch = -85f;

    private Actions playerControls;

    private Vector2 lookInput;

    public float xRotation = 3f;

    void Awake()
    {
        playerControls = new Actions();

        playerControls.Player.Look.performed += context =>
        {
            lookInput = context.ReadValue<Vector2>();
        };

        playerControls.Player.Look.canceled += context =>
        {
            lookInput = Vector2.zero;
        };
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
    }

    void LateUpdate()
    {
        float mouseX = lookInput.x * sensitivity * Time.deltaTime;
        float mouseY = lookInput.y * sensitivity * Time.deltaTime;

        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }

}