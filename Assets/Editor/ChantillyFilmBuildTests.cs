using System.IO;
using NUnit.Framework;

public sealed class ChantillyFilmBuildTests
{
    [Test]
    public void FilmBuildUsesOptInCaptureDefineWithoutDevelopmentWatermark()
    {
        string build = File.ReadAllText("Assets/MarketingCapture/Editor/ChantillyFilmBuild.cs");
        string control = File.ReadAllText("Assets/MarketingCapture/ChantillyCaptureControl.cs");

        StringAssert.Contains("options = BuildOptions.None", build);
        StringAssert.Contains("extraScriptingDefines = new[] { \"CHANTILLY_CAPTURE\" }", build);
        StringAssert.DoesNotContain("options = BuildOptions.Development", build);
        StringAssert.Contains("UNITY_EDITOR || DEVELOPMENT_BUILD || CHANTILLY_CAPTURE", control);
    }
}
