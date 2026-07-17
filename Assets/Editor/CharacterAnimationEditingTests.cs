using NUnit.Framework;
using UnityEngine;

public sealed class CharacterAnimationEditingTests
{
    [Test]
    public void CharacterRootResolvesAnimationDisplayAnimatorForEditing()
    {
        GameObject root = new GameObject("Guest 6");
        GameObject visual = new GameObject("AnimationDisplay");
        visual.transform.SetParent(root.transform, false);

        Animator animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animation/Player/Player.controller");

        CharacterAnimationDisplay display = root.AddComponent<CharacterAnimationDisplay>();
        display.Configure(visual.transform);

        try
        {
            Assert.That(
                CharacterAnimationEditing.TryResolveAnimatorHost(root, out GameObject resolvedHost),
                Is.True);
            Assert.That(resolvedHost, Is.EqualTo(visual));
            Assert.That(
                CharacterAnimationEditing.TryResolveAnimatorHost(visual, out GameObject resolvedFromChild),
                Is.True);
            Assert.That(resolvedFromChild, Is.EqualTo(visual));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
