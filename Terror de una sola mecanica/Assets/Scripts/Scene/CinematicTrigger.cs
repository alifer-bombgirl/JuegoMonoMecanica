using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Trigger sencillo para reproducir una Timeline (PlayableDirector). Desactiva el control del jugador
/// (CameraMovement + InteractionHandler) mientras la cinemática está en reproducción y los reactiva al terminar.
/// Añadir un collider con IsTrigger activado al GameObject que contenga este componente.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CinematicTrigger : MonoBehaviour
{
    public PlayableDirector director;

    [Tooltip("Si verdadero, el trigger solo se activa una vez (se desactiva después)")]
    public bool playOnlyOnce = true;

    private bool played = false;

    private CameraMovement camMovement;
    private InteractionHandler interactionHandler;

    void Awake()
    {
        camMovement = FindObjectOfType<CameraMovement>();
        interactionHandler = FindObjectOfType<InteractionHandler>();
        if (director != null)
        {
            director.stopped += OnDirectorStopped;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (played && playOnlyOnce) return;

        // Podríamos filtrar por Tag del jugador si se desea.
        if (director != null)
        {
            // Deshabilitar control de jugador
            if (camMovement != null) camMovement.enabled = false;
            if (interactionHandler != null) interactionHandler.enabled = false;

            director.Play();
            played = true;
        }
        else
        {
            Debug.LogWarning("CinematicTrigger: PlayableDirector no asignado.");
        }
    }

    private void OnDirectorStopped(PlayableDirector obj)
    {
        // Reactivar control
        if (camMovement != null) camMovement.enabled = true;
        if (interactionHandler != null) interactionHandler.enabled = true;
    }

    void OnDestroy()
    {
        if (director != null) director.stopped -= OnDirectorStopped;
    }
}
