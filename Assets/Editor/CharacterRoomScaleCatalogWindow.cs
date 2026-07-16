using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CharacterRoomScaleCatalogWindow : EditorWindow
{
    private string selectedRoomId = string.Empty;
    private Vector2 scroll;

    [MenuItem("Tools/Butler/Room Scale Calibration")]
    [MenuItem("Tools/Characters/Character Room Scale Catalog")]
    public static void Open()
    {
        GetWindow<CharacterRoomScaleCatalogWindow>("Character Room Scale");
    }

    private void OnGUI()
    {
        CharacterRoomScaleCatalog catalog = CharacterRoomScaleCatalog.FindInScene();
        CharacterRoomScaleController controller =
            FindAnyObjectByType<CharacterRoomScaleController>(FindObjectsInactive.Include);

        EditorGUILayout.LabelField("Character Room Scale", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each room has one Butler-calibrated foot-Y range, curve, and display-size endpoints shared by every Butler and Guest. " +
            "It changes localScale only; movement, position, sorting, tint, animation, and gameplay remain outside this module.",
            MessageType.Info);

        if (catalog == null)
        {
            EditorGUILayout.HelpBox("No CharacterRoomScaleCatalog exists in the open scene.", MessageType.Error);

            if (GUILayout.Button("Create Catalog And Controller"))
            {
                CreateInfrastructure();
            }

            return;
        }

        if (controller == null)
        {
            EditorGUILayout.HelpBox("No CharacterRoomScaleController exists in the open scene.", MessageType.Warning);

            if (GUILayout.Button("Create Controller"))
            {
                controller = CreateControllerWithUndo(catalog);
            }
        }

        string[] roomOptions = BuildRoomOptions(catalog);

        if (roomOptions.Length == 0)
        {
            EditorGUILayout.HelpBox("The catalog has no rooms.", MessageType.Warning);
            return;
        }

        selectedRoomId = ResolveSelectedRoom(roomOptions, selectedRoomId);
        int selectedIndex = Array.FindIndex(
            roomOptions,
            room => CharacterRoomScaleCatalog.SameRoom(room, selectedRoomId));
        selectedIndex = Mathf.Max(0, selectedIndex);

        EditorGUI.BeginChangeCheck();
        selectedIndex = EditorGUILayout.Popup("Room", selectedIndex, roomOptions);

        if (EditorGUI.EndChangeCheck())
        {
            selectedRoomId = roomOptions[selectedIndex];
        }

        CharacterRoomScaleEntry entry = catalog.GetOrCreateRoom(selectedRoomId);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawEntry(catalog, entry);
        EditorGUILayout.Space(12f);
        DrawSelectionCapture(catalog, controller, entry);
        EditorGUILayout.Space(12f);
        DrawValidation(catalog);
        EditorGUILayout.EndScrollView();
    }

    private void DrawEntry(CharacterRoomScaleCatalog catalog, CharacterRoomScaleEntry entry)
    {
        EditorGUILayout.LabelField("Room Endpoints", EditorStyles.boldLabel);
        Undo.RecordObject(catalog, "Edit Character Room Scale");
        EditorGUI.BeginChangeCheck();
        entry.enabled = EditorGUILayout.Toggle("Enabled", entry.enabled);
        entry.frontRoomLocalFootY = EditorGUILayout.FloatField("Front Foot Y", entry.frontRoomLocalFootY);
        entry.backRoomLocalFootY = EditorGUILayout.FloatField("Back Foot Y", entry.backRoomLocalFootY);
        entry.frontFinalLocalScaleY = EditorGUILayout.FloatField(
            "Front Display Size (All Characters)",
            entry.frontFinalLocalScaleY);
        entry.backFinalLocalScaleY = EditorGUILayout.FloatField(
            "Back Display Size (All Characters)",
            entry.backFinalLocalScaleY);

        entry.scaleFunction = EditorGUILayout.CurveField("Scale Function", entry.scaleFunction);
        entry.hasReferenceRoomStageScale = EditorGUILayout.Toggle(
            "Use Stage Reference",
            entry.hasReferenceRoomStageScale);

        using (new EditorGUI.DisabledScope(!entry.hasReferenceRoomStageScale))
        {
            entry.referenceRoomStageScale = EditorGUILayout.FloatField(
                "Reference Stage Scale",
                entry.referenceRoomStageScale);
        }

        if (EditorGUI.EndChangeCheck())
        {
            entry.Sanitize();
            catalog.MarkChanged();
            EditorUtility.SetDirty(catalog);
            MarkSceneDirty(catalog);
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Usable Endpoints", entry.HasUsableEndpoints);
        }
    }

    private void DrawSelectionCapture(
        CharacterRoomScaleCatalog catalog,
        CharacterRoomScaleController controller,
        CharacterRoomScaleEntry entry)
    {
        EditorGUILayout.LabelField("Selected Character Preview", EditorStyles.boldLabel);
        CharacterRoomScaleTarget target = ResolveSelectedTarget();

        if (target == null)
        {
            EditorGUILayout.HelpBox(
                "Select a Butler or Guest object with CharacterRoomScaleTarget to capture or preview.",
                MessageType.None);
            return;
        }

        string runtimeRoomId = target.ResolveRoomId();
        target.TryResolveRoomScaleContext(
            selectedRoomId,
            true,
            out string previewRoomId,
            out Vector2 roomLocalFootPoint);
        Transform scaleRoot = target.ResolveScaleRoot();
        float currentLocalScaleY = scaleRoot != null ? Mathf.Abs(scaleRoot.localScale.y) : 1f;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Target", target, typeof(CharacterRoomScaleTarget), true);
            EditorGUILayout.TextField("Runtime-resolved Room", runtimeRoomId);
            EditorGUILayout.TextField("Preview / Capture Room", previewRoomId);
            EditorGUILayout.FloatField("Room-local Foot Y", roomLocalFootPoint.y);
            EditorGUILayout.FloatField("Displayed Local Scale Y", currentLocalScaleY);
        }

        bool isButlerTarget = target.ResolvedScaleProfile == CharacterScaleProfile.Butler;

        if (!isButlerTarget)
        {
            EditorGUILayout.HelpBox(
                "Select the Butler to capture room endpoints. Guests always use the resulting shared calibration.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!isButlerTarget))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Capture Butler As Front"))
            {
                CaptureEndpoint(catalog, target, true);
            }

            if (GUILayout.Button("Capture Butler As Back"))
            {
                CaptureEndpoint(catalog, target, false);
            }

            EditorGUILayout.EndHorizontal();
        }

        using (new EditorGUI.DisabledScope(scaleRoot == null))
        {
            if (GUILayout.Button("Apply Catalog Preview To Selected Character"))
            {
                controller ??= CreateControllerWithUndo(catalog);

                if (scaleRoot != null)
                {
                    Undo.RecordObject(scaleRoot, "Preview Character Room Scale");
                    controller.RefreshTargetNow(target, selectedRoomId, true);
                    EditorUtility.SetDirty(scaleRoot);
                    MarkSceneDirty(target);
                }
            }
        }

        if (!CharacterRoomScaleCatalog.SameRoom(selectedRoomId, runtimeRoomId))
        {
            EditorGUILayout.HelpBox(
                $"Runtime currently resolves this character to '{runtimeRoomId}'. " +
                $"The preview and capture buttons intentionally use the selected catalog room '{selectedRoomId}'.",
                MessageType.Info);
        }
    }

    private void CaptureEndpoint(
        CharacterRoomScaleCatalog catalog,
        CharacterRoomScaleTarget target,
        bool front)
    {
        if (target.ResolvedScaleProfile != CharacterScaleProfile.Butler)
        {
            Debug.LogWarning("Only the Butler can capture the shared room-scale calibration.", target);
            return;
        }

        if (!target.TryResolveRoomScaleContext(
                selectedRoomId,
                true,
                out string roomId,
                out Vector2 roomLocalFootPoint) ||
            string.IsNullOrWhiteSpace(roomId))
        {
            Debug.LogWarning("The selected character has no resolvable room-scale context.", target);
            return;
        }

        Transform root = target.ResolveScaleRoot();

        if (root == null)
        {
            return;
        }

        float currentZoom = CharacterRoomStageScaleUtility.GetCurrentZoomRatio(catalog, roomId);
        float inheritedZoom = CharacterRoomStageScaleUtility.GetInheritedZoomRatio(target, roomId, currentZoom);
        float calibratedScale = Mathf.Abs(root.localScale.y) * inheritedZoom /
            Mathf.Max(0.0001f, currentZoom);
        Undo.RecordObject(catalog, front ? "Capture Character Front Scale" : "Capture Character Back Scale");

        if (front)
        {
            catalog.SetFront(roomId, roomLocalFootPoint.y, calibratedScale);
        }
        else
        {
            catalog.SetBack(roomId, roomLocalFootPoint.y, calibratedScale);
        }

        selectedRoomId = roomId;
        EditorUtility.SetDirty(catalog);
        MarkSceneDirty(catalog);
    }

    private static void DrawValidation(CharacterRoomScaleCatalog catalog)
    {
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        int invalid = 0;

        for (int i = 0; i < catalog.Rooms.Count; i++)
        {
            CharacterRoomScaleEntry entry = catalog.Rooms[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.roomId) || !entry.HasUsableEndpoints)
            {
                invalid++;
            }
        }

        EditorGUILayout.HelpBox(
            invalid == 0
                ? $"All {catalog.Rooms.Count} room entries have usable front/back endpoints."
                : $"{invalid} room entries are missing a name or have overlapping front/back Y endpoints.",
            invalid == 0 ? MessageType.Info : MessageType.Error);
    }

    private static CharacterRoomScaleTarget ResolveSelectedTarget()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            return null;
        }

        CharacterRoomScaleTarget target = selected.GetComponent<CharacterRoomScaleTarget>();
        target ??= selected.GetComponentInParent<CharacterRoomScaleTarget>(true);
        target ??= selected.GetComponentInChildren<CharacterRoomScaleTarget>(true);
        return target;
    }

    private static string[] BuildRoomOptions(CharacterRoomScaleCatalog catalog)
    {
        List<string> rooms = new List<string>();
        catalog.GetRoomIds(rooms);
        rooms.Sort(StringComparer.OrdinalIgnoreCase);
        return rooms.ToArray();
    }

    private static string ResolveSelectedRoom(string[] roomOptions, string current)
    {
        for (int i = 0; i < roomOptions.Length; i++)
        {
            if (CharacterRoomScaleCatalog.SameRoom(roomOptions[i], current))
            {
                return roomOptions[i];
            }
        }

        return roomOptions.Length > 0 ? roomOptions[0] : string.Empty;
    }

    private static void CreateInfrastructure()
    {
        GameObject catalogObject = new GameObject("CharacterRoomScaleCatalog");
        Undo.RegisterCreatedObjectUndo(catalogObject, "Create Character Room Scale Catalog");
        CharacterRoomScaleCatalog catalog = catalogObject.AddComponent<CharacterRoomScaleCatalog>();
        CreateControllerWithUndo(catalog);
        Selection.activeObject = catalogObject;
        MarkSceneDirty(catalog);
    }

    private static CharacterRoomScaleController CreateControllerWithUndo(
        CharacterRoomScaleCatalog catalog)
    {
        CharacterRoomScaleController existing =
            FindAnyObjectByType<CharacterRoomScaleController>(FindObjectsInactive.Include);

        if (existing != null)
        {
            if (existing.Catalog != catalog)
            {
                Undo.RecordObject(existing, "Assign Character Room Scale Catalog");
                existing.SetCatalog(catalog);
                EditorUtility.SetDirty(existing);
                MarkSceneDirty(existing);
            }

            return existing;
        }

        GameObject controllerObject = new GameObject("CharacterRoomScaleController");
        Undo.RegisterCreatedObjectUndo(controllerObject, "Create Character Room Scale Controller");
        CharacterRoomScaleController controller =
            controllerObject.AddComponent<CharacterRoomScaleController>();
        controller.SetCatalog(catalog);
        EditorUtility.SetDirty(controller);
        MarkSceneDirty(controller);
        return controller;
    }

    private static void MarkSceneDirty(Component component)
    {
        if (component != null && component.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
    }
}
