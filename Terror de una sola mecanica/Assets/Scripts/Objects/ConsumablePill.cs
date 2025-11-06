using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Representa una pastilla consumible en la mesa. Cuando se consume, dispara un evento
/// y desactiva el objeto visualmente para simular que fue tomada.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ConsumablePill : MonoBehaviour
{
    [Tooltip("Identificador o nombre de la pastilla (ej: 'Calmante')")]
    public string pillName = "Pill";

    [Tooltip("Partícula opcional que se reproducirá al consumirla")]
    public ParticleSystem consumeEffect;

    [Tooltip("Evento que se dispara al consumir la pastilla (útil para hooks desde el editor)")]
    public UnityEvent onConsume;

    private bool consumed = false;

    /// <summary>
    /// Llamar para consumir la pastilla. Protección para no consumir varias veces.
    /// </summary>
    public void Consume()
    {
        if (consumed) return;
        consumed = true;

        Debug.Log($"[ConsumablePill] Consumida: {pillName}");

        if (consumeEffect != null)
        {
            var ps = Instantiate(consumeEffect, transform.position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, ps.main.duration + 0.5f);
        }

        // Desactivar collider para que no pueda volver a seleccionarse
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Desactivar renderers para simular que fue tomada
        var rends = GetComponentsInChildren<Renderer>();
        foreach (var r in rends) r.enabled = false;

        onConsume?.Invoke();

        // Notificar al GameManager (si existe) para que valide la pastilla con el monstruo actual
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandlePillConsumed(this);
        }
        else
        {
            Debug.LogWarning("[ConsumablePill] No hay GameManager en la escena para procesar la pastilla.");
        }

        // Por defecto, destruir el objeto tras un tiempo corto
        Destroy(gameObject, 2f);
    }
}