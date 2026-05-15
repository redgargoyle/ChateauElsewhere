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
    public bool useResponsiveLayout = true;
    public Vector2 visibleViewportPosition = new Vector2(0.82f, 0.36f);
    public Vector2 hiddenViewportPosition = new Vector2(0.82f, -0.45f);
    public Vector2 triggerViewportPosition = new Vector2(0.5f, 0.08f);
    public float mapWidthPercent = 0.32f;
    public float minMapScale = 3.25f;
    public float maxMapScale = 6.5f;

    [Header("Power")]
    public PowerManager powerManager;
    public bool drawPowerWhileOpen = true;
    public string powerDrawId = "Map";
    public float openPowerDrawRate = 0.25f;

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

        if (powerManager == null)
        {
            powerManager = FindObjectOfType<PowerManager>();
        }

        if (mapPanel != null)
        {
            originalMapScale = mapPanel.localScale;
        }

        if (arrow != null)
        {
            triggerRect = arrow.transform.parent as RectTransform;

            if (triggerRect != null)
            {
                originalTriggerScale = triggerRect.localScale;
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
            RefreshResponsiveLayout(true);
        }
        else
        {
            visiblePosition = mapPanel.anchoredPosition;
            mapPanel.anchoredPosition = hiddenPosition;
        }

        if (arrow != null)
        {
            arrow.enabled = true;
        }
    }

    private void Update()
    {
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
        StartCoroutine(SlideTo(visiblePosition));
        if (mapOpenSound != null)
        {
            mapOpenSound.Play();
        }

        AddPowerDraw();
    }
    public void HideMap()
    {
        if (!mapVisible)
        {
            return;
        }

        mapVisible = false;
        StopAllCoroutines();
        StartCoroutine(SlideTo(hiddenPosition));

        if (mapOpenSound != null)
        {
            mapOpenSound.Play();
        }

        RemovePowerDraw();
    }

    private void OnDisable()
    {
        RemovePowerDraw();
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

    private void AddPowerDraw()
    {
        if (!drawPowerWhileOpen || powerManager == null)
        {
            return;
        }

        powerManager.AddDraw(powerDrawId, openPowerDrawRate);
    }

    private void RemovePowerDraw()
    {
        if (powerManager != null)
        {
            powerManager.RemoveDraw(powerDrawId);
        }
    }
}
