using System;
using UnityEngine;

[DisallowMultipleComponent]
public class IdeaDimension : MonoBehaviour
{
    [Serializable]
    public sealed class Variant
    {
        [SerializeField] private string ideaId;
        [SerializeField] private GameObject root;
        [TextArea(2, 5)]
        [SerializeField] private string examineText;

        public string IdeaId => IdeaDefinition.NormalizeId(ideaId);
        public GameObject Root => root;
        public string ExamineText => examineText ?? string.Empty;
    }

    [Header("Neutral Dimension")]
    [SerializeField] private GameObject neutralRoot;
    [TextArea(2, 5)]
    [SerializeField] private string neutralExamineText;
    [SerializeField] private bool showNeutralWhenNoIdea = true;
    [SerializeField] private bool showNeutralWhenIdeaHasNoVariant = true;

    [Header("Idea Dimensions")]
    [SerializeField] private Variant[] variants = new Variant[0];

    public string CurrentExamineText { get; private set; }

    private void OnEnable()
    {
        IdeaManager.AnyCurrentIdeaChanged += HandleCurrentIdeaChanged;

        IdeaManager manager = Application.isPlaying ? IdeaManager.GetOrCreate() : IdeaManager.Instance;
        ApplyIdea(manager != null ? manager.CurrentIdea : null);
    }

    private void OnDisable()
    {
        IdeaManager.AnyCurrentIdeaChanged -= HandleCurrentIdeaChanged;
    }

    public bool HasVariantForIdea(string ideaId)
    {
        string normalizedId = IdeaDefinition.NormalizeId(ideaId);

        if (variants == null)
        {
            return false;
        }

        for (int i = 0; i < variants.Length; i++)
        {
            Variant variant = variants[i];

            if (variant != null && variant.IdeaId == normalizedId)
            {
                return true;
            }
        }

        return false;
    }

    public void ApplyIdeaById(string ideaId)
    {
        ApplyIdeaId(IdeaDefinition.NormalizeId(ideaId));
    }

    private void HandleCurrentIdeaChanged(IdeaDefinition idea)
    {
        ApplyIdea(idea);
    }

    private void ApplyIdea(IdeaDefinition idea)
    {
        ApplyIdeaId(idea != null ? idea.Id : string.Empty);
    }

    private void ApplyIdeaId(string currentIdeaId)
    {
        bool hasIdea = !string.IsNullOrEmpty(currentIdeaId);
        bool matchedVariant = false;
        CurrentExamineText = neutralExamineText ?? string.Empty;

        if (variants != null)
        {
            for (int i = 0; i < variants.Length; i++)
            {
                Variant variant = variants[i];

                if (variant == null)
                {
                    continue;
                }

                bool isActiveVariant = hasIdea && variant.IdeaId == currentIdeaId;
                SetRootActive(variant.Root, isActiveVariant);

                if (isActiveVariant)
                {
                    matchedVariant = true;
                    CurrentExamineText = variant.ExamineText;
                }
            }
        }

        bool showNeutral =
            (!hasIdea && showNeutralWhenNoIdea) ||
            (hasIdea && !matchedVariant && showNeutralWhenIdeaHasNoVariant);

        SetRootActive(neutralRoot, showNeutral);
    }

    private void SetRootActive(GameObject root, bool active)
    {
        if (root == null)
        {
            return;
        }

        if (root == gameObject && !active)
        {
            Debug.LogWarning($"IdeaDimension on '{name}' cannot hide its own GameObject. Assign child roots for neutral and Idea variants.", this);
            return;
        }

        if (root.activeSelf != active)
        {
            root.SetActive(active);
        }
    }
}
