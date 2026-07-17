using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CharacterPresentationOwnershipTests
{
    private static readonly string[] DeletedRuntimeTypes =
    {
        "GuestRoomScaleCalibration",
        "GuestRoomScaleApplier",
        "GuestScaleParticipant",
        "GuestRoomStageScaleUtility",
        "RoomProjectedEntity",
        "RoomPerspectiveProfile",
        "CharacterVisualProfile"
    };

    private static readonly string[] DeletedEditorTypes =
    {
        "ButlerRoomScaleCalibrationWindow",
        "GuestRoomScaleMasterWindow",
        "GuestScaleAudit",
        "RoomProjectionCalibrationWindow",
        "RoomPerspectiveProfileEditor",
        "RoomProjectedEntityEditor"
    };

    private static readonly string[] DeletedLegacyGuids =
    {
        "31d79ef7452a4c5288644569bd958a60",
        "2d396ad445bc46b9a6acb3ac62291ef0",
        "b099f2b1c3494d8fa900d71915c16f31",
        "c209e3f5ef8c464db5163927439bd6a4",
        "361e3658088b41ab98d330ae6457640b",
        "e43aae3b108144478b67cf2bce6ce997",
        "9d7c5206bdd145f4bdd4426f7ccc37bd"
    };

    // CharacterAnimationDisplay is the sole body-size writer, and it may only scale
    // its dedicated visual child. Every other production assignment under Assets is
    // reviewed here as an exact file + normalized statement + occurrence count for a
    // UI, prop, environment, VFX, camera, room-stage, or other non-body target.
    private static readonly Dictionary<string, Dictionary<string, int>> AllowedCharacterDisplayScaleAssignments =
        new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal)
        {
            ["Assets/Scripts/Characters/CharacterAnimationDisplay.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["animationDisplay.localScale = requestedScale;"] = 1
            }
        };

    private static readonly Dictionary<string, Dictionary<string, int>> AllowedNonBodyScaleAssignments =
        new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal)
        {
            ["Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["coatObject.transform.localScale = Vector3.one;"] = 1,
                ["spriteRenderer.transform.localScale = assignedScale;"] = 1,
                ["spriteRenderer.transform.localScale = AssignedCoatFallbackScale;"] = 1
            },
            ["Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["targetTransform.localScale = Vector3.one;"] = 1
            },
            ["Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2InteractionHUD.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rect.localScale = Vector3.one;"] = 2
            },
            ["Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2MonsterStingerController.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["placeholder.transform.localScale = new Vector3(0.65f, 1.45f, 0.65f);"] = 1
            },
            ["Assets/Map/CameraManager.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["target.localScale = shakeBaseScale * Mathf.Lerp(1f, safeZoom, strength);"] = 1,
                ["activeShakeTarget.localScale = shakeBaseScale;"] = 1,
                ["canvasRect.localScale = Vector3.one;"] = 2,
                ["rectTransform.localScale = Vector3.one;"] = 1,
                ["activeRoomStage.localScale = new Vector3(stageScale, stageScale, 1f);"] = 1,
                ["backgroundRect.localScale = Vector3.one;"] = 1
            },
            ["Assets/Scripts/Lighting/PostProcessBypassCamera.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["transform.localScale = Vector3.one;"] = 1
            },
            ["Assets/Scripts/Lighting/RoomLightOverlay.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rectTransform.localScale = Vector3.one;"] = 1,
                ["rectTransform.localScale = new Vector3( authoredLocalScale.x * animationScale.x, authoredLocalScale.y * animationScale.y, authoredLocalScale.z);"] = 1
            },
            ["Assets/Scripts/Lighting/RoomLightingController.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["root.localScale = Vector3.one;"] = 1,
                ["rect.localScale = Vector3.one;"] = 1
            },
            ["Assets/Scripts/MainMenuController.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["backgroundRect.localScale = Vector3.one;"] = 1,
                ["textRect.localScale = Vector3.one;"] = 1,
                ["labelRect.localScale = Vector3.one;"] = 1,
                ["backdropRect.localScale = Vector3.one;"] = 1,
                ["audioSettingsPanel.localScale = Vector3.one * modalScale;"] = 1,
                ["transform.localScale = Vector3.one;"] = 1,
                ["canvasRect.localScale = Vector3.one;"] = 1,
                ["menuPanel.localScale = Vector3.one;"] = 1,
                ["audioSettingsPanel.localScale = Vector3.one * Mathf.Clamp(layoutScale, 0.72f, 1f);"] = 1,
                ["rectTransform.localScale = Vector3.one;"] = 2
            },
            ["Assets/Scripts/Navigation/DoorPromptSequenceController.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rectTransform.localScale = Vector3.one;"] = 1
            },
            ["Assets/Scripts/Navigation/RoomNavigationManager.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rectTransform.localScale = Vector3.one;"] = 1,
                ["rootRect.localScale = Vector3.one;"] = 1,
                ["canvasRect.localScale = Vector3.one;"] = 1
            },
            ["Assets/Scripts/Oddities/OdditySpriteAnimator.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["transform.localScale = baseScale * pulse;"] = 1
            },
            ["Assets/Scripts/StaticNoisePlayer.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rectTransform.localScale = Vector3.one;"] = 1
            },
            ["Assets/Scripts/Story/ChapterIntroUI.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rectTransform.localScale = Vector3.one;"] = 3
            },
            ["Assets/Scripts/UI/PostProcessSafeCanvasUtility.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rectTransform.localScale = Vector3.one;"] = 1
            },
            ["Assets/Scripts/UI/RuntimeSettingsMenu.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rectTransform.localScale = Vector3.one;"] = 1,
                ["rootRect.localScale = Vector3.one;"] = 1
            },
            ["Assets/Scripts/UI/SpeakingCharacterIndicator.cs"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["bubbleRenderer.transform.localScale = new Vector3(scale, scale, 1f);"] = 1
            }
        };

    private static readonly Dictionary<string, float> PanicCharacterWorldHeights =
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["Lady"] = 2.900000058f,
            ["ButlerGuest"] = 2.910104743f,
            ["MisterFlorianKnell"] = 2.97f,
            ["CountessElowenDusk"] = 2.97f,
            ["BaronHectorGlass"] = 2.97f,
            ["LadySabineMarrow"] = 2.97f,
            ["LordAmbroseVeil"] = 2.872799943f,
            ["MadameCoralieThread"] = 2.829641005f
        };

    private static readonly string[] ForbiddenPresentationTokens =
    {
        "ApplyPerspectiveScale",
        "SetPerspectiveScaleEnabled",
        "SetScaleWithRoomStageMotion",
        "authoredPerspectiveScaleReference",
        "butlerCalibrationBaseLocalScale",
        "roomGuestScaleMultiplier",
        "referenceRoomStageScale",
        "ApplySpriteScale",
        "RestoreOriginalLocalScale",
        "CaptureOriginalSpriteLocalSize",
        "GetSpriteScaleMultiplier"
    };

    [Test]
    public void DeletedLegacyTypesAndSerializedGuidsDoNotReappear()
    {
        string[] productionSources = GetProductionSourcePaths();

        foreach (string path in productionSources)
        {
            string text = File.ReadAllText(path);

            foreach (string typeName in DeletedRuntimeTypes)
            {
                Assert.That(
                    Regex.IsMatch(text, $@"\b{Regex.Escape(typeName)}\b"),
                    Is.False,
                    $"Deleted runtime type '{typeName}' reappeared in {path}.");
            }
        }

        string thisTestPath = "Assets/Editor/CharacterPresentationOwnershipTests.cs";
        string[] allSources = GetFiles("Assets", "*.cs");

        foreach (string path in allSources.Where(path => !string.Equals(path, thisTestPath, StringComparison.Ordinal)))
        {
            string text = File.ReadAllText(path);

            foreach (string typeName in DeletedRuntimeTypes.Concat(DeletedEditorTypes))
            {
                Assert.That(
                    Regex.IsMatch(text, $@"\b(?:class|struct|interface)\s+{Regex.Escape(typeName)}\b"),
                    Is.False,
                    $"Deleted type declaration '{typeName}' reappeared in {path}.");
            }
        }

        foreach (string path in GetSerializedAssetPaths())
        {
            string text = File.ReadAllText(path);

            foreach (string typeName in DeletedRuntimeTypes)
            {
                Assert.That(text, Does.Not.Contain(typeName), $"Deleted component/data type '{typeName}' remains in {path}.");
            }

            foreach (string guid in DeletedLegacyGuids)
            {
                Assert.That(text, Does.Not.Contain(guid), $"Deleted legacy GUID '{guid}' remains in {path}.");
            }
        }
    }

    [Test]
    public void CharacterBodyScaleHasExactlyOneDisplayOnlyRuntimeWriter()
    {
        Regex assignment = new Regex(
            @"(?m)^[ \t]*(?!//)(?<statement>[^;\r\n]*?\.\s*localScale\s*(?:\+=|-=|\*=|/=|=(?!=))\s*[\s\S]*?;)",
            RegexOptions.Compiled);
        Dictionary<string, Dictionary<string, int>> seenAllowlist = AllowedNonBodyScaleAssignments.ToDictionary(
            pair => pair.Key,
            _ => new Dictionary<string, int>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, int>> seenDisplayAllowlist = AllowedCharacterDisplayScaleAssignments.ToDictionary(
            pair => pair.Key,
            _ => new Dictionary<string, int>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (string path in GetProductionSourcePaths())
        {
            string text = File.ReadAllText(path);

            foreach (Match match in assignment.Matches(text))
            {
                string statement = NormalizeStatement(match.Groups["statement"].Value);
                int lineNumber = GetLineNumber(text, match.Index);

                bool isAllowedNonBodyAssignment =
                    AllowedNonBodyScaleAssignments.TryGetValue(path, out Dictionary<string, int> allowedAssignments) &&
                    allowedAssignments.ContainsKey(statement);
                bool isAllowedDisplayAssignment =
                    AllowedCharacterDisplayScaleAssignments.TryGetValue(path, out Dictionary<string, int> displayAssignments) &&
                    displayAssignments.ContainsKey(statement);

                Assert.That(
                    isAllowedNonBodyAssignment || isAllowedDisplayAssignment,
                    Is.True,
                    $"Character presentation owner writes localScale at {path}:{lineNumber}: {statement}");

                Dictionary<string, int> seenAssignments = isAllowedDisplayAssignment
                    ? seenDisplayAllowlist[path]
                    : seenAllowlist[path];
                seenAssignments.TryGetValue(statement, out int seenCount);
                seenAssignments[statement] = seenCount + 1;
            }
        }

        foreach (KeyValuePair<string, Dictionary<string, int>> fileAllowlist in AllowedCharacterDisplayScaleAssignments)
        {
            foreach (KeyValuePair<string, int> expectedAssignment in fileAllowlist.Value)
            {
                seenDisplayAllowlist[fileAllowlist.Key].TryGetValue(expectedAssignment.Key, out int actualCount);
                Assert.That(
                    actualCount,
                    Is.EqualTo(expectedAssignment.Value),
                    $"The sole character display scale writer drifted in {fileAllowlist.Key}: " +
                    $"expected {expectedAssignment.Value} occurrence(s), found {actualCount}: {expectedAssignment.Key}");
            }
        }

        foreach (KeyValuePair<string, Dictionary<string, int>> fileAllowlist in AllowedNonBodyScaleAssignments)
        {
            foreach (KeyValuePair<string, int> expectedAssignment in fileAllowlist.Value)
            {
                seenAllowlist[fileAllowlist.Key].TryGetValue(expectedAssignment.Key, out int actualCount);
                Assert.That(
                    actualCount,
                    Is.EqualTo(expectedAssignment.Value),
                    $"Documented non-body scale allowlist drifted in {fileAllowlist.Key}: " +
                    $"expected {expectedAssignment.Value} occurrence(s), found {actualCount}: {expectedAssignment.Key}");
            }
        }
    }

    [Test]
    public void OldScaleAndProjectionInfrastructureCannotBeRecreatedAtRuntime()
    {
        foreach (string path in GetProductionSourcePaths())
        {
            string text = File.ReadAllText(path);

            foreach (string token in ForbiddenPresentationTokens)
            {
                Assert.That(text, Does.Not.Contain(token), $"Forbidden presentation token '{token}' reappeared in {path}.");
            }
        }

        string walkerText = File.ReadAllText("Assets/Scripts/Characters/RoomPersonWalker2D.cs");
        Assert.That(walkerText, Does.Not.Contain("nearTint"));
        Assert.That(walkerText, Does.Not.Contain("farTint"));
        Assert.That(walkerText, Does.Not.Contain("targetGraphic.color"));
        Assert.That(walkerText, Does.Not.Contain("shadowScale"));

        string snapshotFileName = "LegacyCharacterScaleSnapshot.json";

        foreach (string path in GetProductionSourcePaths())
        {
            Assert.That(File.ReadAllText(path), Does.Not.Contain(snapshotFileName), $"Runtime code must not load the reference-only migration snapshot: {path}");
        }
    }

    [Test]
    public void CharacterAnimationClipsDoNotAnimateTransformScale()
    {
        string[] animationClips = GetFiles("Assets", "*.anim");
        Assert.That(animationClips, Is.Not.Empty, "No authored character animation clips were found.");

        Regex emptyScaleCurves = new Regex(@"(?m)^\s*m_ScaleCurves:\s*\[\]\s*$", RegexOptions.Compiled);
        Regex localScaleBinding = new Regex(
            @"(?m)^\s*attribute:\s*(?:m_LocalScale|localScale)(?:\.|\s|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        foreach (string path in animationClips)
        {
            string text = File.ReadAllText(path);
            Assert.That(
                emptyScaleCurves.IsMatch(text),
                Is.True,
                $"Animation clip has a non-empty or malformed transform scale curve: {path}");
            Assert.That(
                localScaleBinding.IsMatch(text),
                Is.False,
                $"Animation clip has a transform-scale binding outside m_ScaleCurves: {path}");
        }
    }

    [Test]
    public void PanicSpriteLibraryUsesNormalizedAuthoredWorldHeightsAndBottomCenterPivots()
    {
        Chapter2PanicAnimationLibrary library =
            Resources.Load<Chapter2PanicAnimationLibrary>(Chapter2PanicAnimationLibrary.ResourcesPath);

        Assert.That(library, Is.Not.Null, $"Missing Resources/{Chapter2PanicAnimationLibrary.ResourcesPath} panic library.");
        Assert.That(library.Characters, Has.Length.EqualTo(Chapter2PanicRoster.CharacterIds.Length));
        Assert.That(library.HasCompleteRoster(out string rosterReport), Is.True, rosterReport);

        int slotCount = 0;

        foreach (string characterId in Chapter2PanicRoster.CharacterIds)
        {
            Assert.That(library.TryGetCharacter(characterId, out Chapter2PanicCharacterAnimation animation), Is.True);
            Assert.That(PanicCharacterWorldHeights.TryGetValue(characterId, out float expectedHeight), Is.True);

            foreach ((string actionId, Sprite[] frames, int expectedCount) in GetPanicFrameSets(animation))
            {
                Assert.That(frames, Is.Not.Null, $"{characterId}/{actionId} has a null frame array.");
                Assert.That(frames, Has.Length.EqualTo(expectedCount), $"{characterId}/{actionId} frame count drifted.");

                for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                {
                    Sprite sprite = frames[frameIndex];
                    string slot = $"{characterId}/{actionId}[{frameIndex}]";
                    Assert.That(sprite, Is.Not.Null, $"{slot} has a null sprite reference.");
                    Assert.That(sprite.bounds.size.y, Is.EqualTo(expectedHeight).Within(0.002f), $"{slot} world height drifted.");

                    float normalizedPivotX = sprite.pivot.x / sprite.rect.width;
                    float normalizedPivotY = sprite.pivot.y / sprite.rect.height;
                    Assert.That(normalizedPivotX, Is.EqualTo(0.5f).Within(0.001f), $"{slot} pivot is not horizontally centered.");
                    Assert.That(normalizedPivotY, Is.EqualTo(0f).Within(0.001f), $"{slot} pivot is not bottom-aligned.");
                    slotCount++;
                }
            }
        }

        Assert.That(slotCount, Is.EqualTo(8 * 28), "The panic library must retain all 224 authored sprite slots.");
    }

    [Test]
    public void DrawingRoomAndDiningRoomPosePlacementSignalsRemain()
    {
        string chapter1 = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs");
        string chapter2Search = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs");

        Assert.That(chapter1, Does.Contain("guest.ActorState.SetSeated(!ShouldUseStandingDrawingRoomPose(guest))"));
        Assert.That(chapter1, Does.Contain("guest.GuestIndex == 2"), "Guest 3 must remain standing in the Drawing Room.");
        Assert.That(chapter1, Does.Contain("guest.GuestIndex == 4"), "Guest 5 must remain standing in the Drawing Room.");
        Assert.That(chapter1, Does.Contain("guest.GuestIndex == 6"), "Guest 7 must remain standing in the Drawing Room.");
        Assert.That(chapter1, Does.Contain("BindGuestToRoomStagePoint"), "Authored room-anchor binding must remain.");

        Assert.That(chapter2Search, Does.Contain("guest.actorState.PlaceAt(diningSeat.transform)"));
        Assert.That(chapter2Search, Does.Contain("guest.actorState.SetSeated(true)"));
        Assert.That(chapter2Search, Does.Contain("ActivateForGuest(guest.actorState, diningSeat)"));
        Assert.That(chapter2Search, Does.Contain("guest.actorState.PlaceAt(guest.hideAnchor.transform)"));
    }

    [Test]
    public void SerializedMonoBehavioursResolveToScripts()
    {
        Regex monoBehaviourBlock = new Regex(
            @"(?ms)^--- !u!114 &(?<fileId>-?\d+)(?: stripped)?\r?\nMonoBehaviour:\r?\n(?<body>.*?)(?=^--- !u!|\z)",
            RegexOptions.Compiled);
        Regex scriptReference = new Regex(
            @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*(?<guid>[a-f0-9]{32}),\s*type:\s*3\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex editorClassIdentifier = new Regex(
            @"(?m)^\s*m_EditorClassIdentifier:\s*(?<identifier>[^\r\n]*)$",
            RegexOptions.Compiled);

        foreach (string path in GetSerializedMonoBehaviourGuardPaths())
        {
            string text = File.ReadAllText(path);
            Assert.That(text, Does.Not.Match(@"m_Script:\s*\{fileID:\s*0(?:,|\})"), $"Missing-script component remains in {path}.");

            foreach (Match blockMatch in monoBehaviourBlock.Matches(text))
            {
                string body = blockMatch.Groups["body"].Value;
                Match scriptMatch = scriptReference.Match(body);
                int lineNumber = GetLineNumber(text, blockMatch.Index);

                Assert.That(
                    scriptMatch.Success,
                    Is.True,
                    $"MonoBehaviour at {path}:{lineNumber} has no valid script reference.");

                string guid = scriptMatch.Groups["guid"].Value;
                string resolvedAssetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(resolvedAssetPath))
                {
                    continue;
                }

                Match identifierMatch = editorClassIdentifier.Match(body);
                string identifier = identifierMatch.Success
                    ? identifierMatch.Groups["identifier"].Value.Trim()
                    : string.Empty;

                Assert.That(
                    ResolvesExternalEditorClassIdentifier(identifier),
                    Is.True,
                    $"Serialized MonoBehaviour GUID '{guid}' does not resolve at {path}:{lineNumber}, " +
                    $"and external editor class identifier '{identifier}' could not be loaded.");
            }
        }
    }

    private static IEnumerable<(string actionId, Sprite[] frames, int expectedCount)> GetPanicFrameSets(
        Chapter2PanicCharacterAnimation animation)
    {
        yield return ("panic_hands_up", animation.PanicHandsUp, 4);
        yield return ("panic_pop", animation.PanicPop, 8);
        yield return ("panic_run_down", animation.PanicRunDown, 4);
        yield return ("panic_run_left", animation.PanicRunLeft, 4);
        yield return ("panic_run_right", animation.PanicRunRight, 4);
        yield return ("panic_run_up", animation.PanicRunUp, 4);
    }

    private static bool ResolvesExternalEditorClassIdentifier(string identifier)
    {
        string[] parts = identifier.Split(new[] { "::" }, 2, StringSplitOptions.None);

        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        string assemblyName = parts[0].Trim();
        string typeName = parts[1].Trim();

        // Project scripts must keep a valid MonoScript GUID. This fallback exists only
        // for package/built-in assemblies whose GUID is not represented in AssetDatabase.
        if (assemblyName.StartsWith("Assembly-CSharp", StringComparison.Ordinal))
        {
            return false;
        }

        Type resolvedType = Type.GetType($"{typeName}, {assemblyName}", false);

        if (resolvedType != null)
        {
            return true;
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
            .Any(assembly => assembly.GetType(typeName, false) != null);
    }

    private static string[] GetProductionSourcePaths()
    {
        return GetFiles("Assets", "*.cs")
            .Where(path => !path.StartsWith("Assets/Editor/", StringComparison.Ordinal))
            .Where(path => path.IndexOf("/Editor/", StringComparison.Ordinal) < 0)
            .ToArray();
    }

    private static string[] GetSerializedMonoBehaviourGuardPaths()
    {
        IEnumerable<string> enabledBuildScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
            .Select(scene => scene.path.Replace('\\', '/'))
            .Where(File.Exists);

        return enabledBuildScenes
            .Concat(GetFiles("Assets", "*.prefab"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeStatement(string statement)
    {
        string normalized = Regex.Replace(statement, @"\s+", " ").Trim();
        normalized = Regex.Replace(normalized, @"\s*\.\s*", ".");
        return normalized;
    }

    private static int GetLineNumber(string text, int characterIndex)
    {
        int lineNumber = 1;

        for (int i = 0; i < characterIndex && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private static string[] GetSerializedAssetPaths()
    {
        return new[] { "*.unity", "*.prefab", "*.asset" }
            .SelectMany(pattern => GetFiles("Assets", pattern))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetFiles(string root, string pattern)
    {
        if (!Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(root, pattern, SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }
}
