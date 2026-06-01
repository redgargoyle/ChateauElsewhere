using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class Chapter2RegressionTests
{
    private const string ChapterManagerPath = "Assets/Scripts/Story/ChapterManager.cs";
    private const string ChapterIntroUIPath = "Assets/Scripts/Story/ChapterIntroUI.cs";
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string Chapter2DirectoryPath = "Assets/_Chateau/Scripts/Chapter/Chapter02";
    private const string Chapter2ControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs";
    private const string Chapter2InteractionHUDPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2InteractionHUD.cs";
    private const string Chapter2MonsterStingerControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2MonsterStingerController.cs";
    private const string Chapter2GuestSearchControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs";
    private const string Chapter2ScriptPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Script.md";

    [Test]
    public void Chapter2ScriptSpecExists()
    {
        Assert.That(File.Exists(Chapter2ScriptPath), Is.True, "Chapter 2 should have a markdown implementation script.");
    }

    [Test]
    public void ChapterTitlesUseChapterLabels()
    {
        string managerText = File.ReadAllText(ChapterManagerPath);
        string introText = File.ReadAllText(ChapterIntroUIPath);
        string chapter2Text = File.ReadAllText(Chapter2ControllerPath);

        Assert.That(managerText, Does.Contain("Chapter1Title = \"Chapter 1\""));
        Assert.That(managerText, Does.Contain("displayedTitle = Chapter1Title"));
        Assert.That(introText, Does.Contain("defaultTitle = \"Chapter 1\""));
        Assert.That(chapter2Text, Does.Contain("chapterTitle = \"Chapter 2\""));
        Assert.That(chapter2Text, Does.Contain("ShowTitle(chapterTitle)"));
    }

    [Test]
    public void Chapter1ArrivalControllerDoesNotOwnChapter2()
    {
        string chapter1Text = File.ReadAllText(Chapter1ArrivalControllerPath);

        Assert.That(chapter1Text, Does.Not.Contain("Chapter2Controller"));
        Assert.That(chapter1Text, Does.Not.Contain("Chapter2GuestSearchController"));
        Assert.That(chapter1Text, Does.Not.Contain("Chapter2MonsterStingerController"));
        Assert.That(chapter1Text, Does.Not.Contain("Chapter2InteractionHUD"));
        Assert.That(chapter1Text, Does.Not.Contain("chapter_03_dinner_pending"));
    }

    [Test]
    public void Chapter2ControllerExistsAndDeclaresExpectedHandoff()
    {
        Assert.That(File.Exists(Chapter2ControllerPath), Is.True, "Chapter 2 should have a dedicated controller.");

        string controllerText = File.ReadAllText(Chapter2ControllerPath);
        string[] expectedPhases =
        {
            "NotStarted",
            "FadeInDrawingRoom",
            "AwaitingAddressPrompt",
            "ButlerSpeech",
            "MonsterStinger",
            "GuestSearch",
            "DiningRoomObjective",
            "DiningRoomReveal",
            "Complete"
        };

        Assert.That(controllerText, Does.Match(@"\bBeginChapter2\s*\("), "Chapter2Controller should expose BeginChapter2.");
        Assert.That(controllerText, Does.Match(@"\bHandleAddressGuestsPrompt\s*\("), "Chapter2Controller should expose the address prompt callback.");
        Assert.That(controllerText, Does.Contain("Chapter2InteractionHUD"), "Chapter2Controller should use the Chapter 2 interaction HUD.");
        Assert.That(controllerText, Does.Contain("Chapter2MonsterStingerController"), "Chapter2Controller should use the Chapter 2 monster stinger controller.");
        Assert.That(controllerText, Does.Contain("Chapter2GuestSearchController"), "Chapter2Controller should use the Chapter 2 guest search controller.");
        Assert.That(controllerText, Does.Match(@"\bRunOpeningSpeechRoutine\s*\("), "Chapter2Controller should run the Butler opening speech from a coroutine.");

        for (int i = 0; i < expectedPhases.Length; i++)
        {
            Assert.That(controllerText, Does.Match(@"\b" + Regex.Escape(expectedPhases[i]) + @"\b"), $"Missing Chapter 2 phase: {expectedPhases[i]}.");
        }
    }

    [Test]
    public void Chapter2OpeningSpeechStopsAtMonsterStinger()
    {
        string controllerText = File.ReadAllText(Chapter2ControllerPath);

        Assert.That(controllerText, Does.Contain("Welcome friends and gentlemen, guests of the evening, Count and Countess of Chantilly—"));
        Assert.That(controllerText, Does.Contain("speechLineSeconds = 1.75f"));
        Assert.That(controllerText, Does.Match(@"(?s)\bRunOpeningSpeechRoutine\s*\([^)]*\)\s*\{.*SetPhase\s*\(\s*Chapter2Phase\.MonsterStinger\s*\)"), "Opening speech should advance to the MonsterStinger phase.");
        Assert.That(controllerText, Does.Contain("A terrible sound cuts through the room..."));
    }

    [Test]
    public void Chapter2MonsterStingerControllerExists()
    {
        Assert.That(File.Exists(Chapter2MonsterStingerControllerPath), Is.True, "Chapter 2 should have a dedicated scripted monster stinger controller.");

        string stingerText = File.ReadAllText(Chapter2MonsterStingerControllerPath);
        Assert.That(stingerText, Does.Contain("DisallowMultipleComponent"));
        Assert.That(stingerText, Does.Contain("AudioSource"));
        Assert.That(stingerText, Does.Match(@"\bBeginStinger\s*\("));
        Assert.That(stingerText, Does.Match(@"\bStopStinger\s*\("));
        Assert.That(stingerText, Does.Contain("runSeconds = 1.0f"));
        Assert.That(stingerText, Does.Contain("freezeSeconds = 2.5f"));
        Assert.That(stingerText, Does.Contain("violinsolo"));
        Assert.That(stingerText, Does.Contain(".loop = loopViolinAudio"));
        Assert.That(stingerText, Does.Contain("drawingRoomId = \"Drawing Room\""));
        Assert.That(stingerText, Does.Contain("maxVisibleSeconds = 7f"));
        Assert.That(stingerText, Does.Contain("OnCurrentRoomChanged"));
        Assert.That(stingerText, Does.Contain("SetActive(false)"));
    }

    [Test]
    public void Chapter2ControllerRunsMonsterStingerBeforeGuestSearch()
    {
        string controllerText = File.ReadAllText(Chapter2ControllerPath);

        Assert.That(controllerText, Does.Match(@"\bRunMonsterStingerRoutine\s*\("));
        Assert.That(controllerText, Does.Match(@"(?s)\bRunMonsterStingerRoutine\s*\([^)]*\)\s*\{.*PlayStinger\s*\(.*SetPhase\s*\(\s*Chapter2Phase\.GuestSearch\s*\)"), "Monster stinger should complete before Chapter 2 enters GuestSearch.");
        Assert.That(controllerText, Does.Contain("Find the guests. Tell them dinner will be served at 7:00 PM sharp."));
    }

    [Test]
    public void Chapter2GuestSearchControllerExistsAndUsesRoomState()
    {
        Assert.That(File.Exists(Chapter2GuestSearchControllerPath), Is.True, "Chapter 2 should have a dedicated guest search controller.");

        string guestSearchText = File.ReadAllText(Chapter2GuestSearchControllerPath);
        Assert.That(guestSearchText, Does.Contain("DisallowMultipleComponent"));
        Assert.That(guestSearchText, Does.Contain("ActorRoomState"));
        Assert.That(guestSearchText, Does.Contain("RoomAnchor"));
        Assert.That(guestSearchText, Does.Contain("ChapterActors_Runtime"));
        Assert.That(guestSearchText, Does.Contain("Ch2_Hide_"));
        Assert.That(guestSearchText, Does.Contain("hideRoomId = \"Ballroom\""));
        Assert.That(guestSearchText, Does.Contain("SetCurrentRoom(GetGuestHideRoomId(guest.hideAnchor))"));
        Assert.That(guestSearchText, Does.Contain("Ch2_DiningSeat_"));
        Assert.That(guestSearchText, Does.Match(@"\bBeginSearch\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bMarkGuestFound\s*\("));
    }

    [Test]
    public void Chapter2InteractionHudExists()
    {
        Assert.That(File.Exists(Chapter2InteractionHUDPath), Is.True, "Chapter 2 should have a dedicated runtime HUD.");

        string hudText = File.ReadAllText(Chapter2InteractionHUDPath);
        Assert.That(hudText, Does.Contain("DisallowMultipleComponent"));
        Assert.That(hudText, Does.Contain("Canvas_Chapter2HUD"));
        Assert.That(hudText, Does.Match(@"\bInitialize\s*\("));
        Assert.That(hudText, Does.Match(@"\bSetObjective\s*\("));
        Assert.That(hudText, Does.Match(@"\bSetPrimaryAction\s*\("));
        Assert.That(hudText, Does.Match(@"\bClearPrimaryAction\s*\("));
    }

    [Test]
    public void ChapterManagerHandsOffToChapter2Controller()
    {
        string managerText = File.ReadAllText(ChapterManagerPath);

        Assert.That(managerText, Does.Contain("Chapter2Controller"), "ChapterManager should reference the Chapter 2 controller.");
        Assert.That(managerText, Does.Contain("Chapter2Id"), "ChapterManager should expose the canonical Chapter 2 id.");
        Assert.That(managerText, Does.Contain("chapter_02_guest_search"), "ChapterManager should normalize Chapter 2 requests to the guest-search chapter id.");
        Assert.That(managerText, Does.Match(@"\.BeginChapter2\s*\(\s*this\s*\)"), "ChapterManager should begin Chapter 2 after the Chapter 1 fade-to-black.");
    }

    [Test]
    public void Chapter2DoesNotUseHeavySystems()
    {
        if (!Directory.Exists(Chapter2DirectoryPath))
        {
            return;
        }

        string[] chapter2Files = Directory.GetFiles(Chapter2DirectoryPath, "*.cs", SearchOption.AllDirectories);
        string[] forbiddenTerms =
        {
            "NavMeshAgent",
            "UnityEngine.AI",
            "ChaseTarget",
            "BehaviorTree",
            "QuestSystem",
            "DialogueEditor",
            "InventorySystem"
        };

        for (int i = 0; i < chapter2Files.Length; i++)
        {
            string fileText = File.ReadAllText(chapter2Files[i]);

            for (int termIndex = 0; termIndex < forbiddenTerms.Length; termIndex++)
            {
                Assert.That(
                    fileText,
                    Does.Not.Contain(forbiddenTerms[termIndex]),
                    $"{chapter2Files[i]} should not use {forbiddenTerms[termIndex]}.");
            }
        }
    }

    [Test]
    public void Chapter2AnchorNamingConventionIsDocumented()
    {
        string scriptText = File.ReadAllText(Chapter2ScriptPath);

        Assert.That(scriptText, Does.Match(Regex.Escape("Ch2_ButlerSpeechSpot")));
        Assert.That(scriptText, Does.Match(Regex.Escape("Ch2_MonsterRunStart")));
        Assert.That(scriptText, Does.Match(Regex.Escape("Ch2_MonsterFreezeTarget")));
        Assert.That(scriptText, Does.Match(Regex.Escape("Ch2_Hide_")));
        Assert.That(scriptText, Does.Match(Regex.Escape("Ch2_DiningSeat_")));
    }
}
