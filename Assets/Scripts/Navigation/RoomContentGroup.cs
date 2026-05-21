using System;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomContentGroup : MonoBehaviour
{
    // Attach this to one root GameObject per room. When RoomNavigationManager's
    // currentRoom changes, it activates the matching RoomContentGroup and
    // deactivates the others, so each room can carry its own images, doors,
    // animators, sounds, and other preloaded content.
    [Header("Room")]
    [SerializeField] private string roomName;
    [SerializeField] private Texture roomBackgroundTexture;
    [Header("Child Renderer Defaults")]
    [SerializeField] private bool applyVisibleDefaultsToChildRenderers = true;
    [SerializeField] private string defaultSortingLayerName = "Background";
    [SerializeField] private int defaultSpriteSortingOrder = 20;
    [SerializeField] private int defaultParticleSortingOrder = 40;
    [SerializeField] private bool onlyAdjustDefaultSorting = true;

    public string RoomName => GetEffectiveRoomName();
    public Texture RoomBackgroundTexture => roomBackgroundTexture;

    private void Reset()
    {
        FillRoomNameFromObject();
    }

    private void OnValidate()
    {
        FillRoomNameFromObject();
        ApplyChildRendererVisibilityDefaults();
    }

    private void OnEnable()
    {
        ApplyChildRendererVisibilityDefaults();
    }

    private void OnTransformChildrenChanged()
    {
        ApplyChildRendererVisibilityDefaults();
    }

    public void RefreshInferredRoomName()
    {
        FillRoomNameFromObject();
    }

    public void SetRoomName(string value)
    {
        // The GameObject name is the source of truth. This field is only a
        // readable cache in the Inspector so duplicated rooms cannot keep an old
        // serialized name and steal another room's doors.
        roomName = string.IsNullOrWhiteSpace(value) ? ParseRoomNameFromObject(gameObject.name) : value.Trim();
    }

    public void SetRoomBackgroundTexture(Texture texture)
    {
        roomBackgroundTexture = texture;
    }

    public bool TryGetRoomBackgroundTexture(out Texture texture)
    {
        texture = roomBackgroundTexture;
        return texture != null;
    }

    public void ApplyChildRendererVisibilityDefaults()
    {
        if (!applyVisibleDefaultsToChildRenderers)
        {
            return;
        }

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];

            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.enabled = true;
            ApplyDefaultSorting(spriteRenderer, defaultSpriteSortingOrder);
        }

        ParticleSystemRenderer[] particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);

        for (int i = 0; i < particleRenderers.Length; i++)
        {
            ParticleSystemRenderer particleRenderer = particleRenderers[i];

            if (particleRenderer == null)
            {
                continue;
            }

            particleRenderer.enabled = true;
            ApplyDefaultSorting(particleRenderer, defaultParticleSortingOrder);
        }

        StaticSetImagePlayer[] imagePlayers = GetComponentsInChildren<StaticSetImagePlayer>(true);

        for (int i = 0; i < imagePlayers.Length; i++)
        {
            StaticSetImagePlayer imagePlayer = imagePlayers[i];

            if (imagePlayer == null)
            {
                continue;
            }

            imagePlayer.playOnEnable = true;
            imagePlayer.overrideSpriteSorting = true;

            if (string.IsNullOrWhiteSpace(imagePlayer.spriteSortingLayerName) ||
                imagePlayer.spriteSortingLayerName == "Default")
            {
                imagePlayer.spriteSortingLayerName = GetVisibleSortingLayerName();
            }

            if (imagePlayer.spriteSortingOrder == 0)
            {
                imagePlayer.spriteSortingOrder = defaultSpriteSortingOrder;
            }
        }
    }

    private string GetEffectiveRoomName()
    {
        return ParseRoomNameFromObject(gameObject.name);
    }

    private void FillRoomNameFromObject()
    {
        roomName = ParseRoomNameFromObject(gameObject.name);
    }

    private void ApplyDefaultSorting(Renderer renderer, int sortingOrder)
    {
        if (renderer == null)
        {
            return;
        }

        if (onlyAdjustDefaultSorting && (renderer.sortingLayerID != 0 || renderer.sortingOrder != 0))
        {
            return;
        }

        renderer.sortingLayerName = GetVisibleSortingLayerName();
        renderer.sortingOrder = sortingOrder;
    }

    private string GetVisibleSortingLayerName()
    {
        if (SortingLayerExists(defaultSortingLayerName))
        {
            return defaultSortingLayerName;
        }

        return "Default";
    }

    private static bool SortingLayerExists(string sortingLayerName)
    {
        if (string.IsNullOrWhiteSpace(sortingLayerName))
        {
            return false;
        }

        return string.Equals(sortingLayerName, "Default", StringComparison.OrdinalIgnoreCase) ||
            SortingLayer.NameToID(sortingLayerName) != 0;
    }

    private static string ParseRoomNameFromObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return string.Empty;
        }

        string cleanName = objectName.Trim();

        // This lets a room object be named either "Room_StorageCloset" or just
        // "StorageCloset". "Cam_" is accepted only for old scenes from before
        // rooms were split away from map camera buttons.
        if (cleanName.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Room_".Length);
        }
        else if (cleanName.StartsWith("Cam_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Cam_".Length);
        }

        return cleanName.Replace('_', ' ').Trim();
    }
}
