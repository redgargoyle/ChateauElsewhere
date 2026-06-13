using System;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterVisualProfile", menuName = "ChataeuChatilly/Characters/Character Visual Profile")]
public sealed class CharacterVisualProfile : ScriptableObject
{
    [SerializeField] private string characterId = "Character";
    [SerializeField] [Min(0.01f)] private float heightScaleMultiplier = 1f;
    [SerializeField] [Min(1f)] private float standingVisualHeight = 290f;
    [SerializeField] [Min(1f)] private float sittingVisualHeight = 220f;
    [SerializeField] private Vector2 footPivotNormalized = new Vector2(0.5f, 0f);
    [SerializeField] private string[] rendererPathHints = Array.Empty<string>();
    [SerializeField] private int bodySortingOffset;
    [SerializeField] private int coatSortingOffset = 1;
    [SerializeField] private int shadowSortingOffset = -2;
    [SerializeField] private Vector2 standingVisualOffset;
    [SerializeField] private Vector2 sittingVisualOffset;

    public string CharacterId => characterId;
    public float HeightScaleMultiplier => heightScaleMultiplier;
    public float StandingVisualHeight => standingVisualHeight;
    public float SittingVisualHeight => sittingVisualHeight;
    public Vector2 FootPivotNormalized => footPivotNormalized;
    public int BodySortingOffset => bodySortingOffset;
    public int CoatSortingOffset => coatSortingOffset;
    public int ShadowSortingOffset => shadowSortingOffset;
    public Vector2 StandingVisualOffset => standingVisualOffset;
    public Vector2 SittingVisualOffset => sittingVisualOffset;

    private void OnValidate()
    {
        Sanitize();
    }

    public void Configure(
        string profileCharacterId,
        float profileHeightScaleMultiplier,
        float profileStandingVisualHeight,
        float profileSittingVisualHeight,
        Vector2 profileFootPivotNormalized,
        int profileBodySortingOffset,
        int profileCoatSortingOffset,
        int profileShadowSortingOffset)
    {
        characterId = string.IsNullOrWhiteSpace(profileCharacterId) ? "Character" : profileCharacterId.Trim();
        heightScaleMultiplier = profileHeightScaleMultiplier;
        standingVisualHeight = profileStandingVisualHeight;
        sittingVisualHeight = profileSittingVisualHeight;
        footPivotNormalized = profileFootPivotNormalized;
        bodySortingOffset = profileBodySortingOffset;
        coatSortingOffset = profileCoatSortingOffset;
        shadowSortingOffset = profileShadowSortingOffset;
        Sanitize();
    }

    public float GetVisualHeight(bool seated)
    {
        return seated ? sittingVisualHeight : standingVisualHeight;
    }

    public Vector2 GetStateOffset(bool seated)
    {
        return seated ? sittingVisualOffset : standingVisualOffset;
    }

    public int GetSortingOffsetForRenderer(Transform rendererTransform)
    {
        string hierarchyName = GetHierarchyName(rendererTransform);

        if (ContainsOrdinalIgnoreCase(hierarchyName, "shadow"))
        {
            return shadowSortingOffset;
        }

        if (ContainsOrdinalIgnoreCase(hierarchyName, "coat") ||
            ContainsOrdinalIgnoreCase(hierarchyName, "held") ||
            ContainsOrdinalIgnoreCase(hierarchyName, "item"))
        {
            return coatSortingOffset;
        }

        return bodySortingOffset;
    }

    public bool HasRendererHint(Transform rendererTransform)
    {
        if (rendererPathHints == null || rendererPathHints.Length == 0)
        {
            return false;
        }

        string hierarchyName = GetHierarchyName(rendererTransform);

        for (int i = 0; i < rendererPathHints.Length; i++)
        {
            if (ContainsOrdinalIgnoreCase(hierarchyName, rendererPathHints[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void Sanitize()
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            characterId = "Character";
        }

        heightScaleMultiplier = Mathf.Max(0.01f, heightScaleMultiplier);
        standingVisualHeight = Mathf.Max(1f, standingVisualHeight);
        sittingVisualHeight = Mathf.Max(1f, sittingVisualHeight);
        footPivotNormalized = new Vector2(
            Mathf.Clamp01(footPivotNormalized.x),
            Mathf.Clamp01(footPivotNormalized.y));
    }

    private static bool ContainsOrdinalIgnoreCase(string haystack, string needle)
    {
        return !string.IsNullOrWhiteSpace(haystack) &&
            !string.IsNullOrWhiteSpace(needle) &&
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetHierarchyName(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string value = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            value = current.name + "/" + value;
            current = current.parent;
        }

        return value;
    }
}
