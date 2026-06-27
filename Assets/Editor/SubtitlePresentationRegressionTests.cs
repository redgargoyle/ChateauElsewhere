using System.IO;
using NUnit.Framework;

public sealed class SubtitlePresentationRegressionTests
{
    private const string SubtitleServicePath = "Assets/Scripts/UI/SubtitleService.cs";
    private const string SubtitleLineBankPath = "Assets/Scripts/UI/SubtitleLineBank.cs";
    private const string SubtitleLineBankAssetPath = "Assets/Resources/UI/SubtitleLineBank.asset";

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
