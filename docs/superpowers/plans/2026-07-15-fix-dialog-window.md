# Shared Dialogue Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `SubtitleService` the only spoken-dialogue window renderer and route Chapter 2 conversations and choices through the existing portrait-card layout without text overlap.

**Architecture:** `SubtitleService` keeps its existing card and gains persistent conversation, choice-rail, choice-interactivity, and Skip APIs. `DialogueSpeechService` retains all speech timing and queue ownership. `Chapter2InteractionHUD` loses only its duplicate dialogue controls, while `Chapter2Controller` adapts its existing conversation flow to the shared presenter.

**Tech Stack:** Unity 6000.4.10f1, C#, TextMesh Pro UGUI, Unity UI, NUnit Edit Mode tests.

## Global Constraints

- Keep `Canvas_Subtitles` at a `1920x1080` reference resolution with `Scale With Screen Size`.
- Keep the canonical card at top-left position `(32, -150)` and size `780x225` in reference space.
- Place the choice rail at `(32, -387)` with size `780x48`, using 12-pixel gaps and up to three equal columns.
- Preserve `DialogueSpeechService` queue, interruption, voice, Skip, and guest movement-pause behavior.
- Do not add another canvas, dialogue queue, presenter service, or Chapter 2 copy of the card.
- Keep Chapter 2 objective, status, found-list, primary-action, and clock-strike UI behavior intact.
- Preserve cleanup on room changes, chapter cleanup, service disable, and speech cancellation.

---

## File Map

- Modify `Assets/Scripts/UI/SubtitleService.cs`: own the choice rail and persistent conversation controls alongside the existing card.
- Modify `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs`: route Chapter 2 line, Skip, choices, and cleanup state to `SubtitleService`.
- Modify `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2InteractionHUD.cs`: remove its dialogue-only renderer while retaining all non-dialogue Chapter 2 HUD features.
- Modify `Assets/Editor/SubtitlePresentationRegressionTests.cs`: verify real shared-card geometry, choice behavior, containment, and cleanup.
- Modify `Assets/Editor/Chapter2RegressionTests.cs`: lock Chapter 2 to the shared presenter and reject the removed duplicate panel.

### Task 1: Extend the shared portrait card for persistent conversations and choices

**Files:**
- Modify: `Assets/Scripts/UI/SubtitleService.cs:10-764`
- Test: `Assets/Editor/SubtitlePresentationRegressionTests.cs`

**Interfaces:**
- Consumes: existing `SubtitleService.ShowSpeechLine(string, string, string, bool, Action)` and existing speaker portrait lookup.
- Produces: `ShowConversationLine(string lineId, string speaker, string text)`, `SetConversationChoices(string, Action, string, Action, string, Action)`, `SetConversationChoicesInteractable(bool)`, `SetConversationSkipAction(Action)`, and `ClearConversation()`.

- [ ] **Step 1: Write the failing shared-presenter tests**

Add Unity namespaces and the following tests/helpers to `SubtitlePresentationRegressionTests`:

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Test]
public void SharedConversationUsesCanonicalCardAndSeparateChoiceRail()
{
    GameObject serviceObject = new GameObject("Test_SubtitleService", typeof(SubtitleService));

    try
    {
        SubtitleService service = serviceObject.GetComponent<SubtitleService>();
        service.ShowConversationLine("", "Butler", "Choose how to address the guests.");
        service.SetConversationChoices(
            "Ask supper preference", () => { },
            "Ask drink preference", () => { },
            "Ask smoke preference", () => { });
        service.SetConversationSkipAction(() => { });
        Canvas.ForceUpdateCanvases();

        RectTransform panel = GameObject.Find("Panel_Subtitle").GetComponent<RectTransform>();
        RectTransform rail = GameObject.Find("Rect_SubtitleChoices").GetComponent<RectTransform>();
        RectTransform line = GameObject.Find("Text_SubtitleLine").GetComponent<RectTransform>();
        RectTransform skip = GameObject.Find("Button_SubtitleSkip").GetComponent<RectTransform>();

        AssertVector2(panel.anchoredPosition, new Vector2(32f, -150f));
        AssertVector2(panel.sizeDelta, new Vector2(780f, 225f));
        AssertVector2(rail.anchoredPosition, new Vector2(32f, -387f));
        AssertVector2(rail.sizeDelta, new Vector2(780f, 48f));
        Assert.That(RectTransformOverlaps(line, skip), Is.False);
        Assert.That(GameObject.Find("Button_SubtitleChoice1").activeSelf, Is.True);
        Assert.That(GameObject.Find("Button_SubtitleChoice2").activeSelf, Is.True);
        Assert.That(GameObject.Find("Button_SubtitleChoice3").activeSelf, Is.True);
    }
    finally
    {
        DestroyDialogueTestObjects(serviceObject);
    }
}

[Test]
public void SharedConversationCleanupRemovesCallbacksAndVisibility()
{
    GameObject serviceObject = new GameObject("Test_SubtitleService", typeof(SubtitleService));

    try
    {
        int choiceInvocations = 0;
        int skipInvocations = 0;
        SubtitleService service = serviceObject.GetComponent<SubtitleService>();
        service.ShowConversationLine("", "Butler", "Choose.");
        service.SetConversationChoices("Continue", () => choiceInvocations++);
        service.SetConversationSkipAction(() => skipInvocations++);

        Button choice = GameObject.Find("Button_SubtitleChoice1").GetComponent<Button>();
        Button skip = GameObject.Find("Button_SubtitleSkip").GetComponent<Button>();
        GameObject panel = GameObject.Find("Panel_Subtitle");
        GameObject rail = GameObject.Find("Rect_SubtitleChoices");
        choice.onClick.Invoke();
        skip.onClick.Invoke();
        Assert.That(choiceInvocations, Is.EqualTo(1));
        Assert.That(skipInvocations, Is.EqualTo(1));

        service.ClearConversation();
        choice.onClick.Invoke();
        skip.onClick.Invoke();

        Assert.That(choiceInvocations, Is.EqualTo(1));
        Assert.That(skipInvocations, Is.EqualTo(1));
        Assert.That(panel.activeSelf, Is.False);
        Assert.That(rail.activeSelf, Is.False);
    }
    finally
    {
        DestroyDialogueTestObjects(serviceObject);
    }
}

private static void AssertVector2(Vector2 actual, Vector2 expected)
{
    Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
    Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
}

private static bool RectTransformOverlaps(RectTransform first, RectTransform second)
{
    Vector3[] firstCorners = new Vector3[4];
    Vector3[] secondCorners = new Vector3[4];
    first.GetWorldCorners(firstCorners);
    second.GetWorldCorners(secondCorners);
    Rect firstRect = Rect.MinMaxRect(firstCorners[0].x, firstCorners[0].y, firstCorners[2].x, firstCorners[2].y);
    Rect secondRect = Rect.MinMaxRect(secondCorners[0].x, secondCorners[0].y, secondCorners[2].x, secondCorners[2].y);
    return firstRect.Overlaps(secondRect);
}

private static void DestroyDialogueTestObjects(GameObject serviceObject)
{
    if (serviceObject != null)
    {
        UnityEngine.Object.DestroyImmediate(serviceObject);
    }

    GameObject canvas = GameObject.Find("Canvas_Subtitles");
    if (canvas != null)
    {
        UnityEngine.Object.DestroyImmediate(canvas);
    }

    GameObject eventSystem = GameObject.Find("EventSystem");
    if (eventSystem != null)
    {
        UnityEngine.Object.DestroyImmediate(eventSystem);
    }
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -quit -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testFilter SubtitlePresentationRegressionTests -testResults /tmp/fix-dialog-window-subtitle-red.xml -logFile /tmp/fix-dialog-window-subtitle-red.log
```

Expected: test compilation fails because the five conversation methods do not exist yet.

- [ ] **Step 3: Implement the choice rail and persistent-conversation API**

Add three choice buttons and labels under `Canvas_Subtitles`, not under the Chapter 2 canvas. Use these exact public methods:

```csharp
public void ShowConversationLine(string lineId, string speaker, string text)
{
    ResolveReferences();
    queuedSubtitles.Clear();
    showingPersistentLine = true;

    if (autoHideRoutine != null)
    {
        StopCoroutine(autoHideRoutine);
        autoHideRoutine = null;
    }

    ShowNow(lineId, ResolveSpeakerId(lineId, speaker), speaker, text ?? string.Empty);
}

public void SetConversationChoices(
    string firstLabel,
    Action firstCallback,
    string secondLabel = null,
    Action secondCallback = null,
    string thirdLabel = null,
    Action thirdCallback = null)
{
    ResolveReferences();
    SetConversationChoice(0, firstLabel, firstCallback);
    SetConversationChoice(1, secondLabel, secondCallback);
    SetConversationChoice(2, thirdLabel, thirdCallback);
    LayoutConversationChoices();
}

public void SetConversationChoicesInteractable(bool interactable)
{
    conversationChoicesInteractable = interactable;

    for (int i = 0; i < conversationChoiceButtons.Length; i++)
    {
        Button button = conversationChoiceButtons[i];
        if (button != null && button.gameObject.activeSelf)
        {
            button.interactable = interactable && conversationChoiceCallbacks[i] != null;
        }
    }
}

public void SetConversationSkipAction(Action callback)
{
    ConfigureSkipButton(callback != null, callback);
}

public void ClearConversation()
{
    ClearConversationChoices();
    HideCurrent();
}
```

Create `Rect_SubtitleChoices` as a top-left anchored `RectTransform` at `(32, -387)`, size `780x48`. Create `Button_SubtitleChoice1` through `Button_SubtitleChoice3` inside it, with TMP labels named `Text_SubtitleChoice`. Lay out only active buttons using equal widths and 12-pixel gaps:

```csharp
private void LayoutConversationChoices()
{
    int activeCount = 0;
    for (int i = 0; i < conversationChoiceButtons.Length; i++)
    {
        if (conversationChoiceButtons[i] != null && conversationChoiceButtons[i].gameObject.activeSelf)
        {
            activeCount++;
        }
    }

    conversationChoiceRail.gameObject.SetActive(activeCount > 0);
    if (activeCount == 0)
    {
        return;
    }

    const float gap = 12f;
    float width = (780f - (gap * (activeCount - 1))) / activeCount;
    int visibleIndex = 0;

    for (int i = 0; i < conversationChoiceButtons.Length; i++)
    {
        Button button = conversationChoiceButtons[i];
        if (button == null || !button.gameObject.activeSelf)
        {
            continue;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(visibleIndex * (width + gap), 0f);
        rect.sizeDelta = new Vector2(width, 0f);
        visibleIndex++;
    }
}
```

Configure the speaker as one line with ellipsis, the body as wrapped/auto-sized `18-25`, and choice labels as wrapped/auto-sized `13-18`. `ClearAll`, room-change cleanup, and `OnDisable` must call `ClearConversationChoices()` and clear Skip callbacks.

- [ ] **Step 4: Run the focused test and verify GREEN**

Run the Task 1 command again with result path `/tmp/fix-dialog-window-subtitle-green.xml` and log path `/tmp/fix-dialog-window-subtitle-green.log`.

Expected: all `SubtitlePresentationRegressionTests` pass.

- [ ] **Step 5: Commit the shared presenter**

```bash
git add Assets/Scripts/UI/SubtitleService.cs Assets/Editor/SubtitlePresentationRegressionTests.cs
git commit -m "feat(dialogue): add shared conversation choices"
```

### Task 2: Route Chapter 2 through the shared presenter and remove its duplicate window

**Files:**
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs:180-1325`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2InteractionHUD.cs:1-900`
- Test: `Assets/Editor/Chapter2RegressionTests.cs:560-735`

**Interfaces:**
- Consumes: all five `SubtitleService` conversation methods from Task 1.
- Produces: Chapter 2 dialogue with no `Panel_Chapter2Dialogue`, no Chapter 2 dialogue text/Skip controls, and unchanged public guest-conversation behavior.

- [ ] **Step 1: Write the failing Chapter 2 ownership regression**

Update the Chapter 2 regression assertions to require shared ownership:

```csharp
Assert.That(chapter2Text, Does.Contain("ShowConversationLine"), "Chapter 2 dialogue should render through SubtitleService.");
Assert.That(chapter2Text, Does.Contain("SetConversationChoices"), "Chapter 2 choices should render through SubtitleService.");
Assert.That(holdChoicesBody, Does.Contain("SetConversationSkipAction(service.SkipCurrentSpeech)"), "Interactive dialogue should expose Skip on the shared card.");
Assert.That(holdChoicesBody, Does.Contain("SetConversationChoicesInteractable(false)"), "Choices should remain locked while their prompt is speaking.");
Assert.That(hudText, Does.Not.Contain("Panel_Chapter2Dialogue"), "Chapter 2 must not create a second dialogue panel.");
Assert.That(hudText, Does.Not.Contain("Text_Chapter2DialogueSpeaker"), "Chapter 2 must not own separate speaker text.");
Assert.That(hudText, Does.Not.Contain("Button_Chapter2DialogueSkip"), "Chapter 2 must not own a separate Skip button.");
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -quit -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testFilter Chapter2RegressionTests -testResults /tmp/fix-dialog-window-chapter2-red.xml -logFile /tmp/fix-dialog-window-chapter2-red.log
```

Expected: the new assertions fail because `Chapter2InteractionHUD` still owns `Panel_Chapter2Dialogue` and the controller still calls its dialogue methods.

- [ ] **Step 3: Adapt the Chapter 2 controller**

Keep the public call shape, but route rendering through one private helper:

```csharp
private void ShowGuestConversationInternal(
    string lineId,
    string speaker,
    string line,
    string firstChoice,
    Action firstCallback,
    string secondChoice,
    Action secondCallback,
    string thirdChoice,
    Action thirdCallback)
{
    SubtitleService service = ResolveSubtitleService();
    if (service == null)
    {
        return;
    }

    service.ShowConversationLine(lineId, speaker, line);
    service.SetConversationChoices(
        firstChoice,
        firstCallback,
        secondChoice,
        secondCallback,
        thirdChoice,
        thirdCallback);
    service.SetConversationChoicesInteractable(true);
}

private void SetDialoguePanelSpeechLine(string lineId, string speaker, string text)
{
    ResolveSubtitleService()?.ShowConversationLine(lineId, speaker, text);
}

private void ClearDialoguePanel()
{
    subtitleService?.ClearConversation();
}
```

Have `ShowGuestConversation` call the helper with an empty line ID. Have `ShowGuestConversationWithSubtitle` and `ShowGuestConversationLineWithVoice` call it with their subtitle line ID so portrait lookup remains stable. Replace Chapter 2 calls to `interactionHUD.SetDialogueSkipAction`, `SetDialogueChoicesInteractable`, `SetDialogue`, and `ClearDialogue` with the matching `SubtitleService` method or `ClearDialoguePanel()`.

Keep `DialogueSpeechService` calls on `showSubtitleOverlay: false`; the shared card is persistent during Chapter 2 choices, while `DialogueSpeechService` still owns timing. Pass the line ID through the resolved-line callback:

```csharp
onSpeechLineStarted: (resolvedSpeaker, resolvedText) =>
    SetDialoguePanelSpeechLine(lineId, resolvedSpeaker, resolvedText)
```

- [ ] **Step 4: Remove dialogue rendering from Chapter2InteractionHUD**

Delete the dialogue constants, fields, public dialogue methods, click handlers, and `EnsureUI` block that creates `Panel_Chapter2Dialogue`, speaker/line text, Skip, and choice buttons. Retain the existing objective, status, found list, primary action, clock-strike panel, EventSystem, and all of their callbacks.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the Task 2 command again with `/tmp/fix-dialog-window-chapter2-green.xml` and `/tmp/fix-dialog-window-chapter2-green.log`, then rerun `SubtitlePresentationRegressionTests`.

Expected: both focused suites pass, and the logs contain no compile errors or unexpected exceptions.

- [ ] **Step 6: Commit the Chapter 2 consolidation**

```bash
git add Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2InteractionHUD.cs Assets/Editor/Chapter2RegressionTests.cs
git commit -m "fix(dialogue): unify chapter windows"
```

### Task 3: Verify input routing, speech behavior, cleanup, and final scope

**Files:**
- Modify only if a regression exposes a defect in the Task 1 or Task 2 files.
- Test: `Assets/Editor/DialogueUiInputRoutingRegressionTests.cs`
- Test: `Assets/Editor/DialogueSpeechMovementRegressionTests.cs`

**Interfaces:**
- Consumes: the completed shared presenter and Chapter 2 adapter.
- Produces: evidence that the visual consolidation did not alter speech queueing, movement pauses, interruptions, or click routing.

- [ ] **Step 1: Run all focused dialogue suites**

```bash
for suite in SubtitlePresentationRegressionTests Chapter2RegressionTests DialogueUiInputRoutingRegressionTests DialogueSpeechMovementRegressionTests
do
  /home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -quit -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testFilter "$suite" -testResults "/tmp/fix-dialog-window-$suite.xml" -logFile "/tmp/fix-dialog-window-$suite.log"
done
```

Expected: every focused test passes with zero failures.

- [ ] **Step 2: Run the complete Edit Mode suite**

```bash
/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -quit -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testResults /tmp/fix-dialog-window-all-editmode.xml -logFile /tmp/fix-dialog-window-all-editmode.log
```

Expected: zero failures. Any unrelated baseline failure must be identified by exact test name and separated from this branch's focused results.

- [ ] **Step 3: Perform static and repository checks**

```bash
rg -n "Panel_Chapter2Dialogue|Text_Chapter2DialogueSpeaker|Button_Chapter2DialogueSkip" Assets/_Chateau/Scripts/Chapter/Chapter02
git diff --check
git status --short
```

Expected: the search has no matches; `git diff --check` is clean; only intentional plan or implementation files appear in status.

- [ ] **Step 4: Inspect the rendered layout**

Open Gameplay in Unity, enter a Chapter 1 spoken line and a Chapter 2 hidden-guest conversation, and verify at `1920x1080`, `1366x768`, `1280x720`, and `2560x1080`:

- The portrait card stays at the same top-left position and size in both chapters.
- Speaker name, body, and Skip do not touch or cover each other.
- Choice buttons appear directly below the card and do not cover any Chapter 2 HUD text.
- Choices disable while the prompt speaks, re-enable afterward, and disappear when the conversation ends.
- Chapter 2 objective, status, found list, primary action, and clock strike still appear in their established locations.

- [ ] **Step 5: Commit any verification-driven correction**

If verification required code changes, stage only the affected implementation and regression files and commit:

```bash
git commit -m "test(dialogue): harden shared window layout"
```

If verification required no changes, do not create an empty commit.
