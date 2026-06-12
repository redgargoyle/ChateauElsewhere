using System;
using UnityEngine;

[Serializable]
public sealed class DiningFoodVisualState
{
    [SerializeField] private GameObject coveredDinnerGroup;
    [SerializeField] private GameObject fullFoodGroup;
    [SerializeField] private GameObject halfFoodGroup;
    [SerializeField] private GameObject emptyFoodGroup;

    public bool HasCovered => coveredDinnerGroup != null;
    public bool HasFull => fullFoodGroup != null;
    public bool HasHalf => halfFoodGroup != null;
    public bool HasEmpty => emptyFoodGroup != null;
    public bool HasAnyAssigned => HasCovered || HasFull || HasHalf || HasEmpty;

    public void Configure(
        GameObject coveredDinner,
        GameObject fullFood,
        GameObject halfFood,
        GameObject emptyFood)
    {
        coveredDinnerGroup = coveredDinner;
        fullFoodGroup = fullFood;
        halfFoodGroup = halfFood;
        emptyFoodGroup = emptyFood;
    }

    public void ShowCovered()
    {
        SetActive(coveredDinnerGroup, true);
        SetActive(fullFoodGroup, false);
        SetActive(halfFoodGroup, false);
        SetActive(emptyFoodGroup, false);
    }

    public void ShowFull()
    {
        SetActive(coveredDinnerGroup, false);
        SetActive(fullFoodGroup, true);
        SetActive(halfFoodGroup, false);
        SetActive(emptyFoodGroup, false);
    }

    public void ShowHalf()
    {
        SetActive(coveredDinnerGroup, false);
        SetActive(fullFoodGroup, false);
        SetActive(halfFoodGroup, true);
        SetActive(emptyFoodGroup, false);
    }

    public void ShowEmpty()
    {
        SetActive(coveredDinnerGroup, false);
        SetActive(fullFoodGroup, false);
        SetActive(halfFoodGroup, false);
        SetActive(emptyFoodGroup, true);
    }

    public void HideAll()
    {
        SetActive(coveredDinnerGroup, false);
        SetActive(fullFoodGroup, false);
        SetActive(halfFoodGroup, false);
        SetActive(emptyFoodGroup, false);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
