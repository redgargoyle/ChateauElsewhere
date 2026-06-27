using System.IO;
using NUnit.Framework;
using UnityEditor;

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
    public void SubtitleLineBankUsesTwoByFourGuestPortraitSheetSlices()
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

            Assert.That(File.Exists(portraitPath), Is.True, $"Missing generated subtitle portrait slice at {portraitPath}.");
            Assert.That(portraitGuid, Is.Not.Empty, $"Unity should know the generated subtitle portrait slice at {portraitPath}.");
            Assert.That(assetText, Does.Contain($"speakerId: Guest{i + 1}"));
            Assert.That(assetText, Does.Contain($"guid: {portraitGuid}"), $"Guest{i + 1} should use the generated sheet slice {portraitAssetNames[i]}.");
        }
    }

    [Test]
    public void SubtitlePortraitsRemovePlaqueNamesAndFillTheFrame()
    {
        string serviceText = File.ReadAllText(SubtitleServicePath);

        Assert.That(serviceText, Does.Contain("new Vector2(98f, 206f)"), "The portrait frame should match the decorative character-card aspect ratio so no empty frame box shows.");
        Assert.That(serviceText, Does.Contain("RemoveOutline(portraitFrame.gameObject)"), "The portrait should rely on the illustrated gold border instead of an extra UI outline.");
        Assert.That(serviceText, Does.Not.Contain("ApplyOutline(portraitFrame.gameObject"), "The extra portrait-frame outline should not be visible around the illustrated card.");
        Assert.That(serviceText, Does.Not.Contain("new Vector2(112f, 192f)"), "The previous portrait frame still left a visible box around the art.");
        Assert.That(serviceText, Does.Not.Contain("new Vector2(-8f, -8f)"), "The portrait image should fill the frame bounds instead of shrinking inward.");
        Assert.That(serviceText, Does.Not.Contain("new Vector2(140f, 178f)"), "The old portrait frame made the character art too small.");
        Assert.That(serviceText, Does.Not.Contain("new Vector2(-20f, -18f)"), "The old portrait inset made the card sit too far inside the frame.");

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

        foreach (string portraitAssetName in portraitAssetNames)
        {
            string portraitPath = $"{SubtitlePortraitRoot}/{portraitAssetName}";
            TextureImporter importer = AssetImporter.GetAtPath(portraitPath) as TextureImporter;

            Assert.That(importer, Is.Not.Null, $"Missing texture importer for generated subtitle portrait at {portraitPath}.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);

            Assert.That(height, Is.LessThanOrEqualTo(590), $"The generated portrait should crop out the bottom name plaque: {portraitPath}.");
            Assert.That((float)width / height, Is.GreaterThan(0.45f), $"The plaque-free portrait card should be less tall and fill the UI frame better: {portraitPath}.");
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
}
