using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ChantillyDesktopBuildTests
{
    [TestCase(
        "windows",
        "Windows",
        "Chantilly.exe",
        BuildTarget.StandaloneWindows64,
        GraphicsDeviceType.OpenGLCore)]
    [TestCase(
        "linux",
        "Linux",
        "Chantilly.x86_64",
        BuildTarget.StandaloneLinux64,
        GraphicsDeviceType.OpenGLCore)]
    [TestCase(
        "macos",
        "macOS",
        "Chantilly.app",
        BuildTarget.StandaloneOSX,
        GraphicsDeviceType.Metal)]
    public void SpecificationUsesExpectedPlatformValues(
        string key,
        string folder,
        string launcher,
        BuildTarget target,
        GraphicsDeviceType graphicsApi)
    {
        ChantillyDesktopBuild.Specification specification =
            ChantillyDesktopBuild.GetSpecification(key);

        Assert.That(specification.FolderName, Is.EqualTo(folder));
        Assert.That(specification.LauncherName, Is.EqualTo(launcher));
        Assert.That(specification.Target, Is.EqualTo(target));
        Assert.That(specification.GraphicsApis, Is.EqualTo(new[] { graphicsApi }));
    }

    [Test]
    public void UnknownPlatformIsRejected()
    {
        Assert.That(
            () => ChantillyDesktopBuild.GetSpecification("commodore"),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void PersistedPlayerIdentityUsesChantilly()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string projectSettingsPath = Path.Combine(
            projectRoot,
            "ProjectSettings",
            "ProjectSettings.asset");
        string projectSettings = File.ReadAllText(projectSettingsPath);

        Assert.That(projectSettings, Does.Contain("companyName: Chantilly"));
        Assert.That(projectSettings, Does.Contain("productName: Chantilly"));
        Assert.That(projectSettings, Does.Contain("projectName: Chantilly"));
        Assert.That(
            projectSettings,
            Does.Not.Contain("projectName: dreadforge_2022"));
    }
}
