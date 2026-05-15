using System.Collections;
using TMPro;
using UnityEngine;

public class WinSequence : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NightManager nightManager;
    [SerializeField] private Animator rollOverAnimator;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private TMP_Text rollOverText;
    [SerializeField] private AudioSource cheering;

    [Header("Timing")]
    [SerializeField] private float preRollHold = 0.5f;
    [SerializeField] private float postRollHold = 0.75f;
    [SerializeField] private float fadeDuration = 2f;

    private Coroutine winRoutine;
    private bool running;

    public void Configure(NightManager manager, CanvasGroup winFadeGroup, TMP_Text winText)
    {
        if (manager != null)
        {
            nightManager = manager;
        }

        if (winFadeGroup != null)
        {
            fadeGroup = winFadeGroup;
        }

        if (winText != null)
        {
            rollOverText = winText;
        }
    }

    public void ResetSequence()
    {
        if (winRoutine != null)
        {
            StopCoroutine(winRoutine);
            winRoutine = null;
        }

        running = false;

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.interactable = false;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.gameObject.SetActive(false);
        }

        if (rollOverText != null)
        {
            rollOverText.gameObject.SetActive(false);
        }

        if (rollOverAnimator != null)
        {
            ResetAnimatorToIdle();
        }
    }

    public void Reset()
    {
        ResetSequence();
    }

    public void PlayWinSequence()
    {
        ResolveReferences();

        if (running)
        {
            return;
        }

        if (fadeGroup == null)
        {
            nightManager?.AdvanceNight();
            return;
        }

        winRoutine = StartCoroutine(WinRoutine());
    }

    private IEnumerator WinRoutine()
    {
        running = true;
        fadeGroup.gameObject.SetActive(true);
        fadeGroup.transform.SetAsLastSibling();
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = true;
        fadeGroup.alpha = 0f;

        if (rollOverText != null)
        {
            rollOverText.text = "6 AM";
            rollOverText.gameObject.SetActive(false);
        }

        yield return Fade(0f, 1f);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, preRollHold));

        if (rollOverText != null)
        {
            rollOverText.gameObject.SetActive(true);
            rollOverText.transform.SetAsLastSibling();
        }

        if (rollOverAnimator != null && HasAnimatorTrigger("Roll"))
        {
            rollOverAnimator.SetTrigger("Roll");
            yield return null;
        }

        if (cheering != null)
        {
            cheering.Play();
        }

        yield return new WaitForSecondsRealtime(GetRollHoldDuration());

        if (rollOverText != null)
        {
            rollOverText.gameObject.SetActive(false);
        }

        nightManager?.EnableBlackBackground();
        yield return Fade(1f, 0f);
        fadeGroup.gameObject.SetActive(false);
        running = false;
        winRoutine = null;
        nightManager?.AdvanceNight();
    }

    private IEnumerator Fade(float start, float end)
    {
        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;
        fadeGroup.alpha = start;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        fadeGroup.alpha = end;
    }

    private void ResolveReferences()
    {
        if (nightManager == null)
        {
            nightManager = GetComponent<NightManager>();
        }

        if (nightManager == null)
        {
            nightManager = FindObjectOfType<NightManager>();
        }

        if (rollOverAnimator == null)
        {
            rollOverAnimator = GetComponentInChildren<Animator>(true);
        }

        if (fadeGroup == null)
        {
            CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>(true);

            foreach (CanvasGroup group in groups)
            {
                if (group.name == "Panel_WinFade")
                {
                    fadeGroup = group;
                    break;
                }
            }
        }

        if (rollOverText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text text in texts)
            {
                if (text.name == "Text_6AM")
                {
                    rollOverText = text;
                    break;
                }
            }
        }

        if (cheering == null)
        {
            cheering = GetComponent<AudioSource>();
        }
    }

    private float GetRollHoldDuration()
    {
        float duration = Mathf.Max(0f, postRollHold);

        if (rollOverAnimator == null || rollOverAnimator.runtimeAnimatorController == null)
        {
            return duration;
        }

        AnimatorStateInfo stateInfo = rollOverAnimator.GetCurrentAnimatorStateInfo(0);
        return Mathf.Max(duration, stateInfo.length);
    }

    private bool HasAnimatorTrigger(string triggerName)
    {
        if (rollOverAnimator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in rollOverAnimator.parameters)
        {
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }

    private void ResetAnimatorToIdle()
    {
        if (rollOverAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        if (HasAnimatorTrigger("Roll"))
        {
            rollOverAnimator.ResetTrigger("Roll");
        }

        if (rollOverAnimator.HasState(0, Animator.StringToHash("Idle")))
        {
            rollOverAnimator.Play("Idle", 0, 0f);
            rollOverAnimator.Update(0f);
        }
    }
}
