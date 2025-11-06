using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestiona el ciclo principal: generar monstruo aleatorio, comprobar pastillas y gestionar día / game over.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Tooltip("Array de prefabs de monstruo. Asignar desde el Inspector.")]
    public GameObject[] monsterPrefabs;

    [Tooltip("Punto de spawn donde se instanciará el monstruo.")]
    public Transform spawnPoint;

    [Tooltip("Nombre opcional de la escena de GameOver. Si está vacío, el juego se pausará en Game Over.")]
    public string gameOverSceneName = "";

    private MonsterController currentMonster;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        SpawnRandomMonster();
    }

    /// <summary>
    /// Instancia un monstruo aleatorio del array y activa su blend tree.
    /// </summary>
    public void SpawnRandomMonster()
    {
        if (monsterPrefabs == null || monsterPrefabs.Length == 0)
        {
            Debug.LogError("[GameManager] No hay prefabs de monstruo asignados en monsterPrefabs.");
            return;
        }

        int idx = Random.Range(0, monsterPrefabs.Length);
        var prefab = monsterPrefabs[idx];
        if (prefab == null)
        {
            Debug.LogError("[GameManager] Prefab seleccionado es null.");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        var go = Instantiate(prefab, pos, rot);
        currentMonster = go.GetComponent<MonsterController>();
        if (currentMonster == null)
        {
            Debug.LogError("[GameManager] El prefab instanciado no contiene MonsterController.");
            return;
        }

        currentMonster.ActivateRandomBlendState();
    }

    /// <summary>
    /// Llamado por ConsumablePill cuando una pastilla es consumida.
    /// </summary>
    public void HandlePillConsumed(ConsumablePill pill)
    {
        if (currentMonster == null)
        {
            Debug.LogWarning("[GameManager] No hay monstruo activo para comprobar la pastilla.");
            return;
        }

        bool correct = currentMonster.CheckPill(pill.pillName);
        if (correct)
        {
            Debug.Log("[GameManager] Pastilla correcta. Cambiando de día...");
            StartCoroutine(NextDayRoutine());
        }
        else
        {
            Debug.Log("[GameManager] Pastilla incorrecta. GAME OVER.");
            GameOver();
        }
    }

    private IEnumerator NextDayRoutine()
    {
        // Puedes añadir aquí animaciones, sonido, fade out, etc.
        yield return new WaitForSeconds(0.5f);
        // Volver a cargar la misma escena para simular "cambiar de día"
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void GameOver()
    {
        if (!string.IsNullOrEmpty(gameOverSceneName))
        {
            SceneManager.LoadScene(gameOverSceneName);
        }
        else
        {
            // Si no hay escena de GameOver, pausamos el juego y dejamos un mensaje.
            Time.timeScale = 0f;
            Debug.Log("[GameManager] Game Over (Time.timeScale = 0). Reinicia la escena manualmente o asigna 'gameOverSceneName'.");
        }
    }
}