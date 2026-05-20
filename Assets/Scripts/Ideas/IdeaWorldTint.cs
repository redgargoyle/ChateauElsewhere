using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public class IdeaWorldTint : MonoBehaviour
{
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color noIdeaColor = new Color(0f, 0f, 0f, 0f);

    private void Reset()
    {
        targetGraphic = GetComponent<Graphic>();
        ConfigureGraphic();
    }

    private void Awake()
    {
        ResolveGraphic();
        ConfigureGraphic();
    }

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

    private void HandleCurrentIdeaChanged(IdeaDefinition idea)
    {
        ApplyIdea(idea);
    }

    private void ApplyIdea(IdeaDefinition idea)
    {
        ResolveGraphic();

        if (targetGraphic == null)
        {
            return;
        }

        if (idea == null)
        {
            targetGraphic.color = noIdeaColor;
            return;
        }

        Color color = idea.Tint;
        color.a = idea.TintStrength;
        targetGraphic.color = color;
    }

    private void ResolveGraphic()
    {
        if (targetGraphic == null)
        {
            targetGraphic = GetComponent<Graphic>();
        }
    }

    private void ConfigureGraphic()
    {
        if (targetGraphic != null)
        {
            targetGraphic.raycastTarget = false;
        }
    }
}
