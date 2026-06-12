using UnityEngine;

[DisallowMultipleComponent]
public sealed class DiningFoodVisualState : MonoBehaviour
{
    [SerializeField] private GameObject coveredDinnerGroup;
    [SerializeField] private GameObject fullFoodGroup;
    [SerializeField] private GameObject halfFoodGroup;
    [SerializeField] private GameObject emptyFoodGroup;

    public bool HasAnyFoodReference =>
        coveredDinnerGroup != null ||
        fullFoodGroup != null ||
        halfFoodGroup != null ||
        emptyFoodGroup != null;
    public bool HasHalfFoodReference => halfFoodGroup != null;

    public void ConfigureIfMissing(
        GameObject coveredDinner,
        GameObject fullFood,
        GameObject halfFood,
        GameObject emptyFood)
    {
        if (coveredDinnerGroup == null)
        {
            coveredDinnerGroup = coveredDinner;
        }

        if (fullFoodGroup == null)
        {
            fullFoodGroup = fullFood;
        }

        if (halfFoodGroup == null)
        {
            halfFoodGroup = halfFood;
        }

        if (emptyFoodGroup == null)
        {
            emptyFoodGroup = emptyFood;
        }
    }

    public void ShowCovered()
    {
        ShowOnly(coveredDinnerGroup);
    }

    public void ShowFull()
    {
        ShowOnly(fullFoodGroup);
    }

    public void ShowHalf()
    {
        ShowOnly(halfFoodGroup);
    }

    public void ShowEmpty()
    {
        ShowOnly(emptyFoodGroup);
    }

    public void HideAll()
    {
        SetActiveSafe(coveredDinnerGroup, false);
        SetActiveSafe(fullFoodGroup, false);
        SetActiveSafe(halfFoodGroup, false);
        SetActiveSafe(emptyFoodGroup, false);
    }

    private void ShowOnly(GameObject activeGroup)
    {
        SetActiveSafe(coveredDinnerGroup, coveredDinnerGroup == activeGroup);
        SetActiveSafe(fullFoodGroup, fullFoodGroup == activeGroup);
        SetActiveSafe(halfFoodGroup, halfFoodGroup == activeGroup);
        SetActiveSafe(emptyFoodGroup, emptyFoodGroup == activeGroup);
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
