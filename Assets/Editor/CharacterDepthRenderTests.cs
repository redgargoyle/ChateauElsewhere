using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class CharacterDepthRenderTests
{
    [TestCase(-0.0005f, false, false)]
    [TestCase(0.0005f, false, false)]
    [TestCase(-0.0005f, true, false)]
    [TestCase(0.0005f, true, false)]
    [TestCase(-0.0005f, false, true)]
    [TestCase(0.0005f, false, true)]
    [TestCase(-0.0005f, true, true)]
    [TestCase(0.0005f, true, true)]
    public void RealCenteredAndBottomPivotArtSortByFloor(float butlerY, bool reverseUpdateOrder, bool walking)
    {
        GameObject cameraObject = new GameObject("Depth render camera");
        GameObject butler = new GameObject("Depth test Butler");
        GameObject guest = new GameObject("Depth test Guest4");
        RenderTexture target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        Material material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.7f;
            camera.transform.position = new Vector3(0, 1.5f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 30;
            camera.GetUniversalAdditionalCameraData().SetRenderer(0);
            target.Create();
            SpriteRenderer a = MakeActor(butler,
                "Assets/Art/Characters/butler/butler_idle/butler_idle_08.png", butlerY, material);
            SpriteRenderer b = MakeActor(guest,
                "Assets/Art/Characters/guest4_no_white_artifacts/countess_elowen_dusk_idle_down_03.png", 0, material);
            if (walking)
            {
                a.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/butler/butler_classic_walk_01_r01_c01.png");
                b.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/guest4_no_white_artifacts/countess_elowen_dusk_walk_01_r01_c01.png");
                Assert.That(a.sprite, Is.Not.Null);
                Assert.That(b.sprite, Is.Not.Null);
            }
            CharacterDepthGroup aDepth = butler.GetComponent<CharacterDepthGroup>();
            CharacterDepthGroup bDepth = guest.GetComponent<CharacterDepthGroup>();
            Bounds aBounds = a.bounds;
            Bounds bBounds = b.bounds;
            for (int i = 0; i < 5; i++)
            {
                (reverseUpdateOrder ? bDepth : aDepth).RefreshDepth();
                (reverseUpdateOrder ? aDepth : bDepth).RefreshDepth();
            }
            Assert.That(Vector3.Distance(aBounds.center, a.bounds.center), Is.LessThan(0.00001f));
            Assert.That(Vector3.Distance(bBounds.center, b.bounds.center), Is.LessThan(0.00001f));
            Assert.That(aDepth.Group.transform.position.y, Is.EqualTo(butlerY).Within(0.000001f));
            Assert.That(bDepth.Group.transform.position.y, Is.EqualTo(0).Within(0.000001f));
            Assert.That(butler.transform.localScale, Is.EqualTo(Vector3.one));

            b.enabled = false;
            Color32[] onlyA = Render(camera, target);
            b.enabled = true;
            a.enabled = false;
            Color32[] onlyB = Render(camera, target);
            a.enabled = true;
            Color32[] together = Render(camera, target);
            string evidenceDirectory = System.Environment.GetEnvironmentVariable("CHANTILLY_DEPTH_EVIDENCE_DIR");
            if (!string.IsNullOrEmpty(evidenceDirectory))
            {
                Directory.CreateDirectory(evidenceDirectory);
                Texture2D evidence = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                evidence.SetPixels32(together);
                evidence.Apply();
                File.WriteAllBytes(Path.Combine(evidenceDirectory,
                    $"butler-{(butlerY < 0 ? "front" : "behind")}-walk{walking}-reverse{reverseUpdateOrder}.png"), evidence.EncodeToPNG());
                Object.DestroyImmediate(evidence);
            }
            Color32[] expected = butlerY < 0 ? onlyA : onlyB;
            int tested = 0, incorrect = 0;
            for (int i = 0; i < together.Length; i++)
            {
                // Only fully opaque samples have an exact foreground reference;
                // translucent sprite-edge pixels legitimately blend both actors.
                if (onlyA[i].a < 255 || onlyB[i].a < 255 || Difference(onlyA[i], onlyB[i]) < 70) continue;
                tested++;
                if (Difference(together[i], expected[i]) > 12) incorrect++;
            }
            Assert.That(tested, Is.GreaterThan(100), "Must render a real, distinguishable opaque overlap.");
            Assert.That(incorrect, Is.LessThan(tested / 100 + 1),
                $"Lower floor must cover upper floor, independent of art pivot. Wrong pixels {incorrect}/{tested}.");
        }
        finally
        {
            Object.DestroyImmediate(butler);
            Object.DestroyImmediate(guest);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(material);
        }
    }

    private static SpriteRenderer MakeActor(GameObject actor, string spritePath, float floorY, Material material)
    {
        GameObject visual = new GameObject("AnimationDisplay");
        visual.layer = 30;
        visual.transform.SetParent(actor.transform, false);
        SpriteRenderer body = visual.AddComponent<SpriteRenderer>();
        body.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        Assert.That(body.sprite, Is.Not.Null, spritePath);
        body.sharedMaterial = material;
        body.sortingLayerName = "People";
        body.sortingOrder = 1000;
        body.spriteSortPoint = SpriteSortPoint.Pivot;
        CharacterAnimationDisplay display = actor.AddComponent<CharacterAnimationDisplay>();
        display.Configure(visual.transform);
        CharacterFloorReference floor = CharacterFloorReference.EnsureForActor(actor, body);
        floor.AlignActorToWorldPoint(new Vector2(0, floorY));
        CharacterDepthGroup.EnsureForActor(display).RefreshDepth();
        return body;
    }

    private static Color32[] Render(Camera camera, RenderTexture target)
    {
        // EditMode requests render synchronously, before the normal player loop
        // updates native sorting-group bounds/positions.
        SortingGroup.UpdateAllSortingGroups();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture previous = RenderTexture.active;
        Texture2D texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            return texture.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(texture);
        }
    }

    private static int Difference(Color32 a, Color32 b) =>
        Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
}
