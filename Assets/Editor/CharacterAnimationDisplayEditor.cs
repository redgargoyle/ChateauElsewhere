using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterAnimationDisplay))]
public sealed class CharacterAnimationDisplayEditor : Editor
{
    private const string EditAnimationMenuPath = "Dreadforge/Characters/Edit Selected Character Animations";
    private const string GameObjectMenuPath = "GameObject/Dreadforge/Edit Character Animations";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CharacterAnimationDisplay display = (CharacterAnimationDisplay)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "The Animator lives on the AnimationDisplay child so runtime character scaling cannot move the actor root. " +
            "Use the button below to select that child and bind Unity's Animation window to it.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!CharacterAnimationEditing.TryResolveAnimatorHost(
                   display.gameObject,
                   out _)))
        {
            if (GUILayout.Button("Edit Animations in Animation Window", GUILayout.Height(28f)))
            {
                CharacterAnimationEditing.OpenFor(display.gameObject);
            }
        }
    }

    [MenuItem(EditAnimationMenuPath, false, 120)]
    [MenuItem(GameObjectMenuPath, false, 30)]
    private static void EditSelectedCharacterAnimations()
    {
        CharacterAnimationEditing.OpenFor(Selection.activeGameObject);
    }

    [MenuItem(EditAnimationMenuPath, true)]
    [MenuItem(GameObjectMenuPath, true)]
    private static bool CanEditSelectedCharacterAnimations()
    {
        return CharacterAnimationEditing.TryResolveAnimatorHost(Selection.activeGameObject, out _);
    }
}

public static class CharacterAnimationEditing
{
    private const string AnimationWindowTypeName = "UnityEditor.AnimationWindow";

    public static bool TryResolveAnimatorHost(GameObject selectedObject, out GameObject animatorHost)
    {
        animatorHost = null;

        if (selectedObject == null)
        {
            return false;
        }

        CharacterAnimationDisplay display = selectedObject.GetComponent<CharacterAnimationDisplay>() ??
            selectedObject.GetComponentInParent<CharacterAnimationDisplay>(true);

        if (display == null || display.AnimationDisplay == null)
        {
            return false;
        }

        Animator animator = display.AnimationDisplay.GetComponent<Animator>();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        animatorHost = animator.gameObject;
        return true;
    }

    public static bool OpenFor(GameObject selectedObject)
    {
        if (!TryResolveAnimatorHost(selectedObject, out GameObject animatorHost))
        {
            Debug.LogError(
                "Select a character root with CharacterAnimationDisplay, or its AnimationDisplay child, before editing animations.");
            return false;
        }

        Type animationWindowType = typeof(EditorWindow).Assembly.GetType(AnimationWindowTypeName);

        if (animationWindowType == null)
        {
            Debug.LogError("Unity's Animation window type could not be found.");
            return false;
        }

        Selection.activeGameObject = animatorHost;
        EditorGUIUtility.PingObject(animatorHost);

        EditorWindow animationWindow = EditorWindow.GetWindow(animationWindowType);
        animationWindow.Show();
        animationWindow.Focus();

        // Selection is applied once before and once after the window is created.
        // The delayed assignment makes a newly opened Animation window bind to the
        // visual child instead of retaining its previous empty root selection.
        EditorApplication.delayCall += () =>
        {
            if (animatorHost != null)
            {
                Selection.activeGameObject = animatorHost;
                EditorGUIUtility.PingObject(animatorHost);
                animationWindow.Repaint();
            }
        };

        return true;
    }

    public static bool IsAnimationWindowOpen()
    {
        EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();

        for (int i = 0; i < windows.Length; i++)
        {
            if (windows[i] != null && windows[i].GetType().FullName == AnimationWindowTypeName)
            {
                return true;
            }
        }

        return false;
    }
}

[InitializeOnLoad]
public static class CharacterAnimationRootSelectionBridge
{
    private static bool redirectQueued;

    static CharacterAnimationRootSelectionBridge()
    {
        Selection.selectionChanged -= HandleSelectionChanged;
        Selection.selectionChanged += HandleSelectionChanged;
    }

    private static void HandleSelectionChanged()
    {
        if (redirectQueued || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        GameObject selectedRoot = Selection.activeGameObject;

        if (selectedRoot == null ||
            selectedRoot.GetComponent<CharacterAnimationDisplay>() == null ||
            !CharacterAnimationEditing.IsAnimationWindowOpen() ||
            !CharacterAnimationEditing.TryResolveAnimatorHost(selectedRoot, out GameObject animatorHost) ||
            animatorHost == selectedRoot)
        {
            return;
        }

        redirectQueued = true;
        EditorApplication.delayCall += () =>
        {
            redirectQueued = false;

            if (selectedRoot != null && animatorHost != null && Selection.activeGameObject == selectedRoot)
            {
                Selection.activeGameObject = animatorHost;
                EditorGUIUtility.PingObject(animatorHost);
            }
        };
    }
}
