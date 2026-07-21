using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class CharacterDisplayScaleAuthoringWindow : EditorWindow
{
    private enum AuthoringMode
    {
        RoomDefault,
        IndividualCharacterOverride
    }

    private enum PreviewLocation
    {
        Front,
        Middle,
        Back,
        CurrentRoomLocalFootY,
        NormalizedDepth
    }

    private struct CalibrationDraft
    {
        public Vector2 FrontPreviewPosition;
        public float FrontScale;
        public Vector2 BackPreviewPosition;
        public float BackScale;
        public AnimationCurve YToScaleCurve;
        public bool Enabled;
    }

    private static readonly string[] AuthoringModeLabels =
    {
        "Room Default (All Characters)",
        "Individual Character Override"
    };

    private CharacterDisplayScaleCatalog catalog;
    private readonly List<string> roomIds = new List<string>();
    private int selectedRoomIndex;
    private AuthoringMode authoringMode;
    private CharacterDisplayId selectedCharacterId = CharacterDisplayId.Butler;
    private PreviewLocation previewLocation = PreviewLocation.Middle;
    private float normalizedPreviewDepth = 0.5f;
    private Vector2 scrollPosition;
    private string statusMessage;
    private MessageType statusType = MessageType.Info;

    private CharacterDisplayScaleSubject selectedSubject;
    private CharacterDisplayScaleSubject previewSubject;
    private Transform previewTarget;
    private Vector3 previewOriginalScale;
    private bool previewActive;
    private static CharacterDisplayScaleAuthoringWindow testPreviewWindow;

    [MenuItem("Tools/Chateau/Universal Character Display Scale")]
    public static void Open()
    {
        CharacterDisplayScaleAuthoringWindow window =
            GetWindow<CharacterDisplayScaleAuthoringWindow>("Character Display Scale");
        window.minSize = new Vector2(560f, 700f);
        window.Show();
    }

    public static bool PreviewSubjectForTests(
        CharacterDisplayScaleSubject subject,
        float targetScale)
    {
        RestorePreviewForTests();

        if (subject == null || !subject.HasValidVisualScaleRoot())
        {
            return false;
        }

        CharacterDisplayScaleAuthoringWindow window =
            CreateInstance<CharacterDisplayScaleAuthoringWindow>();
        window.hideFlags = HideFlags.HideAndDontSave;
        window.selectedSubject = subject;
        window.previewSubject = subject;
        window.previewTarget = subject.VisualScaleRoot;
        window.previewOriginalScale = window.previewTarget.localScale;
        window.previewActive = true;

        try
        {
            window.previewTarget.localScale = subject.GetDeterministicScaleVector(targetScale);
            testPreviewWindow = window;
            return true;
        }
        catch
        {
            window.RestorePreview();
            DestroyImmediate(window);
            throw;
        }
    }

    public static void RestorePreviewForTests()
    {
        CharacterDisplayScaleAuthoringWindow window = testPreviewWindow;
        testPreviewWindow = null;

        if (window == null)
        {
            return;
        }

        window.RestorePreview();
        DestroyImmediate(window);
    }

    private void OnEnable()
    {
        Selection.selectionChanged -= HandleSelectionChanged;
        Selection.selectionChanged += HandleSelectionChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        Undo.undoRedoPerformed -= HandleUndoRedo;
        Undo.undoRedoPerformed += HandleUndoRedo;

        selectedSubject = ResolveSelectedSubject();

        if (catalog == null)
        {
            FindCatalogAsset(false);
        }

        RefreshRoomList();
    }

    private void OnDisable()
    {
        RestorePreview();
        UnsubscribeEditorCallbacks();
    }

    private void OnDestroy()
    {
        RestorePreview();
        UnsubscribeEditorCallbacks();
    }

    private void OnHierarchyChange()
    {
        CharacterDisplayScaleSubject nextSubject = ResolveSelectedSubject();

        if (nextSubject != previewSubject && previewActive)
        {
            RestorePreview();
        }

        selectedSubject = nextSubject;
        RefreshRoomList();
        Repaint();
    }

    private void OnProjectChange()
    {
        if (catalog == null)
        {
            RestorePreview();
        }

        RefreshRoomList();
        Repaint();
    }

    private void OnInspectorUpdate()
    {
        if (!previewActive)
        {
            return;
        }

        try
        {
            ReapplyActivePreview();
            Repaint();
        }
        catch (Exception exception)
        {
            RestorePreview();
            Debug.LogException(exception, this);
        }
    }

    private void OnGUI()
    {
        try
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawCatalogControls();

            if (catalog != null)
            {
                DrawRoomAndModeControls();
                DrawSelectedCalibration();
                DrawStateOverrides();
                DrawPreview();
                DrawValidation();
            }

            DrawStatus();
            EditorGUILayout.EndScrollView();
        }
        catch (Exception exception)
        {
            RestorePreview();
            Debug.LogException(exception, this);
        }
    }

    private void DrawCatalogControls()
    {
        EditorGUILayout.LabelField("Character Display Scale Catalog", EditorStyles.boldLabel);

        CharacterDisplayScaleCatalog nextCatalog =
            (CharacterDisplayScaleCatalog)EditorGUILayout.ObjectField(
                "Catalog Asset",
                catalog,
                typeof(CharacterDisplayScaleCatalog),
                false);

        if (nextCatalog != catalog)
        {
            RestorePreview();
            catalog = nextCatalog;
            selectedRoomIndex = 0;
            RefreshRoomList();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Catalog"))
            {
                RestorePreview();
                FindCatalogAsset(true);
                RefreshRoomList();
            }

            if (GUILayout.Button("Create Default Catalog"))
            {
                RestorePreview();
                CreateDefaultCatalogAsset();
                RefreshRoomList();
            }

            using (new EditorGUI.DisabledScope(catalog == null || !EditorUtility.IsPersistent(catalog)))
            {
                if (GUILayout.Button("Save Catalog Asset"))
                {
                    AssetDatabase.SaveAssetIfDirty(catalog);
                    SetStatus($"Saved {AssetDatabase.GetAssetPath(catalog)}.", MessageType.Info);
                }
            }
        }

        if (catalog == null)
        {
            EditorGUILayout.HelpBox(
                $"Select a CharacterDisplayScaleCatalog or create the default asset at " +
                $"{CharacterDisplayScaleCatalog.DefaultAssetPath}.",
                MessageType.Error);
        }
    }

    private void DrawRoomAndModeControls()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Authoring Target", EditorStyles.boldLabel);

        if (roomIds.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No room ids were found in the selected catalog or loaded RoomContentGroup objects.",
                MessageType.Error);
            return;
        }

        selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, roomIds.Count - 1);
        int nextRoomIndex = EditorGUILayout.Popup("Room", selectedRoomIndex, roomIds.ToArray());

        if (nextRoomIndex != selectedRoomIndex)
        {
            RestorePreview();
            selectedRoomIndex = nextRoomIndex;
        }

        int nextModeIndex = EditorGUILayout.Popup(
            "Mode",
            (int)authoringMode,
            AuthoringModeLabels);
        AuthoringMode nextMode = (AuthoringMode)nextModeIndex;

        if (nextMode != authoringMode)
        {
            RestorePreview();
            authoringMode = nextMode;
        }

        if (authoringMode == AuthoringMode.IndividualCharacterOverride)
        {
            CharacterDisplayId nextCharacterId = (CharacterDisplayId)EditorGUILayout.EnumPopup(
                "Character Id",
                selectedCharacterId);

            if (nextCharacterId != selectedCharacterId)
            {
                RestorePreview();
                selectedCharacterId = nextCharacterId;
            }
        }

        string selectedRoomId = GetSelectedRoomId();

        if (!catalog.TryGetRoom(selectedRoomId, out _))
        {
            EditorGUILayout.HelpBox(
                $"Selected room '{selectedRoomId}' is not present in the catalog.",
                MessageType.Error);

            if (GUILayout.Button("Add Room Entry"))
            {
                AddSelectedRoomEntry();
            }
        }
    }

    private void DrawSelectedCalibration()
    {
        if (!TryGetSelectedRoom(out RoomDisplayScaleEntry room))
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Calibration", EditorStyles.boldLabel);

        if (authoringMode == AuthoringMode.IndividualCharacterOverride)
        {
            DrawOverrideManagement(room);

            if (!room.TryGetCharacterOverride(selectedCharacterId, out _))
            {
                return;
            }
        }

        if (!TryGetSelectedCalibration(room, out CalibrationDraft draft))
        {
            return;
        }

        EditorGUI.BeginChangeCheck();

        if (authoringMode == AuthoringMode.IndividualCharacterOverride)
        {
            draft.Enabled = EditorGUILayout.Toggle("Override Enabled", draft.Enabled);
        }

        draft.FrontPreviewPosition = EditorGUILayout.Vector2Field(
            "Front Preview Position",
            draft.FrontPreviewPosition);
        draft.FrontScale = EditorGUILayout.FloatField("Front Scale", draft.FrontScale);
        draft.BackPreviewPosition = EditorGUILayout.Vector2Field(
            "Back Preview Position",
            draft.BackPreviewPosition);
        draft.BackScale = EditorGUILayout.FloatField("Back Scale", draft.BackScale);
        draft.YToScaleCurve = EditorGUILayout.CurveField("Y To Scale Curve", draft.YToScaleCurve);

        if (EditorGUI.EndChangeCheck())
        {
            CommitCalibration(room, draft, "Edit Character Display Scale Calibration");
        }

        DrawCaptureButtons();
    }

    private void DrawOverrideManagement(RoomDisplayScaleEntry room)
    {
        bool hasOverride = room.TryGetCharacterOverride(selectedCharacterId, out _);

        if (!hasOverride)
        {
            EditorGUILayout.HelpBox(
                $"{selectedCharacterId} has no individual override. The room default applies at runtime.",
                MessageType.Warning);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Copy Room Default Into Character Override"))
            {
                CopyRoomDefaultIntoOverride(room);
            }

            using (new EditorGUI.DisabledScope(!hasOverride))
            {
                if (GUILayout.Button("Remove Character Override"))
                {
                    RemoveSelectedCharacterOverride(room);
                }
            }
        }
    }

    private void DrawCaptureButtons()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Capture From Selection", EditorStyles.miniBoldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Front From Selected Character"))
            {
                CapturePosition(true);
            }

            if (GUILayout.Button("Capture Back From Selected Character"))
            {
                CapturePosition(false);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Front Scale From Selected Character"))
            {
                CaptureScale(true);
            }

            if (GUILayout.Button("Capture Back Scale From Selected Character"))
            {
                CaptureScale(false);
            }
        }
    }

    private void DrawStateOverrides()
    {
        if (!TryGetSelectedRoom(out RoomDisplayScaleEntry room))
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Allowed State Overrides", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Only the Drawing Room seated and Dining Room seated display scales are allowed. " +
            "They are direct, deterministic scale values and take precedence over the normal Y curve.",
            MessageType.Info);

        string normalizedRoomId = CharacterDisplayScaleCatalog.NormalizeRoomId(room.RoomId);
        bool isDrawingRoom = normalizedRoomId == CharacterDisplayScaleCatalog.NormalizeRoomId(
            CharacterDisplayScaleCatalog.DrawingRoomId);
        bool isDiningRoom = normalizedRoomId == CharacterDisplayScaleCatalog.NormalizeRoomId(
            CharacterDisplayScaleCatalog.DiningRoomId);

        DrawStateOverride(
            room,
            "Drawing Room Seated Override",
            true,
            isDrawingRoom,
            room.StateOverrides.DrawingRoomSeated);
        DrawStateOverride(
            room,
            "Dining Room Seated Override",
            false,
            isDiningRoom,
            room.StateOverrides.DiningRoomSeated);
    }

    private void DrawStateOverride(
        RoomDisplayScaleEntry room,
        string label,
        bool drawingRoomOverride,
        bool allowedForSelectedRoom,
        CharacterDisplayStateScaleOverride stateOverride)
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

        if (!allowedForSelectedRoom)
        {
            if (stateOverride.Enabled)
            {
                EditorGUILayout.HelpBox(
                    $"This override is enabled on '{room.RoomId}', where it is not allowed.",
                    MessageType.Error);

                if (GUILayout.Button($"Disable Invalid {label}"))
                {
                    CommitStateOverride(room, drawingRoomOverride, false, stateOverride.Scale);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Not available for this room.", EditorStyles.miniLabel);
            }

            return;
        }

        bool enabled = stateOverride.Enabled;
        float scale = stateOverride.Scale;
        EditorGUI.BeginChangeCheck();
        enabled = EditorGUILayout.Toggle("Enabled", enabled);

        using (new EditorGUI.DisabledScope(!enabled))
        {
            scale = EditorGUILayout.FloatField("Seated Display Scale", scale);
        }

        if (EditorGUI.EndChangeCheck())
        {
            CommitStateOverride(room, drawingRoomOverride, enabled, scale);
        }
    }

    private void DrawPreview()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Temporary Visual Preview", EditorStyles.boldLabel);
        selectedSubject = ResolveSelectedSubject();
        DrawSubjectReadout();

        if (selectedSubject == null)
        {
            EditorGUILayout.HelpBox(
                "Select a GameObject on or beneath a CharacterDisplayScaleSubject to preview or capture.",
                MessageType.Warning);
        }
        else if (!selectedSubject.HasValidVisualScaleRoot())
        {
            EditorGUILayout.HelpBox(
                $"Selected subject '{selectedSubject.name}' has no valid dedicated visual scale root.",
                MessageType.Error);
        }

        using (new EditorGUI.DisabledScope(
                   selectedSubject == null ||
                   !selectedSubject.HasValidVisualScaleRoot() ||
                   EditorApplication.isPlayingOrWillChangePlaymode))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview Front"))
                {
                    BeginPreview(PreviewLocation.Front);
                }

                if (GUILayout.Button("Preview Middle"))
                {
                    BeginPreview(PreviewLocation.Middle);
                }

                if (GUILayout.Button("Preview Back"))
                {
                    BeginPreview(PreviewLocation.Back);
                }

                if (GUILayout.Button("Preview Current Foot Y"))
                {
                    BeginPreview(PreviewLocation.CurrentRoomLocalFootY);
                }
            }

            EditorGUI.BeginChangeCheck();
            normalizedPreviewDepth = EditorGUILayout.Slider(
                "Normalized Depth",
                normalizedPreviewDepth,
                0f,
                1f);
            bool depthChanged = EditorGUI.EndChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview Slider Depth"))
                {
                    BeginPreview(PreviewLocation.NormalizedDepth);
                }

                using (new EditorGUI.DisabledScope(!previewActive))
                {
                    if (GUILayout.Button("Stop Preview"))
                    {
                        RestorePreview();
                    }
                }
            }

            if (depthChanged && previewActive && previewLocation == PreviewLocation.NormalizedDepth)
            {
                ReapplyActivePreview();
            }
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox(
                "Temporary authoring preview is disabled during Play Mode and while entering or leaving Play Mode.",
                MessageType.Info);
        }

        if (TryComputePreview(out float previewY, out float previewScale, out string source))
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Preview Room-Local Y", previewY);
                EditorGUILayout.FloatField("Live Computed Scale", previewScale);
                EditorGUILayout.TextField("Scale Source", source);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "The selected calibration cannot currently produce a preview scale.",
                MessageType.Warning);
        }

        EditorGUILayout.HelpBox(
            "Preview changes only the selected subject's visualScaleRoot.localScale. " +
            "It never moves the actor or writes preview values to the catalog.",
            MessageType.Info);
    }

    private void DrawSubjectReadout()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                "Selected Subject",
                selectedSubject,
                typeof(CharacterDisplayScaleSubject),
                true);

            if (selectedSubject == null)
            {
                EditorGUILayout.TextField("Current State", "<no subject selected>");
                EditorGUILayout.TextField("Current Room", "<unknown>");
                EditorGUILayout.TextField("Current Room-Local Foot Y", "<unknown>");
                return;
            }

            EditorGUILayout.EnumPopup("Subject Character Id", selectedSubject.CharacterId);

            if (TryReadSubjectContext(
                    selectedSubject,
                    out string roomId,
                    out float roomLocalFootY,
                    out CharacterDisplayState state))
            {
                EditorGUILayout.EnumPopup("Current State", state);
                EditorGUILayout.TextField("Current Room", roomId);
                EditorGUILayout.TextField(
                    "Current Room-Local Foot Y",
                    IsFinite(roomLocalFootY) ? roomLocalFootY.ToString("0.####") : "<unavailable>");
            }
            else
            {
                EditorGUILayout.TextField("Current State", "<unavailable>");
                EditorGUILayout.TextField("Current Room", "<unavailable>");
                EditorGUILayout.TextField("Current Room-Local Foot Y", "<unavailable>");
            }
        }
    }

    private void DrawValidation()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        if (catalog == null)
        {
            EditorGUILayout.HelpBox("A CharacterDisplayScaleCatalog is required.", MessageType.Error);
            return;
        }

        bool catalogValid = catalog.ValidateCatalog(out string catalogReport);
        EditorGUILayout.HelpBox(catalogReport, catalogValid ? MessageType.Info : MessageType.Error);

        string selectedRoomId = GetSelectedRoomId();

        if (string.IsNullOrWhiteSpace(selectedRoomId))
        {
            EditorGUILayout.HelpBox("The selected room id is missing.", MessageType.Error);
        }
        else if (!catalog.TryGetRoom(selectedRoomId, out RoomDisplayScaleEntry room))
        {
            EditorGUILayout.HelpBox(
                $"Selected room '{selectedRoomId}' was not found in the catalog.",
                MessageType.Error);
        }
        else if (!room.IsConfigured(out string roomReason))
        {
            EditorGUILayout.HelpBox(
                $"Room '{selectedRoomId}': {roomReason}",
                MessageType.Error);
        }

        if (selectedSubject != null && !selectedSubject.HasValidVisualScaleRoot())
        {
            EditorGUILayout.HelpBox(
                $"Selected subject '{selectedSubject.name}' is missing a dedicated visual scale root.",
                MessageType.Error);
        }

        if (authoringMode == AuthoringMode.IndividualCharacterOverride &&
            catalog.TryGetRoom(selectedRoomId, out RoomDisplayScaleEntry selectedRoom) &&
            !selectedRoom.TryGetCharacterOverride(selectedCharacterId, out _))
        {
            EditorGUILayout.HelpBox(
                $"{selectedCharacterId} has no override in '{selectedRoomId}'.",
                MessageType.Warning);
        }

        if (selectedSubject != null &&
            TryReadSubjectContext(selectedSubject, out string subjectRoomId, out _, out _) &&
            !string.IsNullOrWhiteSpace(subjectRoomId) &&
            CharacterDisplayScaleCatalog.NormalizeRoomId(subjectRoomId) !=
            CharacterDisplayScaleCatalog.NormalizeRoomId(selectedRoomId))
        {
            EditorGUILayout.HelpBox(
                $"Selected subject is currently in '{subjectRoomId}', while the tool is authoring '{selectedRoomId}'. " +
                "Front/Middle/Back previews remain valid, but Current Foot Y capture/preview requires matching rooms.",
                MessageType.Warning);
        }
    }

    private void AddSelectedRoomEntry()
    {
        if (catalog == null)
        {
            SetStatus("Select or create a catalog before adding a room entry.", MessageType.Error);
            return;
        }

        string roomId = GetSelectedRoomId();

        if (string.IsNullOrWhiteSpace(roomId))
        {
            SetStatus("The selected room id is missing.", MessageType.Error);
            return;
        }

        float halfHeight = 1f;
        RoomContentGroup roomObject = FindLoadedRoom(roomId);
        RectTransform roomRect = roomObject != null ? roomObject.transform as RectTransform : null;

        if (roomRect != null && roomRect.rect.height > 1f)
        {
            halfHeight = roomRect.rect.height * 0.4f;
        }

        RoomDisplayScaleEntry entry = new RoomDisplayScaleEntry(
            roomId,
            new Vector2(0f, -halfHeight),
            1f,
            new Vector2(0f, halfHeight),
            1f,
            AnimationCurve.Linear(0f, 0f, 1f, 1f));

        Undo.RecordObject(catalog, $"Add {roomId} Character Display Scale Entry");
        catalog.SetRoom(entry);
        MarkCatalogDirty();
        SetStatus($"Added '{roomId}' to the catalog.", MessageType.Info);
    }

    private void CommitCalibration(
        RoomDisplayScaleEntry room,
        CalibrationDraft draft,
        string undoLabel)
    {
        if (catalog == null || room == null)
        {
            return;
        }

        Undo.RecordObject(catalog, undoLabel);
        RoomDisplayScaleEntry replacement = room.Copy();

        if (authoringMode == AuthoringMode.RoomDefault)
        {
            replacement.SetDefaultCalibration(
                draft.FrontPreviewPosition,
                draft.FrontScale,
                draft.BackPreviewPosition,
                draft.BackScale,
                draft.YToScaleCurve);
        }
        else
        {
            replacement.SetCharacterOverride(new CharacterDisplayScaleOverride(
                selectedCharacterId,
                draft.FrontPreviewPosition,
                draft.FrontScale,
                draft.BackPreviewPosition,
                draft.BackScale,
                draft.YToScaleCurve,
                draft.Enabled));
        }

        catalog.SetRoom(replacement);
        MarkCatalogDirty();
        ReapplyActivePreview();
    }

    private void CopyRoomDefaultIntoOverride(RoomDisplayScaleEntry room)
    {
        if (catalog == null || room == null)
        {
            return;
        }

        Undo.RecordObject(catalog, $"Copy {room.RoomId} Default Into {selectedCharacterId} Override");
        RoomDisplayScaleEntry replacement = room.Copy();
        replacement.SetCharacterOverride(new CharacterDisplayScaleOverride(
            selectedCharacterId,
            room.FrontPreviewPosition,
            room.FrontScale,
            room.BackPreviewPosition,
            room.BackScale,
            room.YToScaleCurve,
            true));
        catalog.SetRoom(replacement);
        MarkCatalogDirty();
        SetStatus(
            $"Copied '{room.RoomId}' room default into the {selectedCharacterId} override.",
            MessageType.Info);
        ReapplyActivePreview();
    }

    private void RemoveSelectedCharacterOverride(RoomDisplayScaleEntry room)
    {
        if (catalog == null || room == null)
        {
            return;
        }

        RestorePreview();
        RoomDisplayScaleEntry replacement = room.Copy();

        if (!replacement.RemoveCharacterOverride(selectedCharacterId))
        {
            SetStatus($"{selectedCharacterId} has no override to remove.", MessageType.Warning);
            return;
        }

        Undo.RecordObject(catalog, $"Remove {selectedCharacterId} Character Display Scale Override");
        catalog.SetRoom(replacement);
        MarkCatalogDirty();
        SetStatus(
            $"Removed the {selectedCharacterId} override from '{room.RoomId}'.",
            MessageType.Info);
    }

    private void CommitStateOverride(
        RoomDisplayScaleEntry room,
        bool drawingRoomOverride,
        bool enabled,
        float scale)
    {
        if (catalog == null || room == null)
        {
            return;
        }

        RoomStateScaleOverrides current = room.StateOverrides;
        CharacterDisplayStateScaleOverride drawing = drawingRoomOverride
            ? new CharacterDisplayStateScaleOverride(enabled, scale)
            : current.DrawingRoomSeated.Copy();
        CharacterDisplayStateScaleOverride dining = drawingRoomOverride
            ? current.DiningRoomSeated.Copy()
            : new CharacterDisplayStateScaleOverride(enabled, scale);

        Undo.RecordObject(catalog, "Edit Character Display Seated Scale Override");
        RoomDisplayScaleEntry replacement = room.Copy();
        replacement.SetStateOverrides(new RoomStateScaleOverrides(drawing, dining));
        catalog.SetRoom(replacement);
        MarkCatalogDirty();
        ReapplyActivePreview();
    }

    private void CapturePosition(bool captureFront)
    {
        if (!TryPrepareSelectedSubjectForCapture(out CharacterDisplayScaleSubject subject) ||
            !subject.TryGetCurrentRoomLocalFootPosition(out Vector2 roomLocalFootPosition))
        {
            SetStatus(
                "The selected character's current room-local foot position could not be resolved.",
                MessageType.Error);
            return;
        }

        if (!TryGetSelectedRoom(out RoomDisplayScaleEntry room) ||
            !TryGetSelectedCalibration(room, out CalibrationDraft draft))
        {
            SetStatus("The selected calibration is unavailable.", MessageType.Error);
            return;
        }

        if (captureFront)
        {
            draft.FrontPreviewPosition = roomLocalFootPosition;
        }
        else
        {
            draft.BackPreviewPosition = roomLocalFootPosition;
        }

        CommitCalibration(
            room,
            draft,
            captureFront
                ? "Capture Front Character Display Position"
                : "Capture Back Character Display Position");
        SetStatus(
            $"Captured {(captureFront ? "Front" : "Back")} room-local position from '{subject.name}'.",
            MessageType.Info);
    }

    private void CaptureScale(bool captureFront)
    {
        if (!TryPrepareSelectedSubjectForCapture(out CharacterDisplayScaleSubject subject) ||
            subject.VisualScaleRoot == null)
        {
            SetStatus("Select a character with a valid visual scale root.", MessageType.Error);
            return;
        }

        // A temporary preview must never become authored calibration accidentally.
        RestorePreview();
        float capturedScale = Mathf.Abs(subject.VisualScaleRoot.localScale.y);

        if (!IsFinite(capturedScale) || capturedScale <= 0f)
        {
            SetStatus("The selected character's visual scale is not finite and positive.", MessageType.Error);
            return;
        }

        if (!TryGetSelectedRoom(out RoomDisplayScaleEntry room) ||
            !TryGetSelectedCalibration(room, out CalibrationDraft draft))
        {
            SetStatus("The selected calibration is unavailable.", MessageType.Error);
            return;
        }

        if (captureFront)
        {
            draft.FrontScale = capturedScale;
        }
        else
        {
            draft.BackScale = capturedScale;
        }

        CommitCalibration(
            room,
            draft,
            captureFront
                ? "Capture Front Character Display Scale"
                : "Capture Back Character Display Scale");
        SetStatus(
            $"Captured {(captureFront ? "Front" : "Back")} scale {capturedScale:0.####} from '{subject.name}'.",
            MessageType.Info);
    }

    private bool TryPrepareSelectedSubjectForCapture(out CharacterDisplayScaleSubject subject)
    {
        subject = ResolveSelectedSubject();
        selectedSubject = subject;

        if (subject == null || !subject.HasValidVisualScaleRoot())
        {
            return false;
        }

        if (authoringMode == AuthoringMode.IndividualCharacterOverride &&
            subject.CharacterId != selectedCharacterId)
        {
            SetStatus(
                $"Selected subject is {subject.CharacterId}, but the active override is {selectedCharacterId}.",
                MessageType.Error);
            return false;
        }

        if (!TryReadSubjectContext(subject, out string subjectRoomId, out _, out _) ||
            CharacterDisplayScaleCatalog.NormalizeRoomId(subjectRoomId) !=
            CharacterDisplayScaleCatalog.NormalizeRoomId(GetSelectedRoomId()))
        {
            SetStatus(
                "The selected character must be in the room currently selected by the authoring tool.",
                MessageType.Error);
            return false;
        }

        return true;
    }

    private void BeginPreview(PreviewLocation location)
    {
        RestorePreview();
        selectedSubject = ResolveSelectedSubject();

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            selectedSubject == null ||
            !selectedSubject.HasValidVisualScaleRoot())
        {
            SetStatus("Select a valid character subject in Edit Mode before previewing.", MessageType.Error);
            return;
        }

        if (EditorUtility.IsPersistent(selectedSubject) || !selectedSubject.gameObject.scene.IsValid())
        {
            SetStatus(
                "Preview is available for loaded scene or Prefab Stage subjects, not persistent prefab assets.",
                MessageType.Error);
            return;
        }

        previewLocation = location;
        previewSubject = selectedSubject;
        previewTarget = selectedSubject.VisualScaleRoot;
        previewOriginalScale = previewTarget.localScale;
        previewActive = true;

        try
        {
            if (!ApplyPreviewScale(previewSubject, previewTarget))
            {
                RestorePreview();
                SetStatus("The selected calibration cannot currently produce a preview scale.", MessageType.Error);
                return;
            }

            SetStatus(
                $"Previewing {previewLocation} on '{previewSubject.name}'. Closing or changing selection restores the original scale.",
                MessageType.Info);
        }
        catch (Exception exception)
        {
            RestorePreview();
            Debug.LogException(exception, this);
            SetStatus("Preview failed and the original visual scale was restored.", MessageType.Error);
        }
    }

    private void ReapplyActivePreview()
    {
        if (!previewActive)
        {
            return;
        }

        CharacterDisplayScaleSubject activeSelection = ResolveSelectedSubject();

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            previewSubject == null ||
            previewTarget == null ||
            activeSelection != previewSubject ||
            previewSubject.VisualScaleRoot != previewTarget ||
            !ApplyPreviewScale(previewSubject, previewTarget))
        {
            RestorePreview();
        }
    }

    private bool ApplyPreviewScale(
        CharacterDisplayScaleSubject subject,
        Transform target)
    {
        if (subject == null || target == null || !TryComputePreview(out _, out float scale, out _))
        {
            return false;
        }

        Vector3 requestedScale = subject.GetDeterministicScaleVector(scale);

        if (target.localScale != requestedScale)
        {
            target.localScale = requestedScale;
        }

        SceneView.RepaintAll();
        return true;
    }

    private void RestorePreview()
    {
        Transform target = previewTarget;
        Vector3 originalScale = previewOriginalScale;

        previewActive = false;
        previewSubject = null;
        previewTarget = null;

        if (target == null)
        {
            return;
        }

        try
        {
            if (target.localScale != originalScale)
            {
                target.localScale = originalScale;
            }

            SceneView.RepaintAll();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private bool TryComputePreview(out float roomLocalY, out float scale, out string source)
    {
        roomLocalY = 0f;
        scale = 1f;
        source = string.Empty;

        if (!TryGetSelectedRoom(out RoomDisplayScaleEntry room) ||
            !TryGetSelectedCalibration(room, out CalibrationDraft calibration))
        {
            return false;
        }

        CharacterDisplayState state = CharacterDisplayState.Normal;
        string subjectRoomId = string.Empty;
        float currentFootY = float.NaN;

        if (selectedSubject != null)
        {
            TryReadSubjectContext(selectedSubject, out subjectRoomId, out currentFootY, out state);
        }

        switch (previewLocation)
        {
            case PreviewLocation.Front:
                roomLocalY = calibration.FrontPreviewPosition.y;
                break;
            case PreviewLocation.Middle:
                roomLocalY = Mathf.Lerp(
                    calibration.FrontPreviewPosition.y,
                    calibration.BackPreviewPosition.y,
                    0.5f);
                break;
            case PreviewLocation.Back:
                roomLocalY = calibration.BackPreviewPosition.y;
                break;
            case PreviewLocation.CurrentRoomLocalFootY:
                if (!IsFinite(currentFootY) ||
                    CharacterDisplayScaleCatalog.NormalizeRoomId(subjectRoomId) !=
                    CharacterDisplayScaleCatalog.NormalizeRoomId(room.RoomId))
                {
                    return false;
                }

                roomLocalY = currentFootY;
                break;
            case PreviewLocation.NormalizedDepth:
                roomLocalY = Mathf.Lerp(
                    calibration.FrontPreviewPosition.y,
                    calibration.BackPreviewPosition.y,
                    normalizedPreviewDepth);
                break;
            default:
                return false;
        }

        if (TryGetAllowedStateOverride(room, state, out scale, out source))
        {
            return true;
        }

        if (!CharacterDisplayScaleCatalog.TryEvaluateCalibration(
                calibration.FrontPreviewPosition,
                calibration.FrontScale,
                calibration.BackPreviewPosition,
                calibration.BackScale,
                calibration.YToScaleCurve,
                roomLocalY,
                out scale))
        {
            return false;
        }

        source = authoringMode == AuthoringMode.RoomDefault
            ? "Room Default"
            : calibration.Enabled
                ? $"{selectedCharacterId} Override"
                : $"{selectedCharacterId} Override Preview (disabled at runtime)";
        return true;
    }

    private static bool TryGetAllowedStateOverride(
        RoomDisplayScaleEntry room,
        CharacterDisplayState state,
        out float scale,
        out string source)
    {
        scale = 1f;
        source = string.Empty;
        string normalizedRoomId = CharacterDisplayScaleCatalog.NormalizeRoomId(room.RoomId);

        if (state == CharacterDisplayState.DrawingRoomSeated &&
            normalizedRoomId == CharacterDisplayScaleCatalog.NormalizeRoomId(
                CharacterDisplayScaleCatalog.DrawingRoomId) &&
            room.StateOverrides.DrawingRoomSeated.Enabled &&
            room.StateOverrides.DrawingRoomSeated.IsConfigured(out _))
        {
            scale = room.StateOverrides.DrawingRoomSeated.Scale;
            source = "Drawing Room Seated Override";
            return true;
        }

        if (state == CharacterDisplayState.DiningRoomSeated &&
            normalizedRoomId == CharacterDisplayScaleCatalog.NormalizeRoomId(
                CharacterDisplayScaleCatalog.DiningRoomId) &&
            room.StateOverrides.DiningRoomSeated.Enabled &&
            room.StateOverrides.DiningRoomSeated.IsConfigured(out _))
        {
            scale = room.StateOverrides.DiningRoomSeated.Scale;
            source = "Dining Room Seated Override";
            return true;
        }

        return false;
    }

    private bool TryGetSelectedRoom(out RoomDisplayScaleEntry room)
    {
        room = null;
        return catalog != null && catalog.TryGetRoom(GetSelectedRoomId(), out room);
    }

    private bool TryGetSelectedCalibration(
        RoomDisplayScaleEntry room,
        out CalibrationDraft calibration)
    {
        calibration = default;

        if (room == null)
        {
            return false;
        }

        if (authoringMode == AuthoringMode.RoomDefault)
        {
            calibration = new CalibrationDraft
            {
                FrontPreviewPosition = room.FrontPreviewPosition,
                FrontScale = room.FrontScale,
                BackPreviewPosition = room.BackPreviewPosition,
                BackScale = room.BackScale,
                YToScaleCurve = CopyCurve(room.YToScaleCurve),
                Enabled = true
            };
            return true;
        }

        if (!room.TryGetCharacterOverride(
                selectedCharacterId,
                out CharacterDisplayScaleOverride scaleOverride))
        {
            return false;
        }

        calibration = new CalibrationDraft
        {
            FrontPreviewPosition = scaleOverride.FrontPreviewPosition,
            FrontScale = scaleOverride.FrontScale,
            BackPreviewPosition = scaleOverride.BackPreviewPosition,
            BackScale = scaleOverride.BackScale,
            YToScaleCurve = CopyCurve(scaleOverride.YToScaleCurve),
            Enabled = scaleOverride.Enabled
        };
        return true;
    }

    private static bool TryReadSubjectContext(
        CharacterDisplayScaleSubject subject,
        out string roomId,
        out float roomLocalFootY,
        out CharacterDisplayState state)
    {
        roomId = string.Empty;
        roomLocalFootY = float.NaN;
        state = CharacterDisplayState.Normal;

        if (subject == null || !subject.TryGetContext(out ICharacterDisplayScaleContext context) || context == null)
        {
            return false;
        }

        roomId = context.CurrentRoomId;
        roomLocalFootY = context.CurrentRoomLocalFootY;
        state = context.CurrentDisplayState;
        return true;
    }

    private void RefreshRoomList()
    {
        string previousRoomId = GetSelectedRoomId();
        SortedDictionary<string, string> roomsByNormalizedId =
            new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (catalog != null)
        {
            IReadOnlyList<RoomDisplayScaleEntry> catalogRooms = catalog.Rooms;

            for (int i = 0; i < catalogRooms.Count; i++)
            {
                RoomDisplayScaleEntry room = catalogRooms[i];
                AddRoomId(roomsByNormalizedId, room != null ? room.RoomId : null);
            }
        }

        RoomContentGroup[] loadedRooms = FindObjectsByType<RoomContentGroup>(
            FindObjectsInactive.Include);

        for (int i = 0; i < loadedRooms.Length; i++)
        {
            RoomContentGroup room = loadedRooms[i];

            if (room != null && room.gameObject.scene.IsValid() && room.gameObject.scene.isLoaded)
            {
                AddRoomId(roomsByNormalizedId, room.RoomName);
            }
        }

        roomIds.Clear();
        roomIds.AddRange(roomsByNormalizedId.Values);
        selectedRoomIndex = 0;

        string previousKey = CharacterDisplayScaleCatalog.NormalizeRoomId(previousRoomId);

        for (int i = 0; i < roomIds.Count; i++)
        {
            if (CharacterDisplayScaleCatalog.NormalizeRoomId(roomIds[i]) == previousKey)
            {
                selectedRoomIndex = i;
                break;
            }
        }
    }

    private static void AddRoomId(IDictionary<string, string> rooms, string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        string cleanRoomId = roomId.Trim();
        string normalizedRoomId = CharacterDisplayScaleCatalog.NormalizeRoomId(cleanRoomId);

        if (!string.IsNullOrEmpty(normalizedRoomId) && !rooms.ContainsKey(normalizedRoomId))
        {
            rooms.Add(normalizedRoomId, cleanRoomId);
        }
    }

    private string GetSelectedRoomId()
    {
        return roomIds.Count == 0
            ? string.Empty
            : roomIds[Mathf.Clamp(selectedRoomIndex, 0, roomIds.Count - 1)];
    }

    private static RoomContentGroup FindLoadedRoom(string roomId)
    {
        string normalizedRoomId = CharacterDisplayScaleCatalog.NormalizeRoomId(roomId);
        RoomContentGroup[] rooms = FindObjectsByType<RoomContentGroup>(
            FindObjectsInactive.Include);

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomContentGroup room = rooms[i];

            if (room != null &&
                room.gameObject.scene.IsValid() &&
                room.gameObject.scene.isLoaded &&
                CharacterDisplayScaleCatalog.NormalizeRoomId(room.RoomName) == normalizedRoomId)
            {
                return room;
            }
        }

        return null;
    }

    private static CharacterDisplayScaleSubject ResolveSelectedSubject()
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            return null;
        }

        CharacterDisplayScaleSubject subject =
            selectedObject.GetComponent<CharacterDisplayScaleSubject>();

        if (subject == null)
        {
            subject = selectedObject.GetComponentInParent<CharacterDisplayScaleSubject>(true);
        }

        if (subject == null)
        {
            subject = selectedObject.GetComponentInChildren<CharacterDisplayScaleSubject>(true);
        }

        return subject;
    }

    private void FindCatalogAsset(bool reportResult)
    {
        CharacterDisplayScaleCatalog found =
            AssetDatabase.LoadAssetAtPath<CharacterDisplayScaleCatalog>(
                CharacterDisplayScaleCatalog.DefaultAssetPath);

        if (found == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterDisplayScaleCatalog");

            if (guids.Length > 0)
            {
                Array.Sort(guids, StringComparer.Ordinal);
                found = AssetDatabase.LoadAssetAtPath<CharacterDisplayScaleCatalog>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

                if (reportResult && guids.Length > 1)
                {
                    SetStatus(
                        $"Found {guids.Length} CharacterDisplayScaleCatalog assets; selected " +
                        $"'{AssetDatabase.GetAssetPath(found)}'.",
                        MessageType.Warning);
                }
            }
        }

        catalog = found;

        if (!reportResult)
        {
            return;
        }

        if (catalog != null)
        {
            SetStatus($"Selected {AssetDatabase.GetAssetPath(catalog)}.", MessageType.Info);
        }
        else
        {
            SetStatus("No CharacterDisplayScaleCatalog asset was found.", MessageType.Warning);
        }
    }

    private void CreateDefaultCatalogAsset()
    {
        CharacterDisplayScaleCatalog existing =
            AssetDatabase.LoadAssetAtPath<CharacterDisplayScaleCatalog>(
                CharacterDisplayScaleCatalog.DefaultAssetPath);

        if (existing != null)
        {
            catalog = existing;
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            SetStatus(
                $"The default catalog already exists at {CharacterDisplayScaleCatalog.DefaultAssetPath}.",
                MessageType.Info);
            return;
        }

        string defaultAssetPath = CharacterDisplayScaleCatalog.DefaultAssetPath;
        int finalSlash = defaultAssetPath.LastIndexOf('/');
        EnsureAssetFolder(finalSlash > 0 ? defaultAssetPath.Substring(0, finalSlash) : "Assets");
        CharacterDisplayScaleCatalog created = CreateInstance<CharacterDisplayScaleCatalog>();
        AssetDatabase.CreateAsset(created, CharacterDisplayScaleCatalog.DefaultAssetPath);
        AssetDatabase.SaveAssetIfDirty(created);
        catalog = created;
        Selection.activeObject = created;
        EditorGUIUtility.PingObject(created);
        SetStatus(
            $"Created {CharacterDisplayScaleCatalog.DefaultAssetPath}.",
            MessageType.Info);
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static AnimationCurve CopyCurve(AnimationCurve source)
    {
        AnimationCurve copy = source != null
            ? new AnimationCurve(source.keys)
            : AnimationCurve.Linear(0f, 0f, 1f, 1f);

        if (source != null)
        {
            copy.preWrapMode = source.preWrapMode;
            copy.postWrapMode = source.postWrapMode;
        }

        return copy;
    }

    private void MarkCatalogDirty()
    {
        if (catalog != null)
        {
            EditorUtility.SetDirty(catalog);
        }
    }

    private void HandleSelectionChanged()
    {
        CharacterDisplayScaleSubject nextSubject = ResolveSelectedSubject();

        if (previewActive && nextSubject != previewSubject)
        {
            RestorePreview();
        }

        selectedSubject = nextSubject;
        Repaint();
    }

    private void HandleBeforeAssemblyReload()
    {
        RestorePreview();
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        RestorePreview();
        Repaint();
    }

    private void HandleUndoRedo()
    {
        RestorePreview();
        RefreshRoomList();
        Repaint();
    }

    private void UnsubscribeEditorCallbacks()
    {
        Selection.selectionChanged -= HandleSelectionChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        Undo.undoRedoPerformed -= HandleUndoRedo;
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
        Repaint();
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
