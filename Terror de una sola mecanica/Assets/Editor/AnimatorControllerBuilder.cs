using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Utility editor to create an AnimatorController with a simple BlendTree for Speed.
/// - Busca AnimationClips llamados "Idle", "Walk", "Run" en el proyecto y los usa si existen.
/// - Crea parámetros: float Speed
/// Uso: Menú "Tools/Build Simple Animator Controller"
/// Nota: Si no se encuentran clips, se crean capas/estados vacíos y deberás asignarlos manualmente.
/// </summary>
public static class AnimatorControllerBuilder
{
    [MenuItem("Tools/Build Simple Animator Controller")]
    public static void Build()
    {
        string path = EditorUtility.SaveFilePanelInProject("Save Animator Controller", "PlayerController", "controller", "Choose save location for the generated Animator Controller");
        if (string.IsNullOrEmpty(path)) return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var root = controller.layers[0].stateMachine;

        // Añadir parámetro Speed
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // Buscar clips por nombre
        var guids = AssetDatabase.FindAssets("t:AnimationClip");
        AnimationClip idle = null, walk = null, run = null;
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            if (clip == null) continue;
            if (idle == null && clip.name.ToLower().Contains("idle")) idle = clip;
            if (walk == null && clip.name.ToLower().Contains("walk")) walk = clip;
            if (run == null && clip.name.ToLower().Contains("run")) run = clip;
        }

        // Crear BlendTree state
        var blendTreeState = root.AddState("MoveBlend");
        var blendTree = new BlendTree();
        blendTree.name = "MoveBlendTree";
        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.parameters = new string[] { "Speed" };

        var tree = blendTree;
        // Crear entradas según clips encontrados
        if (idle != null) tree.AddChild(idle, 0f);
        if (walk != null) tree.AddChild(walk, 1f);
        if (run != null) tree.AddChild(run, 2f);

        // Guardar el BlendTree como Motion
        AnimatorControllerUtils.AddBlendTreeToState(blendTreeState, tree, controller.layers[0].stateMachine);

        // Crear default idle si existe
        if (idle != null)
        {
            var idleState = root.AddState("Idle");
            idleState.motion = idle;
            root.defaultState = idleState;
            // Transition Idle -> MoveBlend based on Speed > 0.1
            var t = idleState.AddTransition(blendTreeState);
            t.hasExitTime = false;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        }

        Debug.Log("AnimatorController creado en: " + path + ". Revisa los estados y ajusta parámetros/clips según tu proyecto.");
    }
}

/// <summary>
/// Small helper utilities since AnimatorController API can be verbose.
/// </summary>
public static class AnimatorControllerUtils
{
    public static void AddBlendTreeToState(AnimatorState state, BlendTree tree, AnimatorStateMachine sm)
    {
        // Create the blend tree asset in memory and assign it
        state.motion = tree;
    }
}
