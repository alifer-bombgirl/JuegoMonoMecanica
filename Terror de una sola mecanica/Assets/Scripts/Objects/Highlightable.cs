using UnityEngine;

/// <summary>
/// Componente simple para permitir que un objeto se "resalte" cuando la cámara lo apunta.
/// Implementación basada en activar la emission del material localmente (se instancia el material al asignarlo).
/// Añadir este componente a los botes de pastillas o al prefab correspondiente.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class Highlightable : MonoBehaviour
{
    public Color highlightColor = Color.yellow;
    [Tooltip("Multiplicador de intensidad para la emisión")]
    public float intensity = 2f;

    private Renderer rend;
    private Color originalEmission = Color.black;
    private bool hadEmission = false;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend == null)
            rend = GetComponentInChildren<Renderer>();

        if (rend != null)
        {
            // Instanciar material para no afectar a otros objetos que compartan el material
            rend.material = new Material(rend.material);
            // Intentar leer color de emission si existe
            if (rend.material.HasProperty("_EmissionColor"))
            {
                originalEmission = rend.material.GetColor("_EmissionColor");
                hadEmission = true;
            }
        }
    }

    /// <summary>
    /// Activa o desactiva el resaltado.
    /// </summary>
    public void SetHighlighted(bool on)
    {
        if (rend == null) return;

        if (on)
        {
            if (rend.material.HasProperty("_EmissionColor"))
            {
                Color c = highlightColor * Mathf.LinearToGammaSpace(intensity);
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", c);
            }
        }
        else
        {
            if (rend.material.HasProperty("_EmissionColor"))
            {
                if (hadEmission)
                {
                    rend.material.SetColor("_EmissionColor", originalEmission);
                }
                else
                {
                    rend.material.SetColor("_EmissionColor", Color.black);
                    rend.material.DisableKeyword("_EMISSION");
                }
            }
        }
    }
}
