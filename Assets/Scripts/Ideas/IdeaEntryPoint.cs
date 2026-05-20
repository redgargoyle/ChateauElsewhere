using UnityEngine;
using UnityEngine.EventSystems;

public class IdeaEntryPoint : MonoBehaviour, IPointerClickHandler
{
    public enum EntryAction
    {
        ExploreIdea,
        EnterElsewhere,
        ClearIdea
    }

    [SerializeField] private EntryAction action = EntryAction.ExploreIdea;
    [SerializeField] private string ideaId = "inheritance";

    public void OnPointerClick(PointerEventData eventData)
    {
        Activate();
    }

    public void Activate()
    {
        IdeaManager manager = IdeaManager.GetOrCreate();

        switch (action)
        {
            case EntryAction.EnterElsewhere:
                manager.EnterElsewhere();
                break;
            case EntryAction.ClearIdea:
                manager.ClearIdea();
                break;
            default:
                manager.ExploreIdea(ideaId);
                break;
        }
    }
}
