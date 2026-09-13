using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CharacterSceneScaleRegressionTests
{
    [Test]
    public void GameplayCharactersDoNotMultiplyTheSharedDisplayScaleThroughTheirParents()
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay.unity", OpenSceneMode.Single);
            CharacterAnimationDisplay[] characters = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CharacterAnimationDisplay>(true))
                .ToArray();

            Assert.That(characters.Length, Is.EqualTo(9), "Inspect the actual Butler and eight scene guests, including prefab overrides.");
            foreach (CharacterAnimationDisplay character in characters)
            {
                Assert.That(character.HasValidDisplayRoot(), Is.True, character.name);
                Vector3 parentScale = character.AnimationDisplay.parent.lossyScale;
                Assert.That(parentScale.x, Is.EqualTo(1f).Within(0.0001f),
                    $"{character.name}: an ancestor changes the shared display width. Calibrate the room catalog instead.");
                Assert.That(parentScale.y, Is.EqualTo(1f).Within(0.0001f),
                    $"{character.name}: an ancestor changes the shared display height. Calibrate the room catalog instead.");
            }
        }
        finally
        {
            if (previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
