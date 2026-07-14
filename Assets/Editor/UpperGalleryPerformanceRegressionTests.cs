using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class UpperGalleryPerformanceRegressionTests
{
    private const string LeftPlantPath = "Assets/Art/Objects/upper_gallery_left_plant.png";
    private const string BypassCameraPath = "Assets/Scripts/Lighting/PostProcessBypassCamera.cs";

    [Test]
    public void UpperGalleryLeftPlantTextureIsCroppedToItsSpriteBounds()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(LeftPlantPath);
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(LeftPlantPath).OfType<Sprite>().Single();

        Assert.That(texture, Is.Not.Null);
        Assert.That(texture.width, Is.LessThanOrEqualTo(128));
        Assert.That(texture.height, Is.LessThanOrEqualTo(256));
        Assert.That(sprite.rect, Is.EqualTo(new Rect(0f, 0f, 101f, 222f)));
    }

    [Test]
    public void FlameBypassCameraDoesNotRebuildItsCameraStackEveryFrame()
    {
        string source = File.ReadAllText(BypassCameraPath);

        Assert.That(source, Does.Contain("HasSourceCameraSettingsChanged"));
        Assert.That(source, Does.Not.Contain("cameraStack.RemoveAll"));
    }

    [Test]
    public void FlameBypassCameraRepairsCameraAndStackMutationsOnDemand()
    {
        GameObject sourceObject = new GameObject("Source Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        GameObject bypassObject = new GameObject("Bypass Camera", typeof(Camera), typeof(UniversalAdditionalCameraData), typeof(PostProcessBypassCamera));

        try
        {
            Camera sourceCamera = sourceObject.GetComponent<Camera>();
            Camera bypassCamera = bypassObject.GetComponent<Camera>();
            PostProcessBypassCamera rig = bypassObject.GetComponent<PostProcessBypassCamera>();
            UniversalAdditionalCameraData sourceData = sourceObject.GetComponent<UniversalAdditionalCameraData>();
            MethodInfo hasChanged = typeof(PostProcessBypassCamera).GetMethod(
                "HasSourceCameraSettingsChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(hasChanged, Is.Not.Null);
            rig.Configure(sourceCamera, 1 << 29);
            Assert.That((bool)hasChanged.Invoke(rig, null), Is.False);

            sourceData.cameraStack.Add(bypassCamera);
            Assert.That((bool)hasChanged.Invoke(rig, null), Is.True, "Duplicate stack entries must be repaired.");
            rig.Configure(sourceCamera, 1 << 29);
            Assert.That(sourceData.cameraStack.Count(camera => camera == bypassCamera), Is.EqualTo(1));
            Assert.That(sourceData.cameraStack.Last(), Is.EqualTo(bypassCamera));

            bypassCamera.clearFlags = CameraClearFlags.Skybox;
            Assert.That((bool)hasChanged.Invoke(rig, null), Is.True, "Mutated bypass-camera settings must be restored.");
        }
        finally
        {
            Object.DestroyImmediate(bypassObject);
            Object.DestroyImmediate(sourceObject);
        }
    }
}
