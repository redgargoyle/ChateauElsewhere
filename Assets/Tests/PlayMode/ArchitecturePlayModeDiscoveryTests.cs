using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ArchitecturePlayModeDiscoveryTests
{
    [UnityTest]
    public IEnumerator PlayModeAssemblyRunsInsidePlayerLoop()
    {
        Assert.That(Application.isPlaying, Is.True);
        int startingFrame = Time.frameCount;

        yield return null;

        Assert.That(Time.frameCount, Is.GreaterThan(startingFrame));
    }
}
