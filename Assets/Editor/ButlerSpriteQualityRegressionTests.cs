using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ButlerSpriteQualityRegressionTests
{
    private const string ButlerArtRoot = "Assets/Art/Characters/butler";

    [Test]
    public void EveryButlerSpriteUsesLosslessTwoXImportWithoutChangingWorldSize()
    {
        string[] paths = Directory
            .GetFiles(ButlerArtRoot, "*.png", SearchOption.AllDirectories)
            .OrderBy(path => path)
            .ToArray();

        Assert.That(paths.Length, Is.EqualTo(36), "The reviewed set is 16 walk, 8 directional-idle, and 12 breathing-idle sprites.");
        foreach (string path in paths)
        {
            ReadPng(path, out int width, out int height);
            Assert.That(width, Is.EqualTo(336), $"{path} should retain the reviewed 2x width.");
            Assert.That(height, Is.EqualTo(598), $"{path} should retain the reviewed 2x height.");

            string meta = File.ReadAllText(path + ".meta");
            Assert.That(meta, Does.Contain("spritePixelsToUnits: 200"), $"{path} should preserve its 1.68x2.99 world-unit bounds.");
            Assert.That(meta, Does.Contain("spritePivot: {x: 0.5, y: 0}"), $"{path} should stay registered at the feet.");
            Assert.That(meta, Does.Contain("filterMode: 1"), $"{path} should use bilinear sampling.");
            Assert.That(meta, Does.Contain("textureCompression: 0"), $"{path} should not lose close-up detail to block compression.");
            Assert.That(meta, Does.Contain("alphaIsTransparency: 1"), $"{path} should keep alpha-aware edge import.");

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, $"Unity should import {path} as a Sprite.");
            Assert.That(sprite.bounds.size.x, Is.EqualTo(1.68f).Within(0.0001f), $"{path} should keep its original world width.");
            Assert.That(sprite.bounds.size.y, Is.EqualTo(2.99f).Within(0.0001f), $"{path} should keep its original world height.");
            Assert.That(sprite.pivot.x, Is.EqualTo(168f).Within(0.0001f), $"{path} should remain horizontally centered.");
            Assert.That(sprite.pivot.y, Is.EqualTo(299f).Within(0.0001f), $"{path} should preserve its existing centered importer pivot.");
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Bilinear), $"{path} should import with smooth subpixel filtering.");
        }
    }

    [Test]
    public void ReviewedArmAndLegGapsAreActuallyTransparent()
    {
        AssertTransparent("butler_idle/butler_idle_01.png", 178, 500, "front leg gap");
        AssertTransparent("butler_idle/butler_idle_01.png", 100, 300, "front left arm gap");
        AssertTransparent("butler_idle/butler_idle_01.png", 246, 300, "front right arm gap");
        AssertTransparent("butler_classic_walk_06_r02_c02.png", 224, 290, "left-walk arm gap");
        AssertTransparent("butler_classic_walk_10_r03_c02.png", 114, 290, "right-walk arm gap");
        AssertTransparent("butler_classic_walk_14_r04_c02.png", 236, 260, "away-walk arm gap");
    }

    private static void AssertTransparent(string relativePath, int x, int topY, string description)
    {
        string path = Path.Combine(ButlerArtRoot, relativePath);
        Color32[] pixels = ReadPng(path, out int width, out int height);
        int bottomY = height - 1 - topY;
        Color32 pixel = pixels[bottomY * width + x];

        Assert.That(pixel.a, Is.LessThanOrEqualTo(8), $"The {description} in {path} should reveal the room, not a white matte pixel.");
    }

    private static Color32[] ReadPng(string path, out int width, out int height)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false), Is.True, $"Could not decode {path}.");
            width = texture.width;
            height = texture.height;
            return texture.GetPixels32();
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }
}
