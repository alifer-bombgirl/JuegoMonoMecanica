using UnityEngine;

/// <summary>
/// Maneja la interacción del jugador con el objeto actualmente resaltado por la cámara.
/// Uso: añadir este componente a un GameObject (por ejemplo el Player) y asignar el CameraMovement si no se encuentra automáticamente.
/// La interacción por defecto es click izquierdo (mouse). Si quieres otro input, modifica Update().
/// </summary>
public class InteractionHandler : MonoBehaviour
{
    [Tooltip("Referencia al script CameraMovement que expone CurrentHighlighted. Si es null se buscará en escena.")]
    public CameraMovement cameraMovement;

    [Tooltip("Distancia máxima de interacción (fallback). El resaltado ya usa su propio rango; esto es solo comprobación adicional.")]
    public float maxInteractDistance = 4f;

    void Awake()
    {
        if (cameraMovement == null)
        {
            cameraMovement = FindObjectOfType<CameraMovement>();
            if (cameraMovement == null)
            {
                Debug.LogWarning("InteractionHandler: no se encontró CameraMovement en la escena. Asigne la referencia desde el inspector.");
            }
        }
    }

    void Update()
    {
        // Input simple: click izquierdo
        if (Input.GetMouseButtonDown(0))
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        if (cameraMovement == null) return;

        var highlighted = cameraMovement.CurrentHighlighted;
        if (highlighted == null) return;

        // Comprobar distancia aproximada usando el transform del highlighted (si tiene)
        var ht = highlighted.transform;
        var cam = Camera.main;
        if (cam != null)
        {
            float d = Vector3.Distance(cam.transform.position, ht.position);
            if (d > maxInteractDistance)
            {
                Debug.Log("InteractionHandler: objeto resaltado fuera de alcance.");
                return;
            }
        }

        var pill = highlighted.GetComponent<ConsumablePill>();
        if (pill != null)
        {
            pill.Consume();
            return;
        }

        // Si no es una pastilla, podríamos añadir otra lógica (puerta, palanca, etc.)
        Debug.Log("InteractionHandler: objeto resaltado no tiene ConsumablePill.");
    }
}
