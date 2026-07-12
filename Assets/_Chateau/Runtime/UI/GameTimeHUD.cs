using Chateau.Architecture;
using TMPro;
using UnityEngine;
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
            RefreshClockText();
        }

        private void Update()
        {
            if (clockText != null && chapterClock != null)
            {
                clockText.text = chapterClock.CurrentTimeLabel;
            }
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
    }
}
