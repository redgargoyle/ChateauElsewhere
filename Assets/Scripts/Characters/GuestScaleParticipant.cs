using System;
using UnityEngine;
using UnityEngine.UI;

public enum CharacterPose
{
    Auto,
    Standing,
    Seated,
    Crouching,
    Lying
}

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Guest Scale Participant")]
public sealed class GuestScaleParticipant : MonoBehaviour
{
    [SerializeField] private string characterId;
    [SerializeField] private string roomIdOverride;
    [SerializeField] private CharacterPose pose = CharacterPose.Auto;
    [SerializeField] private Transform scaleRoot;
    [SerializeField] private Transform bodyRoot;
    [SerializeField] private bool excludeFromGuestScaling;
    [SerializeField] private bool isButler;
    [SerializeField, Min(0.001f)] private float manualFineTuneMultiplier = 1f;
    [SerializeField] private float seatedRatioOverride;
    [SerializeField] private Vector3 capturedBaseScale = Vector3.one;
    [SerializeField] private bool hasCapturedBaseScale;

    public string CharacterId => string.IsNullOrWhiteSpace(characterId) ? gameObject.name : characterId.Trim();
    public string RoomIdOverride => roomIdOverride;
    public CharacterPose Pose => pose;
    public Transform ScaleRoot => scaleRoot;
    public Transform BodyRoot => bodyRoot;
    public bool ExcludeFromGuestScaling => excludeFromGuestScaling;
    public bool IsButler => isButler;
    public float ManualFineTuneMultiplier => Mathf.Max(0.001f, manualFineTuneMultiplier);
    public float SeatedRatioOverride => seatedRatioOverride;
    public Vector3 CapturedBaseScale => capturedBaseScale;
    public bool HasCapturedBaseScale => hasCapturedBaseScale;

    private void Reset()
    {
        characterId = gameObject.name;
        isButler = LooksLikeButler(gameObject);
        ResolveScaleRoot();
        CaptureBaseScale(true);
    }

    private void OnValidate()
    {
        manualFineTuneMultiplier = Mathf.Max(0.001f, manualFineTuneMultiplier);
    }

    public void SetCharacterId(string value)
    {
        characterId = string.IsNullOrWhiteSpace(value) ? gameObject.name : value.Trim();
    }

    public void SetRoomIdOverride(string value)
    {
        roomIdOverride = GuestRoomScaleCalibration.CleanRoomId(value);
    }

    public void SetPose(CharacterPose value)
    {
        pose = value;
    }

    public void SetScaleRoot(Transform value)
    {
        scaleRoot = IsUsableBodyTransform(value) ? value : transform;
    }

    public void SetBodyRoot(Transform value)
    {
        bodyRoot = IsUsableBodyTransform(value) ? value : null;
    }

    public void SetExcludedFromGuestScaling(bool value)
    {
        excludeFromGuestScaling = value;
    }

    public void SetIsButler(bool value)
    {
        isButler = value;
    }

    public void SetManualFineTuneMultiplier(float value)
    {
        manualFineTuneMultiplier = Mathf.Max(0.001f, value);
    }

    public void SetSeatedRatioOverride(float value)
    {
        seatedRatioOverride = value;
    }

    public Transform ResolveScaleRoot()
    {
        if (IsUsableBodyTransform(scaleRoot))
        {
            return scaleRoot;
        }

        RoomPersonWalker2D walker = GetComponent<RoomPersonWalker2D>();

        if (walker != null)
        {
            Graphic graphic = walker.TargetGraphic != null ? walker.TargetGraphic : GetComponentInChildren<Graphic>(true);

            if (graphic != null && IsUsableBodyTransform(graphic.rectTransform))
            {
                scaleRoot = graphic.rectTransform;
                return scaleRoot;
            }
        }

        RoomProjectedEntity projectedEntity = GetComponent<RoomProjectedEntity>();

        if (projectedEntity != null &&
            projectedEntity.Mode == RoomProjectedEntity.ProjectionMode.FloorCharacter &&
            IsUsableBodyTransform(projectedEntity.VisualRoot))
        {
            scaleRoot = projectedEntity.VisualRoot;
            return scaleRoot;
        }

        if (IsUsableBodyTransform(bodyRoot))
        {
            scaleRoot = bodyRoot;
            return scaleRoot;
        }

        scaleRoot = transform;
        return scaleRoot;
    }

    public string ResolveRoomId()
    {
        if (!string.IsNullOrWhiteSpace(roomIdOverride))
        {
            return roomIdOverride.Trim();
        }

        RoomPersonWalker2D walker = GetComponent<RoomPersonWalker2D>();

        if (walker != null && walker.RoomProfile != null && !string.IsNullOrWhiteSpace(walker.RoomProfile.RoomId))
        {
            return walker.RoomProfile.RoomId;
        }

        RoomProjectedEntity projectedEntity = GetComponent<RoomProjectedEntity>();

        if (projectedEntity != null)
        {
            if (!string.IsNullOrWhiteSpace(projectedEntity.CurrentVisualScaleRoomId))
            {
                return projectedEntity.CurrentVisualScaleRoomId;
            }

            if (projectedEntity.RoomProfile != null && !string.IsNullOrWhiteSpace(projectedEntity.RoomProfile.RoomId))
            {
                return projectedEntity.RoomProfile.RoomId;
            }
        }

        ActorRoomState actorRoomState = GetComponentInParent<ActorRoomState>(true);

        if (actorRoomState != null && !string.IsNullOrWhiteSpace(actorRoomState.CurrentRoomId))
        {
            return actorRoomState.CurrentRoomId;
        }

        RoomContentGroup roomContent = GetComponentInParent<RoomContentGroup>(true);

        if (roomContent != null && !string.IsNullOrWhiteSpace(roomContent.RoomName))
        {
            return roomContent.RoomName;
        }

        return GuestRoomScaleApplier.TryInferAuthoredSceneGuestRoomId(gameObject, out string inferredRoomId)
            ? inferredRoomId
            : string.Empty;
    }

    public float ResolveRoomLocalY()
    {
        RoomPersonWalker2D walker = GetComponent<RoomPersonWalker2D>();

        if (walker != null)
        {
            return walker.CurrentPosition.y;
        }

        RoomProjectedEntity projectedEntity = GetComponent<RoomProjectedEntity>();

        if (projectedEntity != null)
        {
            return projectedEntity.RoomLocalFootPoint.y;
        }

        Transform root = ResolveScaleRoot();
        RoomContentGroup roomContent = root != null ? root.GetComponentInParent<RoomContentGroup>(true) : null;

        if (roomContent != null)
        {
            Vector3 localPoint = roomContent.transform.InverseTransformPoint(root.position);
            return localPoint.y;
        }

        if (TryFindResolvedRoomContent(out RoomContentGroup resolvedRoomContent))
        {
            Vector3 localPoint = resolvedRoomContent.transform.InverseTransformPoint(root.position);
            return localPoint.y;
        }

        if (root is RectTransform rectTransform)
        {
            return rectTransform.anchoredPosition.y;
        }

        return root != null ? root.localPosition.y : transform.localPosition.y;
    }

    public void CaptureBaseScale(bool force = false)
    {
        if (!force && hasCapturedBaseScale)
        {
            return;
        }

        Transform root = ResolveScaleRoot();
        capturedBaseScale = root != null ? SanitizeScale(root.localScale) : Vector3.one;
        hasCapturedBaseScale = true;
    }

    public void RestoreCapturedBaseScale()
    {
        Transform root = ResolveScaleRoot();

        if (root != null && hasCapturedBaseScale)
        {
            root.localScale = capturedBaseScale;
        }
    }

    public bool ApplyFinalScale(float targetLocalScaleY)
    {
        if (excludeFromGuestScaling || isButler)
        {
            return false;
        }

        CaptureBaseScale(false);
        Transform root = ResolveScaleRoot();

        if (root == null)
        {
            return false;
        }

        float safeTargetY = Mathf.Max(0.001f, targetLocalScaleY);
        float baseY = Mathf.Abs(capturedBaseScale.y) > 0.001f ? capturedBaseScale.y : 1f;
        float signedTargetY = baseY < 0f ? -safeTargetY : safeTargetY;
        float aspectRatio = signedTargetY / baseY;
        Vector3 targetScale = new Vector3(
            capturedBaseScale.x * aspectRatio,
            signedTargetY,
            capturedBaseScale.z);
        bool changed = (root.localScale - targetScale).sqrMagnitude > 0.000001f;
        root.localScale = targetScale;
        return changed;
    }

    public static bool NameLooksExcludedFromBodyScale(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] tokens =
        {
            "coat",
            "coatcutout",
            "jacket",
            "cloak",
            "shawl",
            "speech",
            "thought",
            "bubble",
            "prompt",
            "highlight",
            "icon",
            "shadow",
            "cursor",
            "tooltip"
        };

        for (int i = 0; i < tokens.Length; i++)
        {
            if (value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static Vector3 SanitizeScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }

    private static bool IsUsableBodyTransform(Transform candidate)
    {
        return candidate != null && !NameLooksExcludedFromBodyScale(candidate.name);
    }

    private bool TryFindResolvedRoomContent(out RoomContentGroup roomContent)
    {
        roomContent = null;
        string roomId = ResolveRoomId();

        if (string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        RoomContentGroup[] rooms = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomContentGroup candidate = rooms[i];

            if (candidate != null && GuestRoomScaleCalibration.SameRoom(candidate.RoomName, roomId))
            {
                roomContent = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeButler(GameObject candidate)
    {
        return candidate != null &&
            (candidate.GetComponent<PointClickPlayerMovement>() != null &&
                !candidate.name.Contains("Guest", StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Contains("Butler", StringComparison.OrdinalIgnoreCase));
    }
}
