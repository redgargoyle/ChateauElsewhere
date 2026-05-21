using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class PostProcessSafeCanvasUtility
{
    private const string SafeCanvasName = "Canvas_PostProcessSafeUI";
    private const string BackgroundCanvasName = "Canvas_Background";
    private const int SafeCanvasSortingOrder = 9000;

    public static Canvas GetOrCreateCanvas()
    {
        GameObject canvasObject = GameObject.Find(SafeCanvasName);

        if (canvasObject == null)
        {
            canvasObject = new GameObject(
                SafeCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        }

        int uiLayer = LayerMask.NameToLayer("UI");

        if (uiLayer >= 0)
        {
            canvasObject.layer = uiLayer;
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();

        if (canvas == null)
        {
            canvas = canvasObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SafeCanvasSortingOrder;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();

        if (canvasScaler == null)
        {
            canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        }

        CopyCanvasScalerSettings(canvasScaler, FindSourceCanvasScaler());

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        RectTransform rectTransform = canvasObject.transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
        }

        return canvas;
    }

    public static void MoveToSafeCanvas(Component component)
    {
        if (component == null)
        {
            return;
        }

        Canvas safeCanvas = GetOrCreateCanvas();

        if (safeCanvas == null || component.GetComponentInParent<Canvas>() == safeCanvas)
        {
            return;
        }

        component.transform.SetParent(safeCanvas.transform, false);
        component.transform.SetAsLastSibling();
    }

    private static CanvasScaler FindSourceCanvasScaler()
    {
        GameObject backgroundCanvasObject = GameObject.Find(BackgroundCanvasName);

        if (backgroundCanvasObject != null &&
            backgroundCanvasObject.TryGetComponent(out CanvasScaler backgroundScaler))
        {
            return backgroundScaler;
        }

        return Object.FindObjectOfType<CanvasScaler>(true);
    }

    private static void CopyCanvasScalerSettings(CanvasScaler target, CanvasScaler source)
    {
        target.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        target.referenceResolution = new Vector2(1366f, 768f);
        target.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        target.matchWidthOrHeight = 0.5f;

        if (source == null || source == target)
        {
            return;
        }

        target.uiScaleMode = source.uiScaleMode;
        target.referenceResolution = source.referenceResolution;
        target.screenMatchMode = source.screenMatchMode;
        target.matchWidthOrHeight = source.matchWidthOrHeight;
        target.physicalUnit = source.physicalUnit;
        target.fallbackScreenDPI = source.fallbackScreenDPI;
        target.defaultSpriteDPI = source.defaultSpriteDPI;
        target.dynamicPixelsPerUnit = source.dynamicPixelsPerUnit;
        target.referencePixelsPerUnit = source.referencePixelsPerUnit;
    }
}
