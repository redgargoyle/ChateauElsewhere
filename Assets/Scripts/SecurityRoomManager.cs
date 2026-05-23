using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SecurityRoomManager : MonoBehaviour
{
    private enum DoorSide
    {
        Left,
        Right
    }

    [Header("Config")]
    [SerializeField] private SecurityRoomConfig config;

    [Header("References")]
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private PowerManager powerManager;
    [SerializeField] private Canvas targetCanvas;

    [Header("Buttons")]
    [SerializeField] private Button leftDoorButton;
    [SerializeField] private Button leftLightButton;
    [SerializeField] private Button rightDoorButton;
    [SerializeField] private Button rightLightButton;
    [SerializeField] private bool createRuntimeHotspots = true;
    [SerializeField] private Color hotspotColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Vector4 leftDoorHotspot = new Vector4(0.015f, 0.49f, 0.11f, 0.18f);
    [SerializeField] private Vector4 leftLightHotspot = new Vector4(0.015f, 0.31f, 0.11f, 0.18f);
    [SerializeField] private Vector4 rightDoorHotspot = new Vector4(0.875f, 0.49f, 0.11f, 0.18f);
    [SerializeField] private Vector4 rightLightHotspot = new Vector4(0.875f, 0.31f, 0.11f, 0.18f);

    [Header("Power")]
    [SerializeField] private string lightPowerDrawId = "SecurityRoom.Light";
    [SerializeField] private float lightPowerDrawRate = 0.35f;

    [Header("Startup")]
    [SerializeField] private bool controlsEnabled = true;
    [SerializeField] private bool redrawInitialFrameOnStart = true;
    [SerializeField] private bool onlyApplyWhenCurrentBackgroundIsSecurityRoomFrame = true;

    private int currentFrameIndex;
    private bool lightsOn;
    private bool leftDoorClosed;
    private bool rightDoorClosed;
    private bool powerOutLocked;
    private bool hasInitializedState;
    private bool buttonsConfigured;
    private bool subscribedToPowerOut;
    private Coroutine doorAnimationRoutine;

    private void Awake()
    {
        ResolveReferences();
        InitializeState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToPowerOut();
        RefreshButtonInteractability();
    }

    private void Start()
    {
        ResolveReferences();

        if (!CanAffectCurrentBackground())
        {
            RefreshButtonInteractability();
            return;
        }

        ConfigureButtons();

        if (redrawInitialFrameOnStart)
        {
            ApplyCurrentFrame();
        }

        RefreshButtonInteractability();
    }

    private void OnDisable()
    {
        UnsubscribeFromPowerOut();
    }

    public void ToggleLeftDoor()
    {
        ToggleDoor(DoorSide.Left);
    }

    public void ToggleRightDoor()
    {
        ToggleDoor(DoorSide.Right);
    }

    public void ToggleLight()
    {
        if (!CanUseControls())
        {
            return;
        }

        SetLightOn(!lightsOn);
    }

    public void HandlePowerOutStarted()
    {
        if (powerOutLocked)
        {
            return;
        }

        powerOutLocked = true;
        SetLightOn(false);
        RemoveDoorDraw(config != null ? config.leftDoor : null);
        RemoveDoorDraw(config != null ? config.rightDoor : null);
        RefreshButtonInteractability();
    }

    public void OpenBothDoorsForPowerOut()
    {
        ResolveReferences();
        InitializeState();

        if (doorAnimationRoutine != null)
        {
            StopCoroutine(doorAnimationRoutine);
        }

        if (!leftDoorClosed && !rightDoorClosed)
        {
            doorAnimationRoutine = null;
            RefreshButtonInteractability();
            return;
        }

        doorAnimationRoutine = StartCoroutine(C_OpenBothDoorsForPowerOut());
    }

    public void SetControlsEnabled(bool enabled)
    {
        controlsEnabled = enabled;
        RefreshButtonInteractability();
    }

    private void ToggleDoor(DoorSide side)
    {
        if (!CanUseControls() || doorAnimationRoutine != null)
        {
            return;
        }

        bool targetClosed = !GetDoorClosed(side);
        doorAnimationRoutine = StartCoroutine(C_PlayDoorSequence(side, targetClosed));
    }

    private IEnumerator C_OpenBothDoorsForPowerOut()
    {
        if (leftDoorClosed)
        {
            yield return PlayDoorFrames(DoorSide.Left, false);
        }

        if (rightDoorClosed)
        {
            yield return PlayDoorFrames(DoorSide.Right, false);
        }

        doorAnimationRoutine = null;
        RefreshButtonInteractability();
    }

    private IEnumerator C_PlayDoorSequence(DoorSide side, bool targetClosed)
    {
        RefreshButtonInteractability();
        yield return PlayDoorFrames(side, targetClosed);
        doorAnimationRoutine = null;
        RefreshButtonInteractability();
    }

    private IEnumerator PlayDoorFrames(DoorSide side, bool targetClosed)
    {
        SecurityRoomDoorTrack track = GetDoorTrack(side);

        if (track == null)
        {
            yield break;
        }

        SetDoorPowerDraw(track, targetClosed);

        int[] frames = targetClosed ? track.closeFrames : track.openFrames;

        if (frames != null && frames.Length > 0)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                SetCurrentFrameIndex(frames[i]);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.001f, track.frameDuration));
            }
        }
        else
        {
            SetCurrentFrameIndex(targetClosed ? track.closedFrameIndex : track.openFrameIndex);
        }

        SetDoorClosed(side, targetClosed);
        SetCurrentFrameIndex(targetClosed ? track.closedFrameIndex : track.openFrameIndex);
    }

    private void SetLightOn(bool enabled)
    {
        if (lightsOn == enabled)
        {
            return;
        }

        lightsOn = enabled;

        if (powerManager != null)
        {
            if (lightsOn && !powerOutLocked && !powerManager.IsPowerOut)
            {
                powerManager.AddDraw(lightPowerDrawId, lightPowerDrawRate);
            }
            else
            {
                powerManager.RemoveDraw(lightPowerDrawId);
            }
        }

        ApplyCurrentFrame();
    }

    private void SetCurrentFrameIndex(int frameIndex)
    {
        currentFrameIndex = config != null ? config.ClampFrameIndex(frameIndex) : Mathf.Max(0, frameIndex);
        ApplyCurrentFrame();
    }

    private void ApplyCurrentFrame()
    {
        if (config == null || !config.HasFrames)
        {
            return;
        }

        ResolveReferences();

        if (!CanAffectCurrentBackground())
        {
            return;
        }

        Texture2D frame = config.GetFrame(currentFrameIndex, lightsOn);

        if (frame != null && cameraManager != null)
        {
            cameraManager.SetRoomBackground(frame);
        }
    }

    private void InitializeState()
    {
        if (hasInitializedState)
        {
            return;
        }

        currentFrameIndex = config != null ? config.ClampFrameIndex(config.initialFrameIndex) : 0;
        leftDoorClosed = config != null && config.leftDoor != null && config.leftDoor.startsClosed;
        rightDoorClosed = config != null && config.rightDoor != null && config.rightDoor.startsClosed;
        hasInitializedState = true;
    }

    private void ConfigureButtons()
    {
        if (buttonsConfigured)
        {
            return;
        }

        leftDoorButton = ConfigureButton(leftDoorButton, "Button_LeftSecurityDoor", leftDoorHotspot, ToggleLeftDoor);
        leftLightButton = ConfigureButton(leftLightButton, "Button_LeftSecurityLight", leftLightHotspot, ToggleLight);
        rightDoorButton = ConfigureButton(rightDoorButton, "Button_RightSecurityDoor", rightDoorHotspot, ToggleRightDoor);
        rightLightButton = ConfigureButton(rightLightButton, "Button_RightSecurityLight", rightLightHotspot, ToggleLight);

        buttonsConfigured = true;
    }

    private Button ConfigureButton(Button button, string buttonName, Vector4 normalizedRect, UnityAction onClick)
    {
        if (button == null && createRuntimeHotspots)
        {
            button = FindOrCreateHotspot(buttonName, normalizedRect);
        }

        if (button == null)
        {
            return null;
        }

        button.onClick.AddListener(onClick);
        return button;
    }

    private Button FindOrCreateHotspot(string buttonName, Vector4 normalizedRect)
    {
        Transform parent = FindTargetCanvasTransform();

        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find(buttonName);
        GameObject buttonObject = existing != null ? existing.gameObject : null;

        if (buttonObject == null)
        {
            buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }

        RectTransform rectTransform = buttonObject.transform as RectTransform;

        if (rectTransform != null)
        {
            ApplyNormalizedRect(rectTransform, normalizedRect);
        }

        Image image = buttonObject.GetComponent<Image>();

        if (image == null)
        {
            image = buttonObject.AddComponent<Image>();
        }

        image.color = hotspotColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();

        if (button == null)
        {
            button = buttonObject.AddComponent<Button>();
        }

        button.targetGraphic = image;
        MoveHotspotAboveBackground(buttonObject.transform);
        return button;
    }

    private void MoveHotspotAboveBackground(Transform hotspot)
    {
        if (hotspot == null)
        {
            return;
        }

        Transform parent = hotspot.parent;
        Transform background = cameraManager != null && cameraManager.cameraBackground != null
            ? cameraManager.cameraBackground.transform
            : null;

        if (parent != null && background != null && background.parent == parent)
        {
            int targetIndex = Mathf.Min(background.GetSiblingIndex() + 1, parent.childCount - 1);
            hotspot.SetSiblingIndex(targetIndex);
            return;
        }

        hotspot.SetAsLastSibling();
    }

    private void ApplyNormalizedRect(RectTransform rectTransform, Vector4 normalizedRect)
    {
        float xMin = Mathf.Clamp01(normalizedRect.x);
        float yMin = Mathf.Clamp01(normalizedRect.y);
        float xMax = Mathf.Clamp01(normalizedRect.x + Mathf.Max(0f, normalizedRect.z));
        float yMax = Mathf.Clamp01(normalizedRect.y + Mathf.Max(0f, normalizedRect.w));

        rectTransform.anchorMin = new Vector2(xMin, yMin);
        rectTransform.anchorMax = new Vector2(Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;
    }

    private void RefreshButtonInteractability()
    {
        bool canInteract = CanUseControls() && doorAnimationRoutine == null;
        SetButtonInteractable(leftDoorButton, canInteract);
        SetButtonInteractable(leftLightButton, canInteract);
        SetButtonInteractable(rightDoorButton, canInteract);
        SetButtonInteractable(rightLightButton, canInteract);
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private bool CanUseControls()
    {
        return controlsEnabled &&
            !powerOutLocked &&
            CanAffectCurrentBackground() &&
            (powerManager == null || !powerManager.IsPowerOut);
    }

    private bool CanAffectCurrentBackground()
    {
        if (!onlyApplyWhenCurrentBackgroundIsSecurityRoomFrame)
        {
            return true;
        }

        if (config == null)
        {
            return false;
        }

        ResolveReferences();

        Texture currentTexture = cameraManager != null && cameraManager.cameraBackground != null
            ? cameraManager.cameraBackground.texture
            : null;

        return IsSecurityRoomTexture(currentTexture);
    }

    private bool IsSecurityRoomTexture(Texture texture)
    {
        if (texture == null || config == null)
        {
            return false;
        }

        if (texture == config.fallbackFrame)
        {
            return true;
        }

        return ContainsTexture(config.lightOffFrames, texture) || ContainsTexture(config.lightOnFrames, texture);
    }

    private static bool ContainsTexture(Texture2D[] frames, Texture texture)
    {
        if (frames == null)
        {
            return false;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == texture)
            {
                return true;
            }
        }

        return false;
    }

    private void SetDoorClosed(DoorSide side, bool closed)
    {
        if (side == DoorSide.Left)
        {
            leftDoorClosed = closed;
        }
        else
        {
            rightDoorClosed = closed;
        }
    }

    private bool GetDoorClosed(DoorSide side)
    {
        return side == DoorSide.Left ? leftDoorClosed : rightDoorClosed;
    }

    private SecurityRoomDoorTrack GetDoorTrack(DoorSide side)
    {
        if (config == null)
        {
            return null;
        }

        return side == DoorSide.Left ? config.leftDoor : config.rightDoor;
    }

    private void SetDoorPowerDraw(SecurityRoomDoorTrack track, bool enabled)
    {
        if (track == null || powerManager == null)
        {
            return;
        }

        if (enabled && !powerOutLocked && !powerManager.IsPowerOut)
        {
            powerManager.AddDraw(track.powerDrawId, track.powerDrawRate);
        }
        else
        {
            powerManager.RemoveDraw(track.powerDrawId);
        }
    }

    private void RemoveDoorDraw(SecurityRoomDoorTrack track)
    {
        if (track != null && powerManager != null)
        {
            powerManager.RemoveDraw(track.powerDrawId);
        }
    }

    private void ResolveReferences()
    {
        if (cameraManager == null)
        {
            cameraManager = FindAnyObjectByType<CameraManager>();
        }

        if (powerManager == null)
        {
            powerManager = FindAnyObjectByType<PowerManager>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = FindTargetCanvas();
        }
    }

    private void SubscribeToPowerOut()
    {
        if (subscribedToPowerOut || powerManager == null)
        {
            return;
        }

        powerManager.OnPowerOut += HandlePowerOutStarted;
        subscribedToPowerOut = true;
    }

    private void UnsubscribeFromPowerOut()
    {
        if (!subscribedToPowerOut || powerManager == null)
        {
            subscribedToPowerOut = false;
            return;
        }

        powerManager.OnPowerOut -= HandlePowerOutStarted;
        subscribedToPowerOut = false;
    }

    private Canvas FindTargetCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);

        foreach (Canvas canvas in canvases)
        {
            if (canvas.name == "Canvas_Background")
            {
                return canvas;
            }
        }

        foreach (Canvas canvas in canvases)
        {
            if (canvas.name == "Canvas_NightManager")
            {
                return canvas;
            }
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }

    private Transform FindTargetCanvasTransform()
    {
        ResolveReferences();
        return targetCanvas != null ? targetCanvas.transform : null;
    }
}
