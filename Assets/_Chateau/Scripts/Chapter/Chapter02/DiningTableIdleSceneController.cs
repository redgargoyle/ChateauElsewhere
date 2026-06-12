using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DiningTableIdleSceneController : MonoBehaviour
{
    private static readonly string[] ExistingDiningTableNameFragments =
    {
        "correct_dining_table",
        "dining_table_set"
    };

    [SerializeField] private string diningRoomId = "Dining Room";
    [SerializeField] private string resourcePath = "Chapter2/DiningRoomDinnerStill";
    [SerializeField, Min(0.05f)] private float frameSeconds = 1.15f;
    [SerializeField] private bool pingPong = true;

    private readonly Dictionary<ActorRoomState, GuestVisualState> hiddenGuests = new Dictionary<ActorRoomState, GuestVisualState>();
    private readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Graphic, bool> hiddenGraphics = new Dictionary<Graphic, bool>();

    private Texture2D[] frames = Array.Empty<Texture2D>();
    private RoomContentGroup diningRoom;
    private CameraManager cameraManager;
    private Coroutine loopRoutine;
    private bool isShowing;
    private bool capturedOriginalBackground;
    private Texture originalRoomBackgroundTexture;
    private Texture originalCameraBackgroundTexture;
    private Texture currentDiningBackgroundTexture;

    public void Show(IReadOnlyList<ActorRoomState> guestActors)
    {
        if (!ResolveSceneTargets() || !LoadFramesIfNeeded())
        {
            return;
        }

        isShowing = true;
        CaptureOriginalBackground();
        HideGuestActors(guestActors);
        CaptureAndHideExistingDiningTableVisuals();
        ApplyDiningRoomBackground(frames[0]);

        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        if (frames.Length > 1)
        {
            loopRoutine = StartCoroutine(PlayLoop());
        }
    }

    public void Hide()
    {
        isShowing = false;

        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        RestoreDiningRoomBackground();
        RestoreExistingDiningTableVisuals();
        RestoreGuestActors();
    }

    private void LateUpdate()
    {
        if (!isShowing)
        {
            return;
        }

        CaptureAndHideExistingDiningTableVisuals();
        EnsureDiningRoomBackgroundApplied();
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        Hide();
    }

    private bool ResolveSceneTargets()
    {
        diningRoom = FindDiningRoom();

        if (diningRoom == null)
        {
            Debug.LogWarning($"Dining table idle scene could not find room '{diningRoomId}'.", this);
            return false;
        }

        if (cameraManager == null)
        {
            cameraManager = FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);
        }

        if (cameraManager == null)
        {
            Debug.LogWarning("Dining table idle scene could not find the CameraManager.", this);
            return false;
        }

        return true;
    }

    private bool LoadFramesIfNeeded()
    {
        if (frames != null && frames.Length > 0)
        {
            return true;
        }

        Texture2D[] loadedTextures = Resources.LoadAll<Texture2D>(resourcePath);

        if (loadedTextures != null && loadedTextures.Length > 0)
        {
            Array.Sort(loadedTextures, CompareAssetsByName);
            frames = loadedTextures;
            return true;
        }

        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(resourcePath);

        if (loadedSprites != null && loadedSprites.Length > 0)
        {
            Array.Sort(loadedSprites, CompareAssetsByName);
            List<Texture2D> spriteTextures = new List<Texture2D>();

            for (int i = 0; i < loadedSprites.Length; i++)
            {
                Texture2D texture = loadedSprites[i] != null ? loadedSprites[i].texture : null;

                if (texture != null && !spriteTextures.Contains(texture))
                {
                    spriteTextures.Add(texture);
                }
            }

            frames = spriteTextures.ToArray();
        }

        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning($"Dining table idle scene could not load Resources/{resourcePath}.", this);
            return false;
        }

        return true;
    }

    private void CaptureOriginalBackground()
    {
        if (capturedOriginalBackground)
        {
            return;
        }

        originalRoomBackgroundTexture = null;

        if (diningRoom != null)
        {
            diningRoom.TryGetRoomBackgroundTexture(out originalRoomBackgroundTexture);
        }

        originalCameraBackgroundTexture = cameraManager != null && cameraManager.cameraBackground != null
            ? cameraManager.cameraBackground.texture
            : null;

        capturedOriginalBackground = true;
    }

    private void ApplyDiningRoomBackground(Texture texture)
    {
        if (texture == null)
        {
            return;
        }

        currentDiningBackgroundTexture = texture;

        if (diningRoom != null)
        {
            diningRoom.SetRoomBackgroundTexture(texture);
        }

        if (cameraManager != null)
        {
            cameraManager.SetRoomBackground(texture);
        }
    }

    private void EnsureDiningRoomBackgroundApplied()
    {
        if (currentDiningBackgroundTexture == null || !IsDiningRoomActive())
        {
            return;
        }

        if (cameraManager == null)
        {
            cameraManager = FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);
        }

        if (cameraManager == null || cameraManager.cameraBackground == null)
        {
            return;
        }

        if (cameraManager.cameraBackground.texture != currentDiningBackgroundTexture)
        {
            cameraManager.SetRoomBackground(currentDiningBackgroundTexture);
        }
    }

    private void RestoreDiningRoomBackground()
    {
        if (!capturedOriginalBackground)
        {
            return;
        }

        Texture restoreTexture = originalRoomBackgroundTexture != null
            ? originalRoomBackgroundTexture
            : originalCameraBackgroundTexture;

        if (diningRoom != null)
        {
            diningRoom.SetRoomBackgroundTexture(originalRoomBackgroundTexture);
        }

        if (cameraManager != null && restoreTexture != null && IsDiningRoomActive())
        {
            cameraManager.SetRoomBackground(restoreTexture);
        }

        capturedOriginalBackground = false;
        originalRoomBackgroundTexture = null;
        originalCameraBackgroundTexture = null;
        currentDiningBackgroundTexture = null;
    }

    private IEnumerator PlayLoop()
    {
        int frameIndex = 0;
        int direction = 1;

        while (isShowing && frames != null && frames.Length > 0)
        {
            ApplyDiningRoomBackground(frames[frameIndex]);
            yield return new WaitForSeconds(frameSeconds);

            if (frames.Length == 1)
            {
                continue;
            }

            if (!pingPong)
            {
                frameIndex = (frameIndex + 1) % frames.Length;
                continue;
            }

            frameIndex += direction;

            if (frameIndex >= frames.Length)
            {
                frameIndex = Mathf.Max(0, frames.Length - 2);
                direction = -1;
            }
            else if (frameIndex < 0)
            {
                frameIndex = Mathf.Min(1, frames.Length - 1);
                direction = 1;
            }
        }

        loopRoutine = null;
    }

    private void HideGuestActors(IReadOnlyList<ActorRoomState> guestActors)
    {
        if (guestActors == null)
        {
            return;
        }

        for (int i = 0; i < guestActors.Count; i++)
        {
            ActorRoomState actor = guestActors[i];

            if (actor == null || hiddenGuests.ContainsKey(actor))
            {
                continue;
            }

            hiddenGuests.Add(actor, new GuestVisualState(actor));
            actor.SetInteractable(false);
            actor.SetVisibleByChapterState(false);
            actor.ApplyState();
        }
    }

    private void RestoreGuestActors()
    {
        foreach (KeyValuePair<ActorRoomState, GuestVisualState> pair in hiddenGuests)
        {
            ActorRoomState actor = pair.Key;

            if (actor == null)
            {
                continue;
            }

            pair.Value.Apply(actor);
        }

        hiddenGuests.Clear();
    }

    private void CaptureAndHideExistingDiningTableVisuals()
    {
        if (diningRoom == null)
        {
            return;
        }

        Transform[] roomTransforms = diningRoom.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < roomTransforms.Length; i++)
        {
            Transform roomTransform = roomTransforms[i];

            if (!IsExistingDiningTableVisual(roomTransform))
            {
                continue;
            }

            Renderer[] renderers = roomTransform.GetComponentsInChildren<Renderer>(true);

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];

                if (renderer == null)
                {
                    continue;
                }

                if (!hiddenRenderers.ContainsKey(renderer))
                {
                    hiddenRenderers.Add(renderer, renderer.enabled);
                }

                renderer.enabled = false;
            }

            Graphic[] graphics = roomTransform.GetComponentsInChildren<Graphic>(true);

            for (int graphicIndex = 0; graphicIndex < graphics.Length; graphicIndex++)
            {
                Graphic graphic = graphics[graphicIndex];

                if (graphic == null)
                {
                    continue;
                }

                if (!hiddenGraphics.ContainsKey(graphic))
                {
                    hiddenGraphics.Add(graphic, graphic.enabled);
                }

                graphic.enabled = false;
            }
        }
    }

    private void RestoreExistingDiningTableVisuals()
    {
        foreach (KeyValuePair<Renderer, bool> pair in hiddenRenderers)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        foreach (KeyValuePair<Graphic, bool> pair in hiddenGraphics)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        hiddenRenderers.Clear();
        hiddenGraphics.Clear();
    }

    private bool IsExistingDiningTableVisual(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        string objectName = candidate.name;

        for (int i = 0; i < ExistingDiningTableNameFragments.Length; i++)
        {
            if (!string.IsNullOrEmpty(objectName) &&
                objectName.IndexOf(ExistingDiningTableNameFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDiningRoomActive()
    {
        return diningRoom != null && diningRoom.gameObject.activeInHierarchy;
    }

    private RoomContentGroup FindDiningRoom()
    {
        RoomContentGroup[] rooms = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomContentGroup room = rooms[i];

            if (room != null && SameRoom(room.RoomName, diningRoomId))
            {
                return room;
            }
        }

        return null;
    }

    private static int CompareAssetsByName(UnityEngine.Object left, UnityEngine.Object right)
    {
        string leftName = left != null ? left.name : string.Empty;
        string rightName = right != null ? right.name : string.Empty;
        return string.Compare(leftName, rightName, StringComparison.Ordinal);
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(CleanRoom(left), CleanRoom(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanRoom(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private readonly struct GuestVisualState
    {
        private readonly bool available;
        private readonly bool visible;
        private readonly bool interactable;
        private readonly bool seated;

        public GuestVisualState(ActorRoomState actor)
        {
            available = actor.IsAvailableInCurrentChapter;
            visible = actor.IsVisibleByChapterState;
            interactable = actor.IsInteractable;
            seated = actor.IsSeated;
        }

        public void Apply(ActorRoomState actor)
        {
            actor.SetAvailableInCurrentChapter(available);
            actor.SetVisibleByChapterState(visible);
            actor.SetInteractable(interactable);
            actor.SetSeated(seated);
            actor.ApplyState();
        }
    }
}
