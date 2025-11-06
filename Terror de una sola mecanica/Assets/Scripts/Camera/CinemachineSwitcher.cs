using UnityEngine;

/// <summary>
/// Switcher simple para activar/desactivar cámaras (útil para Virtual Cameras de Cinemachine).
/// Implementa un cambio por índice (activa el index y desactiva los demás).
/// </summary>
public class CinemachineSwitcher : MonoBehaviour
{
    [Tooltip("Lista de GameObjects que representan cámaras virtuales. Puede ser los GameObjects que contienen CinemachineVirtualCamera.")]
    public GameObject[] virtualCameras;

    [Tooltip("Índice de cámara activa al iniciar (-1 = ninguna)")]
    public int startIndex = -1;

    void Start()
    {
        if (startIndex >= 0 && startIndex < virtualCameras.Length)
        {
            SwitchTo(startIndex);
        }
    }

    /// <summary>
    /// Activa la cámara en index y desactiva las demás.
    /// </summary>
    public void SwitchTo(int index)
    {
        if (virtualCameras == null || virtualCameras.Length == 0) return;
        if (index < 0 || index >= virtualCameras.Length) return;

        for (int i = 0; i < virtualCameras.Length; i++)
        {
            var go = virtualCameras[i];
            if (go != null)
                go.SetActive(i == index);
        }
    }

    /// <summary>
    /// Busca el índice del GameObject y lo selecciona.
    /// </summary>
    public void SwitchTo(GameObject cam)
    {
        if (virtualCameras == null) return;
        for (int i = 0; i < virtualCameras.Length; i++)
        {
            if (virtualCameras[i] == cam)
            {
                SwitchTo(i);
                return;
            }
        }
    }
}
