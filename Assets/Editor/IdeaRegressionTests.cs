using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class IdeaRegressionTests
{
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
}
