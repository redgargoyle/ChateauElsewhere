using Chateau.Architecture;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chateau.UI
{
    [DisallowMultipleComponent]
    public sealed class GameTimeHUD : UIScreenBase
    {
        [SerializeField] private global::ChapterClock chapterClock;
        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private Shadow clockShadow;

        private void Awake()
        {
            ResolveReferences();
            EnsureUI();
            RefreshClockText();
        }

        private void Update()
        {
            if (clockText != null && chapterClock != null)
            {
                clockText.text = chapterClock.CurrentTimeLabel;
            }
        }

        public void Initialize(global::ChapterClock clock)
        {
            chapterClock = clock != null ? clock : chapterClock;
            ResolveReferences();
            EnsureUI();
            RefreshClockText();
        }

        public bool IsConfiguredFor(global::ChapterClock expectedClock)
        {
            return chapterClock == expectedClock &&
                HasOwnedViewGraph();
        }

        public override void ValidateConfiguration(ValidationReport report)
        {
            base.ValidateConfiguration(report);

            if (chapterClock == null)
            {
                report.AddError("GameTimeHUD requires its serialized ChapterClock.", this);
            }

            if (canvas == null || canvas.gameObject != gameObject)
            {
                report.AddError("GameTimeHUD requires its serialized owned Canvas.", this);
            }

            if (transform.parent == null || transform.parent.GetComponent<GameRoot>() == null)
            {
                report.AddError("GameTimeHUD requires its Canvas to be a direct child of GameRoot.", this);
            }

            if (clockText == null || canvas == null || clockText.transform.parent != canvas.transform)
            {
                report.AddError("GameTimeHUD requires its serialized clock text as a direct child of the owned Canvas.", this);
            }

            if (clockShadow == null ||
                clockText == null ||
                clockShadow.gameObject != clockText.gameObject ||
                clockText.GetComponent<Shadow>() != clockShadow)
            {
                report.AddError("GameTimeHUD requires its serialized clock-text Shadow.", this);
            }
        }

        private bool HasOwnedViewGraph()
        {
            return canvas != null &&
                canvas.gameObject == gameObject &&
                transform.parent != null &&
                transform.parent.GetComponent<GameRoot>() != null &&
                clockText != null &&
                clockText.transform.parent == canvas.transform &&
                clockShadow != null &&
                clockShadow.gameObject == clockText.gameObject &&
                clockText.GetComponent<Shadow>() == clockShadow;
        }

        private void RefreshClockText()
        {
            if (clockText != null && chapterClock != null)
            {
                clockText.text = chapterClock.CurrentTimeLabel;
            }
        }

        private void EnsureUI()
        {
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = GameObject.Find("Canvas_GameTimeHUD");

            if (canvasObject == null)
            {
                canvasObject = new GameObject("Canvas_GameTimeHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            }

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;
            EnsureEventSystem();

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366f, 768f);
            scaler.matchWidthOrHeight = 0.5f;
            HideLegacyTimeSettingsPanel(canvas.transform);

            clockText = CreateText("Text_CurrentGameTime", canvas.transform, 24f, TextAlignmentOptions.BottomLeft);
            RectTransform clockRect = clockText.GetComponent<RectTransform>();
            clockRect.anchorMin = new Vector2(0f, 0f);
            clockRect.anchorMax = new Vector2(0f, 0f);
            clockRect.pivot = new Vector2(0f, 0f);
            clockRect.anchoredPosition = new Vector2(18f, 18f);
            clockRect.sizeDelta = new Vector2(220f, 36f);

            clockText.textWrappingMode = TextWrappingModes.NoWrap;
            clockText.gameObject.transform.SetAsLastSibling();
            clockShadow = clockText.GetComponent<Shadow>();

            if (clockShadow == null)
            {
                clockShadow = clockText.gameObject.AddComponent<Shadow>();
            }

            clockShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            clockShadow.effectDistance = new Vector2(2f, -2f);
            clockShadow.useGraphicAlpha = true;
        }

        private static TMP_Text CreateText(string objectName, Transform parent, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void HideLegacyTimeSettingsPanel(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return;
            }

            Transform panel = canvasTransform.Find("Panel_TimeSettings");

            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }
        }

        private void ResolveReferences()
        {
            if (chapterClock == null)
            {
                chapterClock = FindAnyObjectByType<global::ChapterClock>(FindObjectsInactive.Include);
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
