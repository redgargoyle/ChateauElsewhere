using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Display Scale/Character Display Scale Controller")]
public sealed class CharacterDisplayScaleController : MonoBehaviour
{
    [SerializeField] private CharacterDisplayScaleCatalog catalog;

    private static CharacterDisplayScaleController activeController;
    private readonly HashSet<CharacterDisplayScaleSubject> subjects =
        new HashSet<CharacterDisplayScaleSubject>();
    private readonly List<CharacterDisplayScaleSubject> updateBuffer =
        new List<CharacterDisplayScaleSubject>();

    public CharacterDisplayScaleCatalog Catalog => catalog;
    public static CharacterDisplayScaleController ActiveController => activeController;

    private void OnEnable()
    {
        if (activeController != null && activeController != this)
        {
            Debug.LogError(
                "Only one CharacterDisplayScaleController may be active. Disabling the duplicate controller.",
                this);
            enabled = false;
            return;
        }

        activeController = this;
        RefreshSubjects();
    }

    private void OnDisable()
    {
        if (activeController == this)
        {
            activeController = null;
        }

        subjects.Clear();
        updateBuffer.Clear();
    }

    private void LateUpdate()
    {
        ApplyRegisteredSubjects();
    }

    public bool TryApplySubject(CharacterDisplayScaleSubject subject)
    {
        if (catalog == null ||
            subject == null ||
            !subject.isActiveAndEnabled ||
            !subject.HasValidVisualScaleRoot() ||
            !subject.TryGetContext(out ICharacterDisplayScaleContext context))
        {
            return false;
        }

        string roomId = context.CurrentRoomId;
        float roomLocalFootY = context.CurrentRoomLocalFootY;

        if (!catalog.TryEvaluateScale(
                roomId,
                subject.CharacterId,
                context.CurrentDisplayState,
                roomLocalFootY,
                out float targetScale))
        {
            return false;
        }

        Transform visualScaleRoot = subject.VisualScaleRoot;
        Vector3 requestedScale = subject.GetDeterministicScaleVector(targetScale);

        if (visualScaleRoot.localScale != requestedScale)
        {
            // The sole runtime Butler/Guest body display-scale assignment.
            visualScaleRoot.localScale = requestedScale;
        }

        return true;
    }

    public void ApplyRegisteredSubjects()
    {
        updateBuffer.Clear();
        updateBuffer.AddRange(subjects);

        for (int i = 0; i < updateBuffer.Count; i++)
        {
            CharacterDisplayScaleSubject subject = updateBuffer[i];

            if (subject == null)
            {
                subjects.Remove(subject);
                continue;
            }

            TryApplySubject(subject);
        }
    }

    public void RefreshSubjects()
    {
        subjects.Clear();
        CharacterDisplayScaleSubject[] sceneSubjects = FindObjectsByType<CharacterDisplayScaleSubject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < sceneSubjects.Length; i++)
        {
            CharacterDisplayScaleSubject subject = sceneSubjects[i];

            if (subject != null && subject.isActiveAndEnabled)
            {
                subjects.Add(subject);
            }
        }
    }

    public static void Register(CharacterDisplayScaleSubject subject)
    {
        if (activeController != null && subject != null)
        {
            activeController.subjects.Add(subject);
        }
    }

    public static void Unregister(CharacterDisplayScaleSubject subject)
    {
        if (activeController != null && subject != null)
        {
            activeController.subjects.Remove(subject);
        }
    }

    internal void Configure(CharacterDisplayScaleCatalog scaleCatalog)
    {
        catalog = scaleCatalog;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(CharacterDisplayScaleCatalog scaleCatalog)
    {
        catalog = scaleCatalog;
    }
#endif
}
