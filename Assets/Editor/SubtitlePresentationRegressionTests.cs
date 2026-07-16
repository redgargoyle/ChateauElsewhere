using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class SubtitlePresentationRegressionTests
{
    private const string SubtitleServicePath = "Assets/Scripts/UI/SubtitleService.cs";
    private const string SubtitleLineBankPath = "Assets/Scripts/UI/SubtitleLineBank.cs";
    private const string SubtitleLineBankAssetPath = "Assets/Resources/UI/SubtitleLineBank.asset";
    private const string SubtitlePortraitRoot = "Assets/Resources/UI/SubtitlePortraits";

    [Test]
    public void SubtitleServiceUsesSpeakerPortraitCardLayout()
    {
        string serviceText = File.ReadAllText(SubtitleServicePath);
        string bankText = File.ReadAllText(SubtitleLineBankPath);

        Assert.That(serviceText, Does.Contain("PortraitImageName"), "The subtitle panel should include a portrait image area.");
        Assert.That(serviceText, Does.Contain("speakerPortraitImage"), "The service should own and update the active speaker portrait image.");
        Assert.That(serviceText, Does.Contain("TryGetSpeakerPortrait"), "The service should resolve portraits through speaker IDs instead of display-name guessing.");
        Assert.That(serviceText, Does.Contain("speakerId"), "Subtitle queue/display data should carry the stable speaker id through to the UI.");
        Assert.That(bankText, Does.Contain("speakerPortraits"), "The line bank should provide speaker-id portrait bindings.");
        Assert.That(bankText, Does.Contain("TryGetSpeakerPortrait"), "The line bank should expose safe portrait lookup for the subtitle service.");
    }

    [Test]
    public void SubtitlePanelMovesOutOfBottomCenterActionBand()
    {
        string serviceText = File.ReadAllText(SubtitleServicePath);

        Assert.That(serviceText, Does.Contain("panelRect.anchorMin = new Vector2(0f, 1f)"), "Subtitles should anchor to the side/top safe band instead of the bottom center.");
        Assert.That(serviceText, Does.Contain("panelRect.anchorMax = new Vector2(0f, 1f)"), "Subtitles should anchor to the side/top safe band instead of the bottom center.");
        Assert.That(serviceText, Does.Contain("panelRect.pivot = new Vector2(0f, 1f)"), "Side-anchored subtitles should grow from the top-left corner.");
        Assert.That(serviceText, Does.Contain("panelRect.anchoredPosition = new Vector2(32f, -150f)"), "The panel should sit below the top-left HUD and away from the floor action.");
        Assert.That(serviceText, Does.Contain("panelRect.sizeDelta = new Vector2(780f, 225f)"), "The new card should be compact enough to avoid the central play area.");
        Assert.That(serviceText, Does.Not.Contain("panelRect.anchorMin = new Vector2(0.5f, 0f)"), "The old bottom-centered subtitle anchor should be removed.");
        Assert.That(serviceText, Does.Not.Contain("panelRect.sizeDelta = new Vector2(1120f, 126f)"), "The old wide subtitle slab should be removed.");
    }

    [Test]
    public void SubtitleLineBankBindsGuestPortraitsByStableSpeakerId()
    {
        string assetText = File.ReadAllText(SubtitleLineBankAssetPath);

        Assert.That(assetText, Does.Contain("speakerPortraits:"), "The subtitle line bank asset should serialize speaker portrait bindings.");
        Assert.That(assetText, Does.Contain("speakerId: Guest1"));
        Assert.That(assetText, Does.Contain("speakerId: Guest2"));
        Assert.That(assetText, Does.Contain("speakerId: Guest3"));
        Assert.That(assetText, Does.Contain("speakerId: Guest4"));
        Assert.That(assetText, Does.Contain("speakerId: Guest5"));
        Assert.That(assetText, Does.Contain("speakerId: Guest6"));
        Assert.That(assetText, Does.Contain("speakerId: Guest7"));
        Assert.That(assetText, Does.Contain("speakerId: Guest8"));
        Assert.That(assetText, Does.Contain("portrait: {fileID:"), "Each speaker binding should be able to reference a Sprite asset.");
    }

    [Test]
    public void SubtitleLineBankUsesProvidedGuestPortraitCards()
    {
        string assetText = File.ReadAllText(SubtitleLineBankAssetPath);
        string[] portraitAssetNames =
        {
            "Guest1_MissIsoldeWren.png",
            "Guest2_ProfessorLucienVale.png",
            "Guest3_MisterFlorianKnell.png",
            "Guest4_CountessElowenDusk.png",
            "Guest5_BaronHectorGlass.png",
            "Guest6_LadySabineMarrow.png",
            "Guest7_LordAmbroseVeil.png",
            "Guest8_MadameCoralieThread.png"
        };

        for (int i = 0; i < portraitAssetNames.Length; i++)
        {
            string portraitPath = $"{SubtitlePortraitRoot}/{portraitAssetNames[i]}";
            string portraitGuid = AssetDatabase.AssetPathToGUID(portraitPath);

            Assert.That(File.Exists(portraitPath), Is.True, $"Missing provided subtitle portrait card at {portraitPath}.");
            Assert.That(portraitGuid, Is.Not.Empty, $"Unity should know the provided subtitle portrait card at {portraitPath}.");
            Assert.That(assetText, Does.Contain($"speakerId: Guest{i + 1}"));
            Assert.That(assetText, Does.Contain($"guid: {portraitGuid}"), $"Guest{i + 1} should use the provided card {portraitAssetNames[i]}.");
        }
    }

    [Test]
    public void SubtitlePortraitsUseCompleteProvidedCardsWithoutExtraUiOutline()
    {
        string serviceText = File.ReadAllText(SubtitleServicePath);

        Assert.That(serviceText, Does.Contain("new Vector2(98f, 205f)"), "The portrait frame should leave equal ten-pixel top and bottom margins inside the 225-pixel card.");
        Assert.That(serviceText, Does.Contain("RemoveOutline(portraitFrame.gameObject)"), "The portrait should rely on the illustrated gold border instead of an extra UI outline.");
        Assert.That(serviceText, Does.Not.Contain("ApplyOutline(portraitFrame.gameObject"), "The extra portrait-frame outline should not be visible around the illustrated card.");
        Assert.That(serviceText, Does.Not.Contain("new Vector2(112f, 192f)"), "The previous portrait frame still left a visible box around the art.");
        Assert.That(serviceText, Does.Not.Contain("new Vector2(140f, 178f)"), "The old portrait frame made the character art too small.");
        Assert.That(serviceText, Does.Not.Contain("new Vector2(98f, 206f)"), "The previous portrait height left unequal top and bottom margins.");
    }

    [Test]
    public void SubtitlePortraitSlicesMatchTheLatestProvidedCards()
    {
        int[] expectedWidths = { 284, 277, 276, 285, 284, 273, 276, 285 };
        int[] expectedHeights = { 586, 589, 590, 590, 586, 585, 586, 586 };
        string[] portraitPaths = GetGuestPortraitPaths();

        for (int i = 0; i < portraitPaths.Length; i++)
        {
            string portraitPath = portraitPaths[i];
            TextureImporter importer = AssetImporter.GetAtPath(portraitPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, $"Missing portrait importer for {portraitPath}.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Assert.That(width, Is.EqualTo(expectedWidths[i]), $"Guest{i + 1} should use the supplied card without resizing: {portraitPath}.");
            Assert.That(height, Is.EqualTo(expectedHeights[i]), $"Guest{i + 1} should use the supplied card without resizing: {portraitPath}.");
        }
    }

    [Test]
    public void SharedPortraitViewportUsesEvenMarginsAndCentersEveryCompleteCard()
    {
        string[] speakerLineIds =
        {
            "SUB_CH01_BUTLER_WELCOME_001",
            "SUB_CH01_G01_GREETING_001",
            "SUB_CH01_G02_GREETING_001",
            "SUB_CH01_G03_GREETING_001",
            "SUB_CH01_G04_GREETING_001",
            "SUB_CH01_G05_GREETING_001",
            "SUB_CH01_G06_GREETING_001",
            "SUB_CH01_G07_GREETING_001",
            "SUB_CH01_G08_GREETING_001"
        };
        GameObject serviceObject = new GameObject("Test_SubtitleService", typeof(SubtitleService));

        try
        {
            SubtitleService service = serviceObject.GetComponent<SubtitleService>();

            foreach (string lineId in speakerLineIds)
            {
                service.ShowPersistentLine(lineId, "Speaker", "Portrait layout check.");
                Canvas.ForceUpdateCanvases();

                RectTransform panel = GameObject.Find("Panel_Subtitle").GetComponent<RectTransform>();
                RectTransform viewport = GameObject.Find("Frame_SubtitleSpeakerPortrait").GetComponent<RectTransform>();
                RectTransform portrait = GameObject.Find("Image_SubtitleSpeakerPortrait").GetComponent<RectTransform>();
                RectTransform nameplate = GameObject.Find("Image_SubtitleSpeakerNameplate").GetComponent<RectTransform>();
                AspectRatioFitter fitter = portrait.GetComponent<AspectRatioFitter>();

                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
                Canvas.ForceUpdateCanvases();

                float leftMargin = viewport.anchoredPosition.x;
                float topMargin = -viewport.anchoredPosition.y;
                float bottomMargin = panel.rect.height - topMargin - viewport.rect.height;
                float rightMargin = nameplate.anchoredPosition.x - viewport.anchoredPosition.x - viewport.rect.width;

                Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null, $"{lineId} should be clipped to the shared rectangular portrait viewport.");
                Assert.That(leftMargin, Is.EqualTo(topMargin).Within(0.01f), $"{lineId} should have equal left and top margins.");
                Assert.That(bottomMargin, Is.EqualTo(topMargin).Within(0.01f), $"{lineId} should have equal top and bottom margins.");
                Assert.That(rightMargin, Is.EqualTo(leftMargin).Within(0.01f), $"{lineId} should have equal space on both sides of the portrait.");
                Assert.That(fitter, Is.Not.Null, $"{lineId} should use the shared non-distorting portrait fitter.");
                Assert.That(fitter.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.FitInParent));
                AssertVector2(portrait.anchoredPosition, Vector2.zero);
                Assert.That(portrait.rect.width, Is.LessThanOrEqualTo(viewport.rect.width + 0.01f), $"{lineId} should not be horizontally cropped.");
                Assert.That(portrait.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + 0.01f), $"{lineId} should not be vertically cropped.");
            }
        }
        finally
        {
            DestroyDialogueTestObjects(serviceObject);
        }
    }

    [Test]
    public void SubtitleCardKeepsSkipButtonAndReadableTextInsideNewLayout()
    {
        string serviceText = File.ReadAllText(SubtitleServicePath);

        Assert.That(serviceText, Does.Contain("SpeakerNameplateName"), "The speaker name should sit in a decorative nameplate.");
        Assert.That(serviceText, Does.Contain("DividerLineName"), "The card should keep a visual divider between name and dialogue text.");
        Assert.That(serviceText, Does.Contain("TextAlignmentOptions.Left"), "Dialogue text should be left-aligned beside the portrait for faster scanning.");
        Assert.That(serviceText, Does.Contain("skipButton"), "The skippable speech affordance should survive the visual redesign.");
        Assert.That(serviceText, Does.Contain("ConfigureSkipButton"), "Existing skip behavior should remain wired through the subtitle service.");
    }

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

    private static string[] GetGuestPortraitPaths()
    {
        return new[]
        {
            $"{SubtitlePortraitRoot}/Guest1_MissIsoldeWren.png",
            $"{SubtitlePortraitRoot}/Guest2_ProfessorLucienVale.png",
            $"{SubtitlePortraitRoot}/Guest3_MisterFlorianKnell.png",
            $"{SubtitlePortraitRoot}/Guest4_CountessElowenDusk.png",
            $"{SubtitlePortraitRoot}/Guest5_BaronHectorGlass.png",
            $"{SubtitlePortraitRoot}/Guest6_LadySabineMarrow.png",
            $"{SubtitlePortraitRoot}/Guest7_LordAmbroseVeil.png",
            $"{SubtitlePortraitRoot}/Guest8_MadameCoralieThread.png"
        };
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
}
