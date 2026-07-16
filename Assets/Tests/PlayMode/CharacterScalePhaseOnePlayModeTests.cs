using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CharacterScalePhaseOnePlayModeTests
{
    private const int GameplayBuildIndex = 1;
    private const float ScaleTolerance = 0.000001f;

    private static readonly string[] ExpectedGuestNames =
    {
        "Guest 1",
        "Guest 2",
        "Guest 3",
        "Guest 4",
        "Guest 5",
        "Guest 6",
        "Guest 7",
        "Guest 8",
    };

    private static readonly float[] ExpectedGuestZScales =
    {
        1f,
        1f,
        1.12f,
        1.12f,
        1.3f,
        1.3f,
        1.3f,
        1.3f,
    };

    [UnityTest]
    public IEnumerator GameplayRosterLoadsWithoutMissingScriptsAndPreservesRootScales()
    {
        bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
        float previousTimeScale = Time.timeScale;
        var fatalSceneLoadMessages = new List<string>();
        Application.LogCallback captureFatalSceneLoadMessage = (condition, stackTrace, logType) =>
        {
            if (IsFatalSceneLoadMessage(condition))
            {
                fatalSceneLoadMessages.Add($"{logType}: {condition}");
            }
        };

        LogAssert.ignoreFailingMessages = true;
        Time.timeScale = 1f;
        Application.logMessageReceived += captureFatalSceneLoadMessage;

        try
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(GameplayBuildIndex, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "Gameplay must remain enabled at build index 1.");

            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;

            Scene gameplay = SceneManager.GetSceneByBuildIndex(GameplayBuildIndex);
            Assert.That(gameplay.IsValid() && gameplay.isLoaded, Is.True);
            AssertNoFatalSceneLoadMessages(fatalSceneLoadMessages, "Gameplay scene load");

            Component[] components = gameplay.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .ToArray();
            Assert.That(
                components.Any(component => component == null),
                Is.False,
                "Gameplay must load without missing-script component slots.");

            Component[] movementOwners = components
                .Where(component => component != null && component.GetType().Name == "PointClickPlayerMovement")
                .Distinct()
                .ToArray();
            Assert.That(
                movementOwners,
                Has.Length.EqualTo(9),
                "Gameplay must retain the Butler plus all eight guest Player-prefab movement owners.");

            Transform[] actorRoots = movementOwners
                .Select(component => component.transform)
                .Distinct()
                .OrderBy(root => root.name, System.StringComparer.Ordinal)
                .ToArray();
            AssertAuthoredRosterScales(actorRoots);

            Vector3[] authoredScales = actorRoots
                .Select(root => root.localScale)
                .ToArray();

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            for (int i = 0; i < actorRoots.Length; i++)
            {
                Assert.That(actorRoots[i], Is.Not.Null);
                Assert.That(
                    Vector3.Distance(actorRoots[i].localScale, authoredScales[i]),
                    Is.LessThanOrEqualTo(ScaleTolerance),
                    $"{actorRoots[i].name} root scale drifted during live Gameplay frames.");
            }

            AssertNoFatalSceneLoadMessages(fatalSceneLoadMessages, "Gameplay runtime initialization");

            Component[] componentsAfterFrames = gameplay.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .ToArray();
            Assert.That(
                componentsAfterFrames.Any(component => component == null),
                Is.False,
                "Runtime initialization must not leave missing-script component slots.");
        }
        finally
        {
            Application.logMessageReceived -= captureFatalSceneLoadMessage;
            Time.timeScale = previousTimeScale;
            LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
        }
    }

    private static void AssertAuthoredRosterScales(Transform[] actorRoots)
    {
        Transform butler = actorRoots.SingleOrDefault(
            root => string.Equals(root.name, "Player", StringComparison.Ordinal));
        Assert.That(butler, Is.Not.Null, "Gameplay must retain the Butler root named Player.");
        AssertScaleComponent(butler.localScale.x, 2.150601f, "Player X");
        AssertScaleComponent(butler.localScale.y, 2.150601f, "Player Y");
        AssertScaleComponent(butler.localScale.z, 1f, "Player Z");

        Transform[] guestRoots = actorRoots
            .Where(root => root != butler)
            .OrderBy(root => root.name, StringComparer.Ordinal)
            .ToArray();
        Assert.That(
            guestRoots.Select(root => root.name).ToArray(),
            Is.EqualTo(ExpectedGuestNames),
            "Gameplay must retain the exact Guest 1 through Guest 8 roster.");

        foreach (Transform guestRoot in guestRoots)
        {
            AssertScaleComponent(guestRoot.localScale.x, 1.42f, $"{guestRoot.name} X");
            AssertScaleComponent(guestRoot.localScale.y, 1.42f, $"{guestRoot.name} Y");
        }

        float[] guestZScales = guestRoots
            .Select(root => root.localScale.z)
            .OrderBy(scale => scale)
            .ToArray();
        Assert.That(guestZScales, Has.Length.EqualTo(ExpectedGuestZScales.Length));
        for (int i = 0; i < ExpectedGuestZScales.Length; i++)
        {
            AssertScaleComponent(guestZScales[i], ExpectedGuestZScales[i], $"Guest Z multiset index {i}");
        }
    }

    private static void AssertScaleComponent(float actual, float expected, string label)
    {
        Assert.That(
            actual,
            Is.EqualTo(expected).Within(ScaleTolerance),
            $"{label} scale must match the authored Gameplay value.");
    }

    private static bool IsFatalSceneLoadMessage(string condition)
    {
        if (string.IsNullOrEmpty(condition))
        {
            return false;
        }

        return condition.IndexOf(
                   "The referenced script on this Behaviour is missing",
                   StringComparison.Ordinal) >= 0
               || condition.IndexOf(
                   "The referenced script (Unknown) on this Behaviour is missing",
                   StringComparison.Ordinal) >= 0
               || condition.IndexOf(
                   "has a different serialization layout when loading",
                   StringComparison.Ordinal) >= 0
               || condition.IndexOf("Failed to deserialize", StringComparison.Ordinal) >= 0
               || condition.IndexOf("Error while deserializing", StringComparison.Ordinal) >= 0
               || condition.IndexOf("Could not deserialize", StringComparison.Ordinal) >= 0
               || condition.IndexOf("Cannot deserialize", StringComparison.Ordinal) >= 0
               || condition.IndexOf("Serialization depth limit", StringComparison.Ordinal) >= 0
               || IsFileBackedIntegerConversionFailure(condition);
    }

    private static bool IsFileBackedIntegerConversionFailure(string condition)
    {
        return condition.IndexOf("Failed to convert ", StringComparison.Ordinal) >= 0
               && condition.IndexOf(" bit int in file '", StringComparison.Ordinal) >= 0;
    }

    private static void AssertNoFatalSceneLoadMessages(List<string> messages, string phase)
    {
        Assert.That(
            messages,
            Is.Empty,
            $"{phase} emitted a missing-script or serialization-layout error:\n{string.Join("\n", messages)}");
    }
}
