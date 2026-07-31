using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class GreenChairRenderStateDiagnostic
{
    public const string ReportPath = "/tmp/chantilly-green-chair-render-state.txt";
    public const string OwnershipAuditReportPath = "/tmp/chantilly-sorting-ownership-audit.txt";

    private const string GuestActorId = "guest_4";
    private const string ChairName = "drawingroomgreenchair_0";
    private const string RailName = "drawingroomgreenchair[_0";
    private const string SittingSpriteGuid = "3c77696129c44fc49b56737e0ffdc4e9";

    private static readonly string[] SortingWriterTypeNames =
    {
        nameof(WorldYSortSpriteRenderer),
        nameof(DiningRoomSeatedGuestOcclusionException),
        nameof(ObjectMovementBlocker2D),
        nameof(RoomContentGroup),
        nameof(PointClickPlayerMovement),
        nameof(CharacterAnimationDisplay),
        nameof(CharacterAnimationPresenter),
        nameof(Animator)
    };

    private static bool armed;
    private static bool capturedManagedState;

    [MenuItem("Tools/Chantilly/Diagnostics/Capture Green Chair Render State")]
    public static void ArmCapture()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode, complete Guest 4's Drawing Room arrival, display the target Game-view frame, " +
                "then run Tools/Chantilly/Diagnostics/Capture Green Chair Render State.");
            return;
        }

        ArmForNextFrame();
        Debug.Log($"Green-chair render capture armed for the next Gameplay-camera frame: {ReportPath}");
    }

    [MenuItem("Tools/Chantilly/Diagnostics/Audit Selected Sorting Ownership")]
    public static void AuditSelectedSortingOwnership()
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            Debug.LogWarning(
                "Select an actor or environment prop, then run the sorting-ownership audit again.");
            return;
        }

        string report = BuildSelectedSortingOwnershipAuditForTests(selectedObject);
        File.WriteAllText(OwnershipAuditReportPath, report);

        if (report.Contains("CONFLICT renderer="))
        {
            Debug.LogError(
                $"Sorting-ownership conflicts found under {selectedObject.name}. " +
                $"Inspector values can be overwritten by the listed owners. Report: {OwnershipAuditReportPath}",
                selectedObject);
        }
        else
        {
            Debug.Log(
                $"Sorting ownership audited under {selectedObject.name}. Report: {OwnershipAuditReportPath}",
                selectedObject);
        }
    }

    public static string BuildSelectedSortingOwnershipAuditForTests(GameObject selectedObject)
    {
        StringBuilder report = new StringBuilder(8192);
        report.AppendLine("CHANTILLY SELECTED SORTING OWNERSHIP AUDIT");
        report.AppendLine($"capturedUtc={DateTime.UtcNow:O}");

        if (selectedObject == null)
        {
            report.AppendLine("ERROR: no GameObject selected.");
            return report.ToString();
        }

        ActorRoomState selectedActor = selectedObject.GetComponentInParent<ActorRoomState>(true);
        GameObject auditRoot = selectedActor != null
            ? selectedActor.gameObject
            : selectedObject;
        report.AppendLine($"selected={GetPath(selectedObject.transform)}");
        report.AppendLine($"auditRoot={GetPath(auditRoot.transform)}");
        report.AppendLine(
            "Inspector sorting values are overwritten by active owners listed for each renderer; " +
            "edit the owning component instead of repeatedly editing SpriteRenderer values.");
        report.AppendLine();

        SpriteRenderer[] renderers = auditRoot.GetComponentsInChildren<SpriteRenderer>(true);
        ObjectMovementBlocker2D[] blockers =
            UnityEngine.Object.FindObjectsByType<ObjectMovementBlocker2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        int conflictCount = 0;

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            SpriteRenderer renderer = renderers[rendererIndex];

            if (renderer == null)
            {
                continue;
            }

            List<string> normalOwners = new List<string>();
            List<string> narrowOwners = new List<string>();
            List<string> relatedWriters = new List<string>();
            CollectNormalSortingOwners(renderer, auditRoot, blockers, normalOwners);
            CollectNarrowSortingOwners(renderer, narrowOwners);
            CollectRelatedVisualWriters(renderer, auditRoot, relatedWriters);

            report.AppendLine(
                $"RENDERER path={GetPath(renderer.transform)} enabled={renderer.enabled} " +
                $"active={renderer.gameObject.activeInHierarchy} layer={renderer.sortingLayerName} " +
                $"order={renderer.sortingOrder}");
            AppendOwnerList(report, "  normalSortingOwners", normalOwners);
            AppendOwnerList(report, "  narrowExceptionOwners", narrowOwners);
            AppendOwnerList(report, "  relatedVisualWriters", relatedWriters);
            AppendRendererSortingGroups(report, renderer, "  ");

            bool activeRenderer = renderer.enabled && renderer.gameObject.activeInHierarchy;
            bool allowedNarrowException =
                activeRenderer &&
                narrowOwners.Count == 1 &&
                normalOwners.Count == 1 &&
                normalOwners[0].Contains(nameof(WorldYSortSpriteRenderer));
            bool conflict =
                activeRenderer &&
                (normalOwners.Count > 1 ||
                narrowOwners.Count > 1 ||
                (narrowOwners.Count == 1 &&
                normalOwners.Count == 1 &&
                !allowedNarrowException));

            if (conflict)
            {
                conflictCount++;
                report.AppendLine(
                    $"  CONFLICT renderer={GetPath(renderer.transform)} " +
                    $"normalOwners={normalOwners.Count} narrowOwners={narrowOwners.Count}");
            }
            else if (allowedNarrowException)
            {
                report.AppendLine(
                    $"  ALLOWED_NARROW_EXCEPTION renderer={GetPath(renderer.transform)} " +
                    $"{nameof(WorldYSortSpriteRenderer)} + " +
                    $"{nameof(DiningRoomSeatedGuestOcclusionException)}");
            }
            else
            {
                report.AppendLine($"  ownershipStatus=OK");
            }

            report.AppendLine();
        }

        report.AppendLine($"SUMMARY renderers={renderers.Length} conflicts={conflictCount}");
        return report.ToString();
    }

    public static void ArmForNextFrame()
    {
        Disarm();
        armed = true;
        capturedManagedState = false;
        Application.onBeforeRender += CaptureManagedState;
        RenderPipelineManager.beginCameraRendering += CaptureAtBeginCameraRendering;
    }

    public static void CaptureNowForTests(string phase, Camera camera, bool append)
    {
        WriteReport(phase, camera, append);
    }

    public static void CaptureGameplayCameraPngForTests(Camera camera, string outputPath)
    {
        if (camera == null || string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        RenderTexture target = new RenderTexture(1672, 941, 24, RenderTextureFormat.ARGB32);
        Texture2D pixels = new Texture2D(1672, 941, TextureFormat.RGB24, false);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(pixels);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void CaptureManagedState()
    {
        if (!armed || capturedManagedState)
        {
            return;
        }

        Camera camera = ResolveGameplayCamera();
        WriteReport("Application.onBeforeRender (after managed LateUpdate writers)", camera, false);
        capturedManagedState = true;
    }

    private static void CaptureAtBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!armed || camera == null || !IsGameplayCamera(camera))
        {
            return;
        }

        WriteReport(
            "RenderPipelineManager.beginCameraRendering (final managed state submitted to Gameplay camera)",
            camera,
            capturedManagedState);
        Disarm();
        Debug.Log($"Green-chair render state captured: {ReportPath}");
    }

    private static void Disarm()
    {
        armed = false;
        Application.onBeforeRender -= CaptureManagedState;
        RenderPipelineManager.beginCameraRendering -= CaptureAtBeginCameraRendering;
    }

    private static void CollectNormalSortingOwners(
        SpriteRenderer renderer,
        GameObject auditRoot,
        ObjectMovementBlocker2D[] blockers,
        List<string> destination)
    {
        WorldYSortSpriteRenderer[] worldSorters =
            auditRoot.GetComponentsInChildren<WorldYSortSpriteRenderer>(true);

        for (int i = 0; i < worldSorters.Length; i++)
        {
            WorldYSortSpriteRenderer sorter = worldSorters[i];

            if (IsActiveWriter(sorter) && WorldSorterOwnsRenderer(sorter, renderer))
            {
                AddUnique(destination, DescribeOwner(sorter));
            }
        }

        PointClickPlayerMovement[] pointClickOwners =
            auditRoot.GetComponentsInChildren<PointClickPlayerMovement>(true);

        for (int i = 0; i < pointClickOwners.Length; i++)
        {
            PointClickPlayerMovement owner = pointClickOwners[i];

            if (IsActiveWriter(owner) &&
                owner.AppliesPlayerSorting &&
                IsAtOrBelow(renderer.transform, owner.transform))
            {
                AddUnique(destination, DescribeOwner(owner));
            }
        }

        for (int i = 0; i < blockers.Length; i++)
        {
            ObjectMovementBlocker2D blocker = blockers[i];

            if (!IsActiveWriter(blocker) ||
                !blocker.SortSourceRenderers ||
                blocker.BlockingCollider == null ||
                !blocker.BlockingCollider.enabled ||
                !BlockerOwnsRenderer(blocker, renderer))
            {
                continue;
            }

            AddUnique(destination, DescribeOwner(blocker));
        }
    }

    private static void CollectNarrowSortingOwners(
        SpriteRenderer renderer,
        List<string> destination)
    {
        DiningRoomSeatedGuestOcclusionException[] exceptions =
            UnityEngine.Object.FindObjectsByType<DiningRoomSeatedGuestOcclusionException>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < exceptions.Length; i++)
        {
            DiningRoomSeatedGuestOcclusionException exception = exceptions[i];

            if (IsActiveWriter(exception) &&
                exception.IsExceptionActive &&
                (IsAtOrBelow(renderer.transform, exception.transform) ||
                renderer == exception.FrontOccluderRenderer))
            {
                AddUnique(destination, DescribeOwner(exception));
            }
        }
    }

    private static void CollectRelatedVisualWriters(
        SpriteRenderer renderer,
        GameObject auditRoot,
        List<string> destination)
    {
        MonoBehaviour[] behaviours = auditRoot.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (!IsActiveWriter(behaviour) ||
                !IsAtOrBelow(renderer.transform, behaviour.transform))
            {
                continue;
            }

            if (behaviour is CharacterAnimationDisplay ||
                behaviour is CharacterAnimationPresenter)
            {
                AddUnique(destination, DescribeOwner(behaviour));
            }
        }

        for (Transform cursor = renderer.transform;
            cursor != null && IsAtOrBelow(cursor, auditRoot.transform);
            cursor = cursor.parent)
        {
            Animator[] animators = cursor.GetComponents<Animator>();

            for (int i = 0; i < animators.Length; i++)
            {
                if (IsActiveWriter(animators[i]))
                {
                    AddUnique(destination, DescribeOwner(animators[i]));
                }
            }
        }
    }

    private static void AppendOwnerList(
        StringBuilder report,
        string label,
        List<string> owners)
    {
        report.AppendLine($"{label}={owners.Count}");

        for (int i = 0; i < owners.Count; i++)
        {
            report.AppendLine($"    {owners[i]}");
        }
    }

    private static void AppendRendererSortingGroups(
        StringBuilder report,
        SpriteRenderer renderer,
        string indent)
    {
        report.AppendLine($"{indent}enabledSortingGroups:");
        bool foundGroup = false;

        for (Transform cursor = renderer.transform; cursor != null; cursor = cursor.parent)
        {
            SortingGroup[] groups = cursor.GetComponents<SortingGroup>();

            for (int i = 0; i < groups.Length; i++)
            {
                SortingGroup group = groups[i];

                if (group == null || !group.enabled)
                {
                    continue;
                }

                foundGroup = true;
                report.AppendLine(
                    $"{indent}  {GetPath(group.transform)} layer={group.sortingLayerName} " +
                    $"order={group.sortingOrder} sortAtRoot={group.sortAtRoot}");
            }
        }

        if (!foundGroup)
        {
            report.AppendLine($"{indent}  <none>");
        }
    }

    private static bool BlockerOwnsRenderer(
        ObjectMovementBlocker2D blocker,
        SpriteRenderer renderer)
    {
        GameObject sourceObject = null;

        if (blocker.SourceObject is GameObject gameObjectSource)
        {
            sourceObject = gameObjectSource;
        }
        else if (blocker.SourceObject is Component componentSource)
        {
            sourceObject = componentSource.gameObject;
        }

        return sourceObject != null &&
            IsAtOrBelow(renderer.transform, sourceObject.transform);
    }

    private static bool WorldSorterOwnsRenderer(
        WorldYSortSpriteRenderer sorter,
        SpriteRenderer renderer)
    {
        if (sorter == null || renderer == null)
        {
            return false;
        }

        if (renderer.transform == sorter.transform)
        {
            return true;
        }

        SerializedObject serializedSorter = new SerializedObject(sorter);
        SerializedProperty includeChildren = serializedSorter.FindProperty("includeChildren");
        return includeChildren != null &&
            includeChildren.boolValue &&
            renderer.transform.IsChildOf(sorter.transform);
    }

    private static bool IsActiveWriter(Behaviour behaviour)
    {
        return behaviour != null &&
            behaviour.enabled &&
            behaviour.gameObject.activeInHierarchy;
    }

    private static bool IsAtOrBelow(Transform candidate, Transform root)
    {
        return candidate != null &&
            root != null &&
            (candidate == root || candidate.IsChildOf(root));
    }

    private static string DescribeOwner(Component owner)
    {
        return $"{owner.GetType().Name} path={GetPath(owner.transform)} instanceId={owner.GetInstanceID()}";
    }

    private static void AddUnique(List<string> destination, string value)
    {
        if (!destination.Contains(value))
        {
            destination.Add(value);
        }
    }

    private static void WriteReport(string phase, Camera camera, bool append)
    {
        StringBuilder report = new StringBuilder(16384);
        Scene scene = SceneManager.GetActiveScene();
        ActorRoomState guest = FindTargetGuest();
        Transform chair = FindTransformInLoadedScenes(ChairName);
        Transform rail = FindTransformInLoadedScenes(RailName);

        report.AppendLine("CHANTILLY GREEN-CHAIR FINAL RENDER STATE");
        report.AppendLine($"capturedUtc={DateTime.UtcNow:O}");
        report.AppendLine($"phase={phase}");
        report.AppendLine($"frame={Time.frameCount}");
        report.AppendLine($"isPlaying={EditorApplication.isPlaying}");
        report.AppendLine($"activeScene={scene.path}");
        report.AppendLine();

        AppendCamera(report, camera);
        AppendActor(report, guest);
        AppendObject(report, "FULL GREEN CHAIR", chair != null ? chair.gameObject : null);
        AppendObject(report, "DETACHED FOREGROUND RAIL", rail != null ? rail.gameObject : null);
        AppendAnimationBindings(report, guest);
        AppendExecutionOrders(report, guest, chair, rail);

        if (guest == null)
        {
            report.AppendLine($"ERROR: runtime ActorRoomState actorId={GuestActorId} was not found.");
        }
        else
        {
            SpriteRenderer sittingRenderer = FindRendererWithSpriteGuid(guest.gameObject, SittingSpriteGuid);
            report.AppendLine(
                sittingRenderer != null
                    ? $"EXPECTED SITTING SPRITE CONFIRMED: {GetPath(sittingRenderer.transform)}"
                    : $"ERROR: active Guest 4 renderer with sprite GUID {SittingSpriteGuid} was not found.");
        }

        File.WriteAllText(
            ReportPath,
            append && File.Exists(ReportPath)
                ? File.ReadAllText(ReportPath) + Environment.NewLine + report
                : report.ToString());
    }

    private static void AppendCamera(StringBuilder report, Camera camera)
    {
        report.AppendLine("CAMERA");

        if (camera == null)
        {
            report.AppendLine("  <none>");
            report.AppendLine();
            return;
        }

        report.AppendLine($"  path={GetPath(camera.transform)}");
        report.AppendLine($"  instanceId={camera.GetInstanceID()}");
        report.AppendLine($"  enabled={camera.enabled} active={camera.gameObject.activeInHierarchy}");
        report.AppendLine($"  cameraType={camera.cameraType} orthographic={camera.orthographic}");
        report.AppendLine($"  depth={camera.depth} worldZ={camera.transform.position.z:R}");
        report.AppendLine($"  transparencySortMode={camera.transparencySortMode}");
        report.AppendLine($"  transparencySortAxis={camera.transparencySortAxis}");
        report.AppendLine();
    }

    private static void AppendActor(StringBuilder report, ActorRoomState guest)
    {
        report.AppendLine("TARGET YELLOW-DRESS ACTOR");

        if (guest == null)
        {
            report.AppendLine("  <not found>");
            report.AppendLine();
            return;
        }

        report.AppendLine($"  actorId={guest.ActorId}");
        report.AppendLine($"  room={guest.CurrentRoomId}");
        report.AppendLine($"  seated={guest.IsSeated}");
        report.AppendLine($"  visibleInCurrentRoom={guest.IsVisibleInCurrentRoom}");
        AppendObjectDetails(report, guest.gameObject, "  ");
        report.AppendLine();
    }

    private static void AppendObject(StringBuilder report, string heading, GameObject target)
    {
        report.AppendLine(heading);

        if (target == null)
        {
            report.AppendLine("  <not found>");
            report.AppendLine();
            return;
        }

        AppendObjectDetails(report, target, "  ");
        report.AppendLine();
    }

    private static void AppendObjectDetails(StringBuilder report, GameObject target, string indent)
    {
        report.AppendLine($"{indent}path={GetPath(target.transform)}");
        report.AppendLine($"{indent}instanceId={target.GetInstanceID()}");
        report.AppendLine(
            $"{indent}activeSelf={target.activeSelf} activeInHierarchy={target.activeInHierarchy} layer={target.layer}");
        report.AppendLine(
            $"{indent}worldPosition={FormatVector(target.transform.position)} localPosition={FormatVector(target.transform.localPosition)}");

        Component[] components = target.GetComponentsInChildren<Component>(true);
        report.AppendLine($"{indent}componentsAndKnownWriters:");

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];

            if (component == null)
            {
                continue;
            }

            string enabledState = component is Behaviour behaviour
                ? $" enabled={behaviour.enabled} active={behaviour.gameObject.activeInHierarchy}"
                : string.Empty;
            string writerMarker = IsKnownSortingWriter(component.GetType().Name) ? " SORTING_WRITER" : string.Empty;
            report.AppendLine(
                $"{indent}  {GetPath(component.transform)} :: {component.GetType().FullName} " +
                $"instanceId={component.GetInstanceID()}{enabledState}{writerMarker}");
        }

        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
        report.AppendLine($"{indent}spriteRenderers={renderers.Length}");

        for (int i = 0; i < renderers.Length; i++)
        {
            AppendRenderer(report, renderers[i], indent + "  ");
        }

        SortingGroup[] groups = target.GetComponentsInChildren<SortingGroup>(true);
        report.AppendLine($"{indent}localSortingGroups={groups.Length}");

        for (int i = 0; i < groups.Length; i++)
        {
            AppendSortingGroup(report, groups[i], indent + "  ");
        }
    }

    private static void AppendRenderer(StringBuilder report, SpriteRenderer renderer, string indent)
    {
        if (renderer == null)
        {
            return;
        }

        string spritePath = renderer.sprite != null ? AssetDatabase.GetAssetPath(renderer.sprite) : string.Empty;
        string spriteGuid = string.IsNullOrEmpty(spritePath) ? string.Empty : AssetDatabase.AssetPathToGUID(spritePath);
        Material material = renderer.sharedMaterial;
        Shader shader = material != null ? material.shader : null;
        report.AppendLine($"{indent}RENDERER path={GetPath(renderer.transform)} instanceId={renderer.GetInstanceID()}");
        report.AppendLine(
            $"{indent}  enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy} " +
            $"sprite={renderer.sprite?.name ?? "<null>"} spritePath={spritePath} spriteGuid={spriteGuid}");
        report.AppendLine(
            $"{indent}  layer={renderer.sortingLayerName} layerId={renderer.sortingLayerID} " +
            $"layerValue={SortingLayer.GetLayerValueFromID(renderer.sortingLayerID)} order={renderer.sortingOrder} " +
            $"sortPoint={renderer.spriteSortPoint} rendererPriority={renderer.rendererPriority}");
        report.AppendLine(
            $"{indent}  worldZ={renderer.transform.position.z:R} localZ={renderer.transform.localPosition.z:R} " +
            $"material={material?.name ?? "<null>"} shader={shader?.name ?? "<null>"} " +
            $"renderQueue={(material != null ? material.renderQueue : -1)}");

        if (material != null && shader != null)
        {
            for (int pass = 0; pass < material.passCount; pass++)
            {
                report.AppendLine($"{indent}  shaderPass[{pass}]={material.GetPassName(pass)}");
            }
        }

        report.AppendLine($"{indent}  enabledSortingGroupsInAncestry:");
        bool foundGroup = false;

        for (Transform cursor = renderer.transform; cursor != null; cursor = cursor.parent)
        {
            SortingGroup[] groups = cursor.GetComponents<SortingGroup>();

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null || !groups[i].enabled)
                {
                    continue;
                }

                foundGroup = true;
                AppendSortingGroup(report, groups[i], indent + "    ");
            }
        }

        if (!foundGroup)
        {
            report.AppendLine($"{indent}    <none>");
        }
    }

    private static void AppendSortingGroup(StringBuilder report, SortingGroup group, string indent)
    {
        if (group == null)
        {
            return;
        }

        report.AppendLine(
            $"{indent}GROUP path={GetPath(group.transform)} instanceId={group.GetInstanceID()} " +
            $"enabled={group.enabled} active={group.gameObject.activeInHierarchy} " +
            $"layer={group.sortingLayerName} layerId={group.sortingLayerID} " +
            $"layerValue={SortingLayer.GetLayerValueFromID(group.sortingLayerID)} " +
            $"order={group.sortingOrder} sortAtRoot={group.sortAtRoot}");
    }

    private static void AppendAnimationBindings(StringBuilder report, ActorRoomState guest)
    {
        report.AppendLine("TARGET YELLOW-DRESS ANIMATION STATE AND SORTING-RELEVANT BINDINGS");

        if (guest == null)
        {
            report.AppendLine("  <guest not found>");
            report.AppendLine();
            return;
        }

        Animator[] animators = guest.GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            report.AppendLine(
                $"  animator={GetPath(animator.transform)} enabled={animator.enabled} " +
                $"active={animator.gameObject.activeInHierarchy} controller={animator.runtimeAnimatorController?.name ?? "<null>"}");

            if (animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                report.AppendLine(
                    $"    stateHash={state.fullPathHash} normalizedTime={state.normalizedTime:R} speed={state.speed:R}");
            }

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;

            if (controller == null)
            {
                continue;
            }

            AnimationClip[] clips = controller.animationClips;

            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                AnimationClip clip = clips[clipIndex];

                if (clip == null)
                {
                    continue;
                }

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                ObjectReferenceKeyframe[][] objectCurves = GetObjectReferenceCurves(clip);
                bool wroteClipHeader = false;

                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    EditorCurveBinding binding = bindings[bindingIndex];

                    if (!IsSortingRelevantBinding(binding.propertyName))
                    {
                        continue;
                    }

                    if (!wroteClipHeader)
                    {
                        report.AppendLine($"    clip={AssetDatabase.GetAssetPath(clip)}");
                        wroteClipHeader = true;
                    }

                    report.AppendLine(
                        $"      curve path={binding.path} type={binding.type?.FullName} property={binding.propertyName}");
                }

                EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

                for (int bindingIndex = 0; bindingIndex < objectBindings.Length; bindingIndex++)
                {
                    EditorCurveBinding binding = objectBindings[bindingIndex];

                    if (!IsSortingRelevantBinding(binding.propertyName))
                    {
                        continue;
                    }

                    if (!wroteClipHeader)
                    {
                        report.AppendLine($"    clip={AssetDatabase.GetAssetPath(clip)}");
                        wroteClipHeader = true;
                    }

                    int keyCount = bindingIndex < objectCurves.Length ? objectCurves[bindingIndex].Length : 0;
                    report.AppendLine(
                        $"      objectCurve path={binding.path} type={binding.type?.FullName} " +
                        $"property={binding.propertyName} keys={keyCount}");
                }
            }
        }

        report.AppendLine();
    }

    private static ObjectReferenceKeyframe[][] GetObjectReferenceCurves(AnimationClip clip)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        ObjectReferenceKeyframe[][] curves = new ObjectReferenceKeyframe[bindings.Length][];

        for (int i = 0; i < bindings.Length; i++)
        {
            curves[i] = AnimationUtility.GetObjectReferenceCurve(clip, bindings[i]);
        }

        return curves;
    }

    private static void AppendExecutionOrders(
        StringBuilder report,
        ActorRoomState guest,
        Transform chair,
        Transform rail)
    {
        report.AppendLine("MONOBEHAVIOUR EXECUTION ORDERS");
        HashSet<MonoBehaviour> behaviours = new HashSet<MonoBehaviour>();
        AddBehaviours(behaviours, guest != null ? guest.transform : null);
        AddBehaviours(behaviours, chair);
        AddBehaviours(behaviours, rail);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }

            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            report.AppendLine(
                $"  {GetPath(behaviour.transform)} :: {behaviour.GetType().FullName} " +
                $"enabled={behaviour.enabled} active={behaviour.gameObject.activeInHierarchy} " +
                $"projectOverrideOrder={(script != null ? MonoImporter.GetExecutionOrder(script) : 0)} " +
                $"defaultExecutionOrderAttribute={GetDefaultExecutionOrder(behaviour.GetType())}");
        }

        report.AppendLine();
    }

    private static int GetDefaultExecutionOrder(Type behaviourType)
    {
        DefaultExecutionOrder attribute =
            Attribute.GetCustomAttribute(
                behaviourType,
                typeof(DefaultExecutionOrder),
                true) as DefaultExecutionOrder;
        return attribute != null ? attribute.order : 0;
    }

    private static void AddBehaviours(HashSet<MonoBehaviour> destination, Transform root)
    {
        if (root == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
            {
                destination.Add(behaviours[i]);
            }
        }
    }

    private static ActorRoomState FindTargetGuest()
    {
        ActorRoomState[] actors =
            UnityEngine.Object.FindObjectsByType<ActorRoomState>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < actors.Length; i++)
        {
            if (actors[i] != null &&
                string.Equals(actors[i].ActorId, GuestActorId, StringComparison.OrdinalIgnoreCase))
            {
                return actors[i];
            }
        }

        return null;
    }

    private static Transform FindTransformInLoadedScenes(string objectName)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] descendants = roots[rootIndex].GetComponentsInChildren<Transform>(true);

                for (int childIndex = 0; childIndex < descendants.Length; childIndex++)
                {
                    if (descendants[childIndex] != null &&
                        string.Equals(descendants[childIndex].name, objectName, StringComparison.Ordinal))
                    {
                        return descendants[childIndex];
                    }
                }
            }
        }

        return null;
    }

    private static SpriteRenderer FindRendererWithSpriteGuid(GameObject root, string expectedGuid)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer.sprite == null)
            {
                continue;
            }

            string path = AssetDatabase.GetAssetPath(renderer.sprite);

            if (string.Equals(AssetDatabase.AssetPathToGUID(path), expectedGuid, StringComparison.Ordinal))
            {
                return renderer;
            }
        }

        return null;
    }

    private static Camera ResolveGameplayCamera()
    {
        Camera[] cameras =
            UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            if (IsGameplayCamera(cameras[i]))
            {
                return cameras[i];
            }
        }

        return Camera.main;
    }

    private static bool IsGameplayCamera(Camera camera)
    {
        return camera != null &&
            camera.enabled &&
            camera.gameObject.activeInHierarchy &&
            camera.cameraType == CameraType.Game;
    }

    private static bool IsKnownSortingWriter(string typeName)
    {
        for (int i = 0; i < SortingWriterTypeNames.Length; i++)
        {
            if (string.Equals(SortingWriterTypeNames[i], typeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSortingRelevantBinding(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        string normalized = propertyName.ToLowerInvariant();
        return normalized.Contains("sorting") ||
            normalized.Contains("material") ||
            normalized.Contains("rendererpriority") ||
            normalized.Contains("enabled") ||
            normalized.Contains("isactive") ||
            normalized.Contains("localposition") ||
            normalized.Contains("sprite");
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        List<string> segments = new List<string>();

        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
        {
            segments.Add(cursor.name);
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:R},{value.y:R},{value.z:R})";
    }
}
