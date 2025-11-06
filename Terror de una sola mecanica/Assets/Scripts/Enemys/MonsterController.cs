using System;
using UnityEngine;

/// <summary>
/// Controla el monstruo individual: tipo, estado del blend tree y comprobación de la pastilla correcta.
/// </summary>
[RequireComponent(typeof(Animator))]
public class MonsterController : MonoBehaviour
{
    [Tooltip("Identificador del monstruo (ej: 'Zombi', 'Lobo')")]
    public string monsterType = "Monster";

    [Tooltip("Nombre de la pastilla correcta para este monstruo (comparación case-insensitive)")]
    public string requiredPillName = "Pill";

    [Tooltip("Referenciar el Animator del prefab. Debe tener un parámetro entero para controlar el Blend Tree.")]
    public Animator animator;

    [Tooltip("Nombre del parámetro entero en el Animator que controla el Blend Tree. Debe existir en el Animator.")]
    public string animatorStateParam = "State";

    [Tooltip("Cuántos estados tiene el Blend Tree (0..count-1). Se elegirá uno aleatorio.")]
    public int blendTreeStatesCount = 1;

    private int activeState = -1;
    private int animatorStateHash;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animatorStateHash = Animator.StringToHash(animatorStateParam);
    }

    /// <summary>
    /// Activa un estado aleatorio del blend tree (setea el parámetro entero).
    /// </summary>
    public void ActivateRandomBlendState()
    {
        if (animator == null || blendTreeStatesCount <= 0)
        {
            Debug.LogWarning($"[MonsterController:{name}] Animator no asignado o blendTreeStatesCount inválido.");
            return;
        }

        activeState = UnityEngine.Random.Range(0, blendTreeStatesCount);
        animator.SetInteger(animatorStateHash, activeState);
        Debug.Log($"[MonsterController:{name}] Estado blend activado: {activeState}");
    }

    /// <summary>
    /// Comprueba si la pastilla recibida es la correcta para este monstruo.
    /// </summary>
    public bool CheckPill(string pillName)
    {
        if (string.IsNullOrWhiteSpace(pillName))
            return false;

        return string.Equals(pillName.Trim(), requiredPillName?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}