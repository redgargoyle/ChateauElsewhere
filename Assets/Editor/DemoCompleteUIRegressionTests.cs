using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DemoCompleteUIRegressionTests
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string MenuFontPath = "Assets/Art/UI/Fonts/NotoSerifDisplay-Medium SDF.asset";
    private const string MenuButtonPath = "Assets/Art/MainMenuRedesign/MainMenu_ButtonBlank.png";
    private const string DemoCompleteUIPath = "Assets/Scripts/UI/DemoCompleteUI.cs";
    private const string ChapterManagerPath = "Assets/Scripts/Story/ChapterManager.cs";

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void GameplaySceneSerializesExactMainMenuStyleAssetsAndSceneTargets()
    {
        Type demoType = FindDemoCompleteType();
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        Component controller = FindSceneComponent(scene, demoType);
        SerializedObject serialized = new SerializedObject(controller);

        Assert.That(
            AssetDatabase.GetAssetPath(serialized.FindProperty("menuFontAsset").objectReferenceValue),
            Is.EqualTo(MenuFontPath));
        Assert.That(
            AssetDatabase.GetAssetPath(serialized.FindProperty("buttonFrameSprite").objectReferenceValue),
            Is.EqualTo(MenuButtonPath));
        Assert.That(serialized.FindProperty("gameplaySceneName").stringValue, Is.EqualTo("Gameplay"));
        Assert.That(serialized.FindProperty("mainMenuSceneName").stringValue, Is.EqualTo("MainMenu"));
    }

    [Test]
    public void CompletionOverlayKeepsActionsHiddenUntilRevealAndMatchesMainMenuButtons()
    {
        Type demoType = FindDemoCompleteType();
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        Component controller = FindSceneComponent(scene, demoType);

        Invoke(controller, "BeginFade", 1f);

        GameObject canvasObject = FindSceneObject(scene, "Canvas_DemoCompleteOverlay");
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        TextMeshProUGUI message = FindSceneObject(scene, "Text_DemoCompleteMessage")
            .GetComponent<TextMeshProUGUI>();
        GameObject actions = FindSceneObject(scene, "Panel_DemoCompleteActions");

        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(canvas.sortingOrder, Is.EqualTo(12001));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
        Assert.That(message.text, Is.EqualTo("Demo Complete!\nTo be continued..."));
        Assert.That(message.alpha, Is.EqualTo(0f));
        Assert.That(actions.activeSelf, Is.False, "Actions must not exist as interactive UI during the fade.");

        Invoke(controller, "RevealActions");

        Button restartButton = FindSceneObject(scene, "Button_RestartGame").GetComponent<Button>();
        Button mainMenuButton = FindSceneObject(scene, "Button_MainMenu").GetComponent<Button>();
        TMP_FontAsset expectedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MenuFontPath);
        Sprite expectedFrame = AssetDatabase.LoadAssetAtPath<Sprite>(MenuButtonPath);

        Assert.That(message.alpha, Is.EqualTo(1f));
        Assert.That(message.font, Is.SameAs(expectedFont));
        Assert.That(actions.activeSelf, Is.True);
        AssertStyledButton(restartButton, "Restart Game", expectedFont, expectedFrame);
        AssertStyledButton(mainMenuButton, "Main Menu", expectedFont, expectedFrame);
        Assert.That(restartButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
        Assert.That(restartButton.navigation.selectOnDown, Is.SameAs(mainMenuButton));
        Assert.That(mainMenuButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
        Assert.That(mainMenuButton.navigation.selectOnUp, Is.SameAs(restartButton));
    }

    [Test]
    public void CompletionActionsResetRuntimeStateAndLoadTheirConfiguredScenes()
    {
        Assert.That(File.Exists(DemoCompleteUIPath), Is.True, "The focused completion controller should exist.");
        string source = File.ReadAllText(DemoCompleteUIPath);
        string restartBody = ExtractMethodBody(source, "public void RestartGame");
        string mainMenuBody = ExtractMethodBody(source, "public void ReturnToMainMenu");
        string loadBody = ExtractMethodBody(source, "private void LoadScene");

        Assert.That(restartBody, Does.Contain("LoadScene(gameplaySceneName, \"Restart Game\")"));
        Assert.That(mainMenuBody, Does.Contain("LoadScene(mainMenuSceneName, \"Main Menu\")"));
        Assert.That(loadBody, Does.Contain("Application.CanStreamedLevelBeLoaded(sceneName)"));
        Assert.That(loadBody.IndexOf("GameplayRuntimeState.ResetForNewGame()", StringComparison.Ordinal),
            Is.LessThan(loadBody.IndexOf("SceneManager.LoadScene(sceneName, LoadSceneMode.Single)", StringComparison.Ordinal)));
    }

    [Test]
    public void ChapterManagerSynchronizesDemoMessageAndActionsAroundTheExistingBlackFade()
    {
        string source = File.ReadAllText(ChapterManagerPath);
        string completeRoutine = ExtractMethodBody(source, "private IEnumerator CompleteChapterRoutine");
        int normalizeIndex = completeRoutine.IndexOf(
            "string cleanNextChapterId = NormalizeNextChapterId(nextChapterId)",
            StringComparison.Ordinal);
        int requestIndex = completeRoutine.IndexOf(
            "bool isDemoCompletion = IsChapter3Request(cleanNextChapterId)",
            StringComparison.Ordinal);
        int beginIndex = completeRoutine.IndexOf(
            "demoCompleteUI.BeginFade(fadeSeconds)",
            StringComparison.Ordinal);
        int fadeIndex = completeRoutine.IndexOf(
            "yield return introUI.FadeToBlack(fadeSeconds)",
            StringComparison.Ordinal);
        int revealIndex = completeRoutine.IndexOf(
            "demoCompleteUI.RevealActions()",
            StringComparison.Ordinal);

        Assert.That(normalizeIndex, Is.GreaterThanOrEqualTo(0), "The next chapter must be normalized before fade routing.");
        Assert.That(requestIndex, Is.GreaterThan(normalizeIndex), "Only the Chapter 3 pending request should activate demo UI.");
        Assert.That(beginIndex, Is.GreaterThan(requestIndex), "The message should begin after identifying the demo boundary.");
        Assert.That(fadeIndex, Is.GreaterThan(beginIndex), "The message must begin on the first fade-to-black frame.");
        Assert.That(revealIndex, Is.GreaterThan(fadeIndex), "Actions must wait until the black fade coroutine completes.");
        Assert.That(CountOccurrences(completeRoutine, "demoCompleteUI.BeginFade(fadeSeconds)"), Is.EqualTo(1));
        Assert.That(source, Does.Contain("private static bool IsChapter3Request(string nextChapterId)"));
    }

    private static Type FindDemoCompleteType()
    {
        Type type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
            .FirstOrDefault(candidate => candidate.Name == "DemoCompleteUI");

        Assert.That(type, Is.Not.Null, "DemoCompleteUI should provide the focused completion overlay.");
        return type;
    }

    private static Component FindSceneComponent(Scene scene, Type componentType)
    {
        Component component = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Component>(true))
            .FirstOrDefault(candidate => candidate != null && candidate.GetType() == componentType);

        Assert.That(component, Is.Not.Null, $"{componentType.Name} should be authored in Gameplay.unity.");
        return component;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        Transform match = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(candidate => candidate.name == objectName);

        Assert.That(match, Is.Not.Null, $"Gameplay should contain runtime UI object '{objectName}'.");
        return match.gameObject;
    }

    private static void Invoke(Component component, string methodName, params object[] arguments)
    {
        MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, $"{component.GetType().Name}.{methodName} should exist.");
        method.Invoke(component, arguments);
    }

    private static void AssertStyledButton(
        Button button,
        string expectedLabel,
        TMP_FontAsset expectedFont,
        Sprite expectedFrame)
    {
        Image image = button.GetComponent<Image>();
        TextMeshProUGUI label = button.GetComponentsInChildren<TextMeshProUGUI>(true)
            .Single(candidate => candidate.name == "Text_Label");

        Assert.That(button.gameObject.activeInHierarchy, Is.True);
        Assert.That(button.interactable, Is.True);
        Assert.That(image.sprite, Is.SameAs(expectedFrame));
        Assert.That(label.text, Is.EqualTo(expectedLabel));
        Assert.That(label.font, Is.SameAs(expectedFont));
        Assert.That(label.color.r, Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(label.color.g, Is.EqualTo(0.075f).Within(0.001f));
        Assert.That(label.color.b, Is.EqualTo(0.16f).Within(0.001f));
        Assert.That(button.targetGraphic.name, Is.EqualTo("Button_StateOverlay"));
        Assert.That(((Image)button.targetGraphic).sprite, Is.SameAs(expectedFrame));
        Assert.That(button.colors.highlightedColor, Is.EqualTo(new Color(0.88f, 0.53f, 0.16f, 0.24f)));
        Assert.That(button.colors.pressedColor, Is.EqualTo(new Color(0.06f, 0.035f, 0.02f, 0.5f)));
        Assert.That(button.GetComponent<NavigationCursorHoverTarget>(), Is.Not.Null);
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), $"Missing method signature: {signature}");
        int openingBrace = source.IndexOf('{', signatureIndex);
        Assert.That(openingBrace, Is.GreaterThan(signatureIndex));
        int depth = 0;

        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return source.Substring(openingBrace + 1, index - openingBrace - 1);
                }
            }
        }

        Assert.Fail($"Method body did not close: {signature}");
        return string.Empty;
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int startIndex = 0;

        while ((startIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }
}
