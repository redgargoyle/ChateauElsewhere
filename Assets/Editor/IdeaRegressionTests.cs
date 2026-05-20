using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class IdeaRegressionTests
{
    private const string MainMenuControllerPath = "Assets/Scripts/MainMenuController.cs";
    private const string IdeaGameplayUiPath = "Assets/Scripts/Ideas/IdeaGameplayUI.cs";
    private const string IdeaGameplayUiBootstrapPath = "Assets/Scripts/Ideas/IdeaGameplayUIBootstrap.cs";

    [Test]
    public void BuiltInIdeasIncludeElsewhereAndThreeExplorableIdeas()
    {
        List<IdeaDefinition> ideas = IdeaManager.CreateDefaultIdeas();

        Assert.That(ideas.Count, Is.EqualTo(4));
        Assert.That(ideas.Exists(idea => idea.Id == "inheritance" && !idea.IsElsewhere), Is.True);
        Assert.That(ideas.Exists(idea => idea.Id == "appetite" && !idea.IsElsewhere), Is.True);
        Assert.That(ideas.Exists(idea => idea.Id == "witness" && !idea.IsElsewhere), Is.True);
        Assert.That(ideas.Exists(idea => idea.Id == IdeaManager.ElsewhereIdeaId && idea.IsElsewhere), Is.True);
    }

    [Test]
    public void IdeaDimensionSwitchesOnlyTheMatchingVariantRoot()
    {
        GameObject root = new GameObject("Portrait_Frame");
        GameObject neutral = new GameObject("Neutral");
        GameObject inheritance = new GameObject("Idea_Inheritance");
        GameObject appetite = new GameObject("Idea_Appetite");

        neutral.transform.SetParent(root.transform);
        inheritance.transform.SetParent(root.transform);
        appetite.transform.SetParent(root.transform);

        try
        {
            IdeaDimension dimension = root.AddComponent<IdeaDimension>();
            SerializedObject serializedDimension = new SerializedObject(dimension);

            serializedDimension.FindProperty("neutralRoot").objectReferenceValue = neutral;
            SerializedProperty variants = serializedDimension.FindProperty("variants");
            variants.arraySize = 2;

            SerializedProperty inheritanceVariant = variants.GetArrayElementAtIndex(0);
            inheritanceVariant.FindPropertyRelative("ideaId").stringValue = "inheritance";
            inheritanceVariant.FindPropertyRelative("root").objectReferenceValue = inheritance;
            inheritanceVariant.FindPropertyRelative("examineText").stringValue = "The frame is heavier than the portrait.";

            SerializedProperty appetiteVariant = variants.GetArrayElementAtIndex(1);
            appetiteVariant.FindPropertyRelative("ideaId").stringValue = "appetite";
            appetiteVariant.FindPropertyRelative("root").objectReferenceValue = appetite;
            appetiteVariant.FindPropertyRelative("examineText").stringValue = "The frame waits.";

            serializedDimension.ApplyModifiedPropertiesWithoutUndo();

            dimension.ApplyIdeaById("inheritance");

            Assert.That(neutral.activeSelf, Is.False);
            Assert.That(inheritance.activeSelf, Is.True);
            Assert.That(appetite.activeSelf, Is.False);
            Assert.That(dimension.CurrentExamineText, Is.EqualTo("The frame is heavier than the portrait."));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void NewGameStartsTheIdeaTutorialFlow()
    {
        string mainMenuControllerText = File.ReadAllText(MainMenuControllerPath);

        Assert.That(mainMenuControllerText, Does.Contain("IdeaGameFlow.MarkNewGameStarted()"));
    }

    [Test]
    public void GameplayIdeasUiCoversCoreAuthoringNeeds()
    {
        string uiText = File.ReadAllText(IdeaGameplayUiPath);

        Assert.That(uiText, Does.Contain("BuildIdeasPanel"), "The player needs a simple menu for discovering and starting Ideas.");
        Assert.That(uiText, Does.Contain("DoorTriggerNavigation.HoveredTriggerChanged"), "Door hover should update the selection readout.");
        Assert.That(uiText, Does.Contain("IdeaWorldObject.SelectedObjectChanged"), "World-object selection should update the selection readout.");
        Assert.That(uiText, Does.Contain("PlaceItem("), "The Ideas menu should include a minimal world-placement path.");
        Assert.That(uiText, Does.Contain("StartTutorial"), "New games should be able to launch the first Ideas tutorial.");
    }

    [Test]
    public void GameplayIdeasUiBootstrapsOnlyInGameplay()
    {
        string bootstrapText = File.ReadAllText(IdeaGameplayUiBootstrapPath);

        Assert.That(bootstrapText, Does.Contain("CameraManager"));
        Assert.That(bootstrapText, Does.Contain("Canvas_Background"));
        Assert.That(bootstrapText, Does.Not.Contain("DoorTriggerNavigation"), "MainMenu has legacy door triggers, so they must not be the bootstrap signal.");
    }
}
