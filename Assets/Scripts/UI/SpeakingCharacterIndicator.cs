using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SpeakingCharacterIndicator : MonoBehaviour
{
    private const string ServiceObjectName = "SpeakingCharacterIndicator";
    private const string SpriteObjectName = "Sprite_ChatBubble";
    private const string DefaultSpriteResourcePath = "UI/chat_bubble";

    private static readonly string[] GuestDisplayNames =
    {
        "Miss Isolde Wren",
        "Professor Lucien Vale",
        "Mister Florian Knell",
        "Countess Elowen Dusk",
        "Baron Hector Glass",
        "Lady Sabine Marrow",
        "Lord Ambrose Veil",
        "Madame Coralie Thread"
    };

    [SerializeField] private Sprite bubbleSprite;
    [SerializeField] private string bubbleSpriteResourcePath = DefaultSpriteResourcePath;
    [SerializeField, Min(0.01f)] private float bubbleHeightToActorHeight = 0.22f;
    [SerializeField, Min(0.01f)] private float minimumBubbleWorldHeight = 0.32f;
    [SerializeField, Min(0.01f)] private float maximumBubbleWorldHeight = 0.6f;
    [SerializeField, Min(0f)] private float verticalWorldPadding = 0.035f;
    [SerializeField] private int sortingOrderOffset = 50;

    private SpriteRenderer bubbleRenderer;
    private Transform currentTarget;
    private ActorRoomState currentActor;
    private int currentSpeechToken;
    private bool isShowing;
    private bool loggedMissingSprite;

    public static SpeakingCharacterIndicator FindOrCreate()
    {
        SpeakingCharacterIndicator existing = FindAnyObjectByType<SpeakingCharacterIndicator>(FindObjectsInactive.Include);

        if (existing != null)
        {
            existing.ResolveReferences();
            return existing;
        }

        GameObject serviceObject = new GameObject(ServiceObjectName, typeof(SpeakingCharacterIndicator));
        SpeakingCharacterIndicator service = serviceObject.GetComponent<SpeakingCharacterIndicator>();
        service.ResolveReferences();
        return service;
    }

    public static void HideAnyCurrent()
    {
        SpeakingCharacterIndicator existing = FindAnyObjectByType<SpeakingCharacterIndicator>(FindObjectsInactive.Include);
        existing?.Hide();
    }

    public void ShowForSpeechLine(int speechToken, string lineId, string speaker, string text)
    {
        ResolveReferences();

        if (bubbleRenderer == null)
        {
            return;
        }

        if (bubbleSprite == null)
        {
            LogMissingSpriteOnce();
            Hide();
            return;
        }

        if (!TryResolveSpeakerTarget(lineId, speaker, out Transform target, out ActorRoomState actor))
        {
            Hide();
            return;
        }

        currentSpeechToken = speechToken;
        currentTarget = target;
        currentActor = actor;
        isShowing = true;
        bubbleRenderer.sprite = bubbleSprite;
        bubbleRenderer.enabled = true;
        bubbleRenderer.gameObject.SetActive(true);

        if (!UpdateBubbleTransform())
        {
            Hide();
        }
    }

    public void HideForSpeechToken(int speechToken)
    {
        if (!isShowing || speechToken == currentSpeechToken)
        {
            Hide();
        }
    }

    public void Hide()
    {
        isShowing = false;
        currentSpeechToken = 0;
        currentTarget = null;
        currentActor = null;

        if (bubbleRenderer != null)
        {
            bubbleRenderer.enabled = false;
            bubbleRenderer.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (isShowing && !UpdateBubbleTransform())
        {
            Hide();
        }
    }

    private void ResolveReferences()
    {
        if (bubbleSprite == null && !string.IsNullOrWhiteSpace(bubbleSpriteResourcePath))
        {
            bubbleSprite = Resources.Load<Sprite>(bubbleSpriteResourcePath.Trim());
        }

        if (bubbleRenderer != null)
        {
            return;
        }

        Transform existingSprite = transform.Find(SpriteObjectName);
        GameObject spriteObject = existingSprite != null ? existingSprite.gameObject : new GameObject(SpriteObjectName);
        spriteObject.transform.SetParent(transform, false);

        bubbleRenderer = spriteObject.GetComponent<SpriteRenderer>();

        if (bubbleRenderer == null)
        {
            bubbleRenderer = spriteObject.AddComponent<SpriteRenderer>();
        }

        bubbleRenderer.enabled = false;
        bubbleRenderer.gameObject.SetActive(false);
    }

    private bool UpdateBubbleTransform()
    {
        if (bubbleRenderer == null ||
            bubbleSprite == null ||
            currentTarget == null ||
            !currentTarget.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (currentActor != null && !currentActor.IsVisibleInCurrentRoom)
        {
            return false;
        }

        if (!TryGetTargetBounds(currentTarget, out Bounds bounds, out int sortingLayerId, out int sortingOrder))
        {
            return false;
        }

        float scale = GetBubbleScale(bounds);
        float bubbleWidth = bubbleSprite.bounds.size.x * scale;
        float bubbleHeight = bubbleSprite.bounds.size.y * scale;
        float horizontalDirection = GetHorizontalDirection(bounds);
        float horizontalDistance = Mathf.Max(bounds.extents.x * 0.55f, 0.08f) + bubbleWidth * 0.12f;
        float verticalDistance = bubbleHeight * 0.5f + verticalWorldPadding;

        bubbleRenderer.transform.position = new Vector3(
            bounds.center.x + horizontalDirection * horizontalDistance,
            bounds.max.y + verticalDistance,
            bounds.center.z);
        bubbleRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        bubbleRenderer.sortingLayerID = sortingLayerId;
        bubbleRenderer.sortingOrder = sortingOrder + sortingOrderOffset;
        return true;
    }

    private bool TryResolveSpeakerTarget(string lineId, string speaker, out Transform target, out ActorRoomState actor)
    {
        target = null;
        actor = null;

        if (IsButlerSpeaker(lineId, speaker))
        {
            return TryResolveButlerTarget(out target, out actor);
        }

        if (TryResolveGuestNumber(lineId, speaker, out int guestNumber) &&
            TryResolveGuestTarget(guestNumber, speaker, out target, out actor))
        {
            return true;
        }

        return TryResolveNamedActor(speaker, out target, out actor);
    }

    private static bool TryResolveButlerTarget(out Transform target, out ActorRoomState actor)
    {
        target = null;
        actor = null;

        ChapterManager chapterManager = FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include);
        GameObject butlerObject = chapterManager != null ? chapterManager.PlayerButlerReference : null;

        if (butlerObject == null)
        {
            PointClickPlayerMovement playerMovement = FindAnyObjectByType<PointClickPlayerMovement>(FindObjectsInactive.Include);
            butlerObject = playerMovement != null ? playerMovement.gameObject : null;
        }

        if (butlerObject == null)
        {
            butlerObject = GameObject.Find("Player");
        }

        if (butlerObject == null)
        {
            return false;
        }

        target = butlerObject.transform;
        actor = butlerObject.GetComponent<ActorRoomState>();
        return target != null;
    }

    private static bool TryResolveGuestTarget(int guestNumber, string speaker, out Transform target, out ActorRoomState actor)
    {
        target = null;
        actor = null;
        ActorRoomState bestActor = null;
        int bestScore = 0;
        ActorRoomState[] actorStates = FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);

        for (int i = 0; i < actorStates.Length; i++)
        {
            ActorRoomState candidate = actorStates[i];

            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            int score = GetGuestTargetScore(candidate, guestNumber, speaker);

            if (score > bestScore)
            {
                bestScore = score;
                bestActor = candidate;
            }
        }

        if (bestActor == null)
        {
            return false;
        }

        actor = bestActor;
        target = bestActor.transform;
        return target != null;
    }

    private static bool TryResolveNamedActor(string speaker, out Transform target, out ActorRoomState actor)
    {
        target = null;
        actor = null;

        if (string.IsNullOrWhiteSpace(speaker))
        {
            return false;
        }

        string normalizedSpeaker = NormalizeLabel(speaker);

        if (string.IsNullOrEmpty(normalizedSpeaker))
        {
            return false;
        }

        ActorRoomState[] actorStates = FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);

        for (int i = 0; i < actorStates.Length; i++)
        {
            ActorRoomState candidate = actorStates[i];

            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            if (!LabelsMatch(candidate.ActorId, normalizedSpeaker) &&
                !LabelsMatch(candidate.gameObject.name, normalizedSpeaker))
            {
                continue;
            }

            actor = candidate;
            target = candidate.transform;
            return target != null;
        }

        return false;
    }

    private static int GetGuestTargetScore(ActorRoomState candidate, int guestNumber, string speaker)
    {
        int score = 0;

        if (TryExtractGuestNumberFromActorLabel(candidate.ActorId, out int actorIdNumber) && actorIdNumber == guestNumber)
        {
            score += 100;
        }

        if (TryExtractGuestNumberFromActorLabel(candidate.gameObject.name, out int objectNameNumber) && objectNameNumber == guestNumber)
        {
            score += 90;
        }

        if (guestNumber >= 1 && guestNumber <= GuestDisplayNames.Length)
        {
            string guestDisplayName = GuestDisplayNames[guestNumber - 1];

            if (LabelsMatch(candidate.ActorId, NormalizeLabel(guestDisplayName)) ||
                LabelsMatch(candidate.gameObject.name, NormalizeLabel(guestDisplayName)))
            {
                score += 80;
            }
        }

        if (!string.IsNullOrWhiteSpace(speaker))
        {
            string normalizedSpeaker = NormalizeLabel(speaker);

            if (LabelsMatch(candidate.ActorId, normalizedSpeaker) ||
                LabelsMatch(candidate.gameObject.name, normalizedSpeaker))
            {
                score += 60;
            }
        }

        if (candidate.IsVisibleInCurrentRoom)
        {
            score += 20;
        }

        if (candidate.gameObject.activeInHierarchy)
        {
            score += 5;
        }

        return score;
    }

    private bool TryGetTargetBounds(Transform target, out Bounds bounds, out int sortingLayerId, out int sortingOrder)
    {
        bounds = new Bounds(target.position, Vector3.zero);
        sortingLayerId = 0;
        sortingOrder = 0;
        bool foundBounds = false;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null ||
                renderer == bubbleRenderer ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                ShouldIgnoreBoundsTransform(renderer.transform))
            {
                continue;
            }

            if (!foundBounds)
            {
                bounds = renderer.bounds;
                foundBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }

            if (renderer.sortingOrder >= sortingOrder)
            {
                sortingLayerId = renderer.sortingLayerID;
                sortingOrder = renderer.sortingOrder;
            }
        }

        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null ||
                !graphic.enabled ||
                !graphic.gameObject.activeInHierarchy ||
                ShouldIgnoreBoundsTransform(graphic.transform))
            {
                continue;
            }

            RectTransform rectTransform = graphic.rectTransform;

            if (rectTransform == null)
            {
                continue;
            }

            Bounds graphicBounds = GetRectTransformWorldBounds(rectTransform);

            if (!foundBounds)
            {
                bounds = graphicBounds;
                foundBounds = true;
            }
            else
            {
                bounds.Encapsulate(graphicBounds);
            }

            Canvas canvas = graphic.canvas;

            if (canvas != null && canvas.sortingOrder >= sortingOrder)
            {
                sortingLayerId = canvas.sortingLayerID;
                sortingOrder = canvas.sortingOrder;
            }
        }

        if (foundBounds)
        {
            return true;
        }

        bounds = new Bounds(target.position + Vector3.up * 0.8f, new Vector3(0.6f, 1.6f, 0.1f));
        return true;
    }

    private float GetBubbleScale(Bounds actorBounds)
    {
        float spriteHeight = bubbleSprite != null ? bubbleSprite.bounds.size.y : 0f;

        if (spriteHeight <= 0f)
        {
            return 1f;
        }

        float desiredHeight = Mathf.Clamp(
            actorBounds.size.y * bubbleHeightToActorHeight,
            minimumBubbleWorldHeight,
            maximumBubbleWorldHeight);
        return desiredHeight / spriteHeight;
    }

    private static float GetHorizontalDirection(Bounds actorBounds)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return 1f;
        }

        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(actorBounds.center);
        return viewportPoint.x > 0.68f ? -1f : 1f;
    }

    private static Bounds GetRectTransformWorldBounds(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Bounds bounds = new Bounds(corners[0], Vector3.zero);

        for (int i = 1; i < corners.Length; i++)
        {
            bounds.Encapsulate(corners[i]);
        }

        return bounds;
    }

    private static bool ShouldIgnoreBoundsTransform(Transform candidate)
    {
        for (Transform current = candidate; current != null; current = current.parent)
        {
            string name = current.name;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (name.IndexOf("coat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("chat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("speech", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void LogMissingSpriteOnce()
    {
        if (loggedMissingSprite)
        {
            return;
        }

        loggedMissingSprite = true;
        Debug.LogWarning(
            $"[SpeakingIndicator] Missing chat bubble sprite at Resources path '{bubbleSpriteResourcePath}'. " +
            "Place the cutout PNG at Assets/Resources/UI/chat_bubble.png.",
            this);
    }

    private static bool IsButlerSpeaker(string lineId, string speaker)
    {
        if (!string.IsNullOrWhiteSpace(speaker) &&
            NormalizeLabel(speaker).IndexOf("butler", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(lineId) &&
            lineId.Trim().IndexOf("BUTLER", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryResolveGuestNumber(string lineId, string speaker, out int guestNumber)
    {
        if (TryExtractGuestNumberFromLineId(lineId, out guestNumber) ||
            TryExtractGuestNumberFromActorLabel(speaker, out guestNumber))
        {
            return true;
        }

        string normalizedSpeaker = NormalizeLabel(speaker);

        for (int i = 0; i < GuestDisplayNames.Length; i++)
        {
            if (string.Equals(normalizedSpeaker, NormalizeLabel(GuestDisplayNames[i]), StringComparison.Ordinal))
            {
                guestNumber = i + 1;
                return true;
            }
        }

        guestNumber = 0;
        return false;
    }

    private static bool TryExtractGuestNumberFromLineId(string lineId, out int guestNumber)
    {
        return TryExtractNumberAfterToken(lineId, "CH1_G", out guestNumber) ||
            TryExtractNumberAfterToken(lineId, "CH2_G", out guestNumber) ||
            TryExtractNumberAfterToken(lineId, "SUB_CH01_G", out guestNumber) ||
            TryExtractNumberAfterToken(lineId, "SUB_CH02_G", out guestNumber);
    }

    private static bool TryExtractGuestNumberFromActorLabel(string value, out int guestNumber)
    {
        return TryExtractNumberAfterToken(value, "Guest", out guestNumber);
    }

    private static bool TryExtractNumberAfterToken(string value, string token, out int number)
    {
        number = 0;

        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        int tokenIndex = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);

        if (tokenIndex < 0)
        {
            return false;
        }

        int index = tokenIndex + token.Length;

        while (index < value.Length && !char.IsDigit(value[index]))
        {
            index++;
        }

        int numberStart = index;

        while (index < value.Length && char.IsDigit(value[index]))
        {
            index++;
        }

        return index > numberStart &&
            int.TryParse(value.Substring(numberStart, index - numberStart), out number) &&
            number > 0;
    }

    private static bool LabelsMatch(string candidate, string normalizedTarget)
    {
        string normalizedCandidate = NormalizeLabel(candidate);
        return !string.IsNullOrEmpty(normalizedCandidate) &&
            !string.IsNullOrEmpty(normalizedTarget) &&
            (string.Equals(normalizedCandidate, normalizedTarget, StringComparison.Ordinal) ||
             normalizedCandidate.IndexOf(normalizedTarget, StringComparison.Ordinal) >= 0 ||
             normalizedTarget.IndexOf(normalizedCandidate, StringComparison.Ordinal) >= 0);
    }

    private static string NormalizeLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}
