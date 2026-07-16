using System.Collections.Generic;
using UnityEngine;

public readonly struct CharacterRoomScaleComputation
{
    public CharacterRoomScaleComputation(
        string roomId,
        CharacterScaleProfile profile,
        Vector2 roomLocalFootPoint,
        float frontToBack01,
        float catalogLocalScaleY,
        float displaySizeMultiplier,
        float roomStageZoomRatio,
        float inheritedRoomStageZoomRatio,
        float targetLocalScaleY)
    {
        RoomId = roomId;
        Profile = profile;
        RoomLocalFootPoint = roomLocalFootPoint;
        FrontToBack01 = frontToBack01;
        CatalogLocalScaleY = catalogLocalScaleY;
        DisplaySizeMultiplier = displaySizeMultiplier;
        RoomStageZoomRatio = roomStageZoomRatio;
        InheritedRoomStageZoomRatio = inheritedRoomStageZoomRatio;
        TargetLocalScaleY = targetLocalScaleY;
    }

    public string RoomId { get; }
    public CharacterScaleProfile Profile { get; }
    public Vector2 RoomLocalFootPoint { get; }
    public float FrontToBack01 { get; }
    public float CatalogLocalScaleY { get; }
    public float DisplaySizeMultiplier { get; }
    public float RoomStageZoomRatio { get; }
    public float InheritedRoomStageZoomRatio { get; }
    public float TargetLocalScaleY { get; }
}

public readonly struct CharacterRoomScaleApplyResult
{
    public CharacterRoomScaleApplyResult(int evaluated, int changed)
    {
        Evaluated = evaluated;
        Changed = changed;
    }

    public int Evaluated { get; }
    public int Changed { get; }
}

/// <summary>
/// Sole room-dependent size authority for Butler/Guest displayed sprites. It evaluates one room catalog and
/// writes only CharacterRoomScaleTarget.ScaleRoot.localScale. It never changes movement, position,
/// animation, sorting, tint, room state, visibility, collision, or dialogue/gameplay state.
/// </summary>
[DefaultExecutionOrder(32000)]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Character Room Scale Controller")]
public sealed class CharacterRoomScaleController : MonoBehaviour
{
    [SerializeField] private CharacterRoomScaleCatalog catalog;
    [SerializeField] private bool includeInactiveTargets = true;
    [SerializeField] private bool logScaleDiagnostics;

    private readonly List<CharacterRoomScaleTarget> targetBuffer = new List<CharacterRoomScaleTarget>();

    public CharacterRoomScaleCatalog Catalog => catalog;
    public bool LogScaleDiagnostics
    {
        get => logScaleDiagnostics;
        set => logScaleDiagnostics = value;
    }

    private void Awake()
    {
        ResolveCatalog();
        EnsureButlerTarget();
    }

    private void OnEnable()
    {
        ResolveCatalog();

        if (Application.isPlaying)
        {
            EnsureButlerTarget();
        }
    }

    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            RefreshAllWithResultNow();
        }
    }

    public void SetCatalog(CharacterRoomScaleCatalog value)
    {
        catalog = value;
    }

    public static CharacterRoomScaleController EnsureInScene()
    {
        CharacterRoomScaleController existing =
            FindAnyObjectByType<CharacterRoomScaleController>(FindObjectsInactive.Include);

        if (existing != null)
        {
            existing.ResolveCatalog();
            return existing;
        }

        GameObject controllerObject = new GameObject("CharacterRoomScaleController");
        CharacterRoomScaleController created = controllerObject.AddComponent<CharacterRoomScaleController>();
        created.ResolveCatalog();
        return created;
    }

    public static CharacterRoomScaleTarget EnsureTargetForCharacterObject(
        GameObject characterObject,
        string characterId = null,
        string roomId = null,
        CharacterPose pose = CharacterPose.Standing,
        CharacterScaleProfile profile = CharacterScaleProfile.Auto,
        bool roomIdIsCurrent = false)
    {
        if (characterObject == null)
        {
            return null;
        }

        CharacterRoomScaleTarget target = characterObject.GetComponent<CharacterRoomScaleTarget>();

        if (target == null)
        {
            target = characterObject.AddComponent<CharacterRoomScaleTarget>();
        }

        if (!string.IsNullOrWhiteSpace(characterId))
        {
            target.SetCharacterId(characterId);
        }

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            if (roomIdIsCurrent)
            {
                target.SetCurrentRoomId(roomId);
            }
            else
            {
                target.SetRoomIdOverride(roomId);
            }
        }

        target.SetPose(pose);
        target.SetScaleProfile(profile);
        target.ResolveScaleRoot();
        target.CaptureBaseScale(false);
        return target;
    }

    public static bool IsManagedCharacterTarget(CharacterRoomScaleTarget target)
    {
        return target != null &&
            target.gameObject != null &&
            !target.ExcludeFromRoomScaling;
    }

    public CharacterRoomScaleApplyResult RefreshAllWithResultNow()
    {
        ResolveCatalog();

        if (catalog == null || !catalog.EnableCharacterRoomScaling)
        {
            return new CharacterRoomScaleApplyResult(0, 0);
        }

        RefreshTargetBuffer();
        int evaluated = 0;
        int changed = 0;

        for (int i = 0; i < targetBuffer.Count; i++)
        {
            CharacterRoomScaleTarget target = targetBuffer[i];

            if (!IsManagedCharacterTarget(target) ||
                (!includeInactiveTargets && !target.gameObject.activeInHierarchy) ||
                (Application.isPlaying && !target.gameObject.activeInHierarchy))
            {
                continue;
            }

            if (!RefreshTargetNow(target, out bool targetChanged))
            {
                continue;
            }

            evaluated++;

            if (targetChanged)
            {
                changed++;
            }
        }

        return new CharacterRoomScaleApplyResult(evaluated, changed);
    }

    public int RefreshAllNow()
    {
        return RefreshAllWithResultNow().Evaluated;
    }

    public bool RefreshTargetNow(CharacterRoomScaleTarget target)
    {
        return RefreshTargetNow(target, out _);
    }

    public bool RefreshTargetNow(CharacterRoomScaleTarget target, out bool changed)
    {
        return RefreshTargetNow(
            target,
            string.Empty,
            false,
            out changed);
    }

    public bool RefreshTargetNow(
        CharacterRoomScaleTarget target,
        string preferredRoomId,
        bool preferProvidedRoomId)
    {
        return RefreshTargetNow(
            target,
            preferredRoomId,
            preferProvidedRoomId,
            out _);
    }

    public bool RefreshTargetNow(
        CharacterRoomScaleTarget target,
        string preferredRoomId,
        bool preferProvidedRoomId,
        out bool changed)
    {
        changed = false;

        if (!TryComputeTargetScale(
            target,
            preferredRoomId,
            preferProvidedRoomId,
            out CharacterRoomScaleComputation computation))
        {
            return false;
        }

        changed = target.ApplyFinalScale(computation.TargetLocalScaleY);

        if (logScaleDiagnostics)
        {
            Debug.Log(
                $"[CharacterRoomScale] {target.CharacterId} profile={computation.Profile} " +
                $"room={computation.RoomId} y={computation.RoomLocalFootPoint.y:0.###} " +
                $"depth={computation.FrontToBack01:0.###} catalog={computation.CatalogLocalScaleY:0.###} " +
                $"target={computation.TargetLocalScaleY:0.###}",
                target);
        }

        return true;
    }

    public bool TryComputeTargetScale(
        CharacterRoomScaleTarget target,
        out CharacterRoomScaleComputation computation)
    {
        return TryComputeTargetScale(
            target,
            string.Empty,
            false,
            out computation);
    }

    public bool TryComputeTargetScale(
        CharacterRoomScaleTarget target,
        string preferredRoomId,
        bool preferProvidedRoomId,
        out CharacterRoomScaleComputation computation)
    {
        ResolveCatalog();
        computation = default;

        if (catalog == null ||
            !catalog.EnableCharacterRoomScaling ||
            !IsManagedCharacterTarget(target) ||
            !target.TryResolveRoomScaleContext(
                preferredRoomId,
                preferProvidedRoomId,
                out string roomId,
                out Vector2 roomLocalFootPoint))
        {
            return false;
        }

        CharacterScaleProfile profile = target.ResolvedScaleProfile;

        if (!catalog.TryEvaluate(
            roomId,
            profile,
            roomLocalFootPoint.y,
            out CharacterRoomScaleSample sample))
        {
            return false;
        }

        float sizeMultiplier = target.DisplaySizeMultiplier;
        float calibratedScale = sample.FinalLocalScaleY * sizeMultiplier;
        float roomStageZoomRatio = CharacterRoomStageScaleUtility.GetCurrentZoomRatio(catalog, roomId);
        float inheritedRoomStageZoomRatio = CharacterRoomStageScaleUtility.GetInheritedZoomRatio(
            target,
            roomId,
            roomStageZoomRatio);
        float targetLocalScaleY = CharacterRoomStageScaleUtility.CalculateTargetLocalScale(
            calibratedScale,
            roomStageZoomRatio,
            inheritedRoomStageZoomRatio);

        computation = new CharacterRoomScaleComputation(
            roomId,
            profile,
            roomLocalFootPoint,
            sample.FrontToBack01,
            sample.FinalLocalScaleY,
            sizeMultiplier,
            roomStageZoomRatio,
            inheritedRoomStageZoomRatio,
            targetLocalScaleY);
        return true;
    }

    private void ResolveCatalog()
    {
        if (catalog == null)
        {
            catalog = CharacterRoomScaleCatalog.FindInScene();
        }
    }

    private void EnsureButlerTarget()
    {
        GameObject playerObject = GameObject.Find("Player");
        PointClickPlayerMovement movement = playerObject != null
            ? playerObject.GetComponent<PointClickPlayerMovement>()
            : null;

        if (movement == null)
        {
            PointClickPlayerMovement[] candidates =
                FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Exclude);

            for (int i = 0; i < candidates.Length; i++)
            {
                PointClickPlayerMovement candidate = candidates[i];

                if (candidate == null || CharacterRoomScaleTarget.LooksLikeGuest(candidate.gameObject))
                {
                    continue;
                }

                movement = candidate;
                break;
            }
        }

        if (movement == null)
        {
            return;
        }

        EnsureTargetForCharacterObject(
            movement.gameObject,
            movement.gameObject.name,
            null,
            CharacterPose.Standing,
            CharacterScaleProfile.Butler,
            false);
    }

    private void RefreshTargetBuffer()
    {
        targetBuffer.Clear();
        CharacterRoomScaleTarget[] targets = FindObjectsByType<CharacterRoomScaleTarget>(
            includeInactiveTargets ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < targets.Length; i++)
        {
            CharacterRoomScaleTarget target = targets[i];

            if (target != null)
            {
                targetBuffer.Add(target);
            }
        }
    }
}
