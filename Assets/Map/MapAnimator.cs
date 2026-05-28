using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MapAnimator : MonoBehaviour
{
    public RectTransform mapPanel;
    public Image arrow;
    public float speed = 5.0f;

    public Vector2 hiddenPosition;
    public Vector2 visiblePosition;

    public AudioSource mapOpenSound;
    [Header("Startup")]
    public bool startHidden = true;
    public bool normalizeTriggerScaleOnStart = true;
    public bool useResponsiveLayout = true;
    public Vector2 visibleViewportPosition = new Vector2(0.82f, 0.36f);
    public Vector2 hiddenViewportPosition = new Vector2(0.82f, -0.45f);
    public Vector2 triggerViewportPosition = new Vector2(0.95f, 0.92f);
    public float mapWidthPercent = 0.32f;
    public float minMapScale = 3.25f;
    public float maxMapScale = 6.5f;
    [Header("Keyboard")]
    public bool toggleWithKey = true;
    public KeyCode toggleKey = KeyCode.M;

    private bool mapVisible = false;
    private RectTransform triggerRect;
    private Vector3 originalMapScale = Vector3.one;
    private Vector3 originalTriggerScale = Vector3.one;
    private Vector2 lastParentSize;

    private void Reset()
    {
        mapPanel = GetComponent<RectTransform>();
        mapOpenSound = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (mapPanel == null)
        {
            mapPanel = GetComponent<RectTransform>();
        }

        if (mapOpenSound == null)
        {
            mapOpenSound = GetComponent<AudioSource>();
        }

        if (mapPanel != null)
        {
            originalMapScale = mapPanel.localScale;

            if (startHidden)
            {
                // The editor tools let us drag the map around while placing door
                // triggers, so the scene can easily be saved with the map visible.
                // Play mode should always begin with the map tucked away.
                mapVisible = false;
                mapPanel.anchoredPosition = hiddenPosition;
            }
        }

        if (arrow != null)
        {
            triggerRect = arrow.transform.parent as RectTransform;

            if (triggerRect != null)
            {
                // The trigger is a gameplay affordance, not an edit-mode canvas
                // ruler. Normalize it so saved preview scale does not leak into
                // Play mode and cover the room image.
                originalTriggerScale = normalizeTriggerScaleOnStart ? Vector3.one : triggerRect.localScale;

                if (normalizeTriggerScaleOnStart)
                {
                    triggerRect.localScale = Vector3.one;
                }
            }
        }
    }

    private void Start()
    {
        if (mapPanel == null)
        {
            Debug.LogError("MapAnimator needs a Map Panel RectTransform assigned.", this);
            enabled = false;
            return;
        }

        if (useResponsiveLayout)
        {
            RefreshResponsiveLayout(false);
        }
        else
        {
            visiblePosition = mapPanel.anchoredPosition;
        }

        if (startHidden)
        {
            mapVisible = false;
        }

        SnapMapToCurrentState();

        if (arrow != null)
        {
            arrow.enabled = true;
        }
    }

    private void Update()
    {
        if (toggleWithKey && Input.GetKeyDown(toggleKey))
        {
            ToggleMap();
        }

        if (!useResponsiveLayout || mapPanel == null)
        {
            return;
        }

        RectTransform parent = mapPanel.parent as RectTransform;

        if (parent == null)
        {
            return;
        }

        Vector2 parentSize = parent.rect.size;

        if (Vector2.Distance(parentSize, lastParentSize) > 0.5f)
        {
            RefreshResponsiveLayout(true);
        }
    }

    public void OnArrowEnter()
    {
        if (arrow != null)
        {
            arrow.enabled = false;
        }

        ToggleMap();
    }

    public void OnArrowClick()
    {
        ShowMap();
    }

    public void ToggleMap()
    {
        if (!mapVisible)
        {
            ShowMap();
        }
        else
        {
            HideMap();
        }
    }

    public void OnArrowExit()
    {
        if (arrow != null)
        {
            arrow.enabled = true;
        }
    }
    public void ShowMap()
    {
        if (mapVisible)
        {
            return;
        }

        mapVisible = true;
        StopAllCoroutines();
        if (useResponsiveLayout)
        {
            RefreshResponsiveLayout(false);
        }
        StartCoroutine(SlideTo(visiblePosition));
        if (mapOpenSound != null)
        {
            mapOpenSound.Play();
        }

    }
    public void HideMap()
    {
        if (!mapVisible)
        {
            SnapMapToCurrentState();
            return;
        }

        mapVisible = false;
        StopAllCoroutines();
        if (useResponsiveLayout)
        {
            RefreshResponsiveLayout(false);
        }
        StartCoroutine(SlideTo(hiddenPosition));

        if (mapOpenSound != null)
        {
            mapOpenSound.Play();
        }

    }

    private void SnapMapToCurrentState()
    {
        if (mapPanel == null)
        {
            return;
        }

        mapPanel.anchoredPosition = mapVisible ? visiblePosition : hiddenPosition;
    }

    public IEnumerator SlideTo(Vector2 target)
    {
        if (mapPanel == null)
        {
            yield break;
        }

        while (Vector2.Distance(mapPanel.anchoredPosition, target) > 0.1f)
        {
            mapPanel.anchoredPosition = Vector2.Lerp(mapPanel.anchoredPosition, target, Time.deltaTime * speed);
            yield return null;
        }
        mapPanel.anchoredPosition = target;
    }

    private void RefreshResponsiveLayout(bool snapToState)
    {
        if (mapPanel == null)
        {
            return;
        }

        RectTransform parent = mapPanel.parent as RectTransform;

        if (parent == null)
        {
            return;
        }

        Vector2 parentSize = parent.rect.size;

        if (parentSize.x <= 0f || parentSize.y <= 0f)
        {
            return;
        }

        lastParentSize = parentSize;
        mapPanel.anchorMin = Vector2.zero;
        mapPanel.anchorMax = Vector2.zero;
        mapPanel.pivot = new Vector2(0.5f, 0.5f);

        float baseWidth = Mathf.Max(1f, mapPanel.rect.width);
        float scaleX = Mathf.Clamp((parentSize.x * mapWidthPercent) / baseWidth, minMapScale, maxMapScale);
        float scaleRatio = originalMapScale.x == 0f ? 1f : originalMapScale.y / originalMapScale.x;
        mapPanel.localScale = new Vector3(scaleX, scaleX * scaleRatio, originalMapScale.z == 0f ? 1f : originalMapScale.z);

        visiblePosition = ViewportToAnchoredPosition(parentSize, visibleViewportPosition);
        hiddenPosition = ViewportToAnchoredPosition(parentSize, hiddenViewportPosition);

        if (triggerRect != null)
        {
            triggerRect.anchorMin = Vector2.zero;
            triggerRect.anchorMax = Vector2.zero;
            triggerRect.pivot = new Vector2(0.5f, 0.5f);
            triggerRect.anchoredPosition = ViewportToAnchoredPosition(parentSize, triggerViewportPosition);

            float triggerScale = Mathf.Clamp(parentSize.x / 1366f, 0.75f, 1.25f);
            triggerRect.localScale = new Vector3(originalTriggerScale.x * triggerScale, originalTriggerScale.y, originalTriggerScale.z);
        }

        if (snapToState)
        {
            mapPanel.anchoredPosition = mapVisible ? visiblePosition : hiddenPosition;
        }
    }

    private Vector2 ViewportToAnchoredPosition(Vector2 parentSize, Vector2 viewportPosition)
    {
        return new Vector2(parentSize.x * viewportPosition.x, parentSize.y * viewportPosition.y);
    }

}
