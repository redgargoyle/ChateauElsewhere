using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FlameParticleMaterialRepair
{
    private const string MaterialPath = "Assets/Art/Flame/M_FlameParticleVertexColor.mat";

    [MenuItem("Dreadforge/Particles/Repair Flame Particle Materials")]
    public static void RepairOpenFlameParticles()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Material material = LoadMaterial();
        if (material == null)
        {
            return;
        }

        int repairedCount = 0;
        foreach (ParticleSystemRenderer renderer in Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>())
        {
            if (!IsSceneObject(renderer) || !IsFlameObject(renderer.gameObject))
            {
                continue;
            }

            if (RepairFlameRenderer(renderer, material, true))
            {
                repairedCount++;
            }
        }

        if (repairedCount > 0)
        {
            Debug.Log($"Repaired {repairedCount} Flame particle renderer material(s).");
        }
    }

    [MenuItem("Dreadforge/Particles/Repair Selected Particle Materials")]
    public static void RepairSelectedParticleMaterials()
    {
        Material material = LoadMaterial();
        if (material == null)
        {
            return;
        }

        int repairedCount = 0;
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            foreach (ParticleSystemRenderer renderer in selectedObject.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (RepairFlameRenderer(renderer, material, true))
                {
                    repairedCount++;
                }
            }
        }

        Debug.Log($"Repaired {repairedCount} selected particle renderer material(s).");
    }

    [MenuItem("Dreadforge/Particles/Repair Selected Particle Materials", true)]
    private static bool CanRepairSelectedParticleMaterials()
    {
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject.GetComponentInChildren<ParticleSystemRenderer>(true) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Material LoadMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"Could not find flame particle material at {MaterialPath}.");
        }

        return material;
    }

    private static bool RepairFlameRenderer(ParticleSystemRenderer renderer, Material material, bool forceMaterial)
    {
        bool changed = AssignMaterialIfNeeded(renderer, material, forceMaterial);
        changed |= ConfigureFlameParticleSystem(renderer);
        return changed;
    }

    private static bool AssignMaterialIfNeeded(ParticleSystemRenderer renderer, Material material, bool force)
    {
        if (renderer == null || material == null)
        {
            return false;
        }

        Material currentMaterial = renderer.sharedMaterial;
        if (currentMaterial == material)
        {
            return false;
        }

        if (!force && !NeedsRepair(currentMaterial))
        {
            return false;
        }

        Undo.RecordObject(renderer, "Repair Particle Material");
        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);

        Scene scene = renderer.gameObject.scene;
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        return true;
    }

    private static bool ConfigureFlameParticleSystem(ParticleSystemRenderer renderer)
    {
        ParticleSystem particleSystem = renderer != null ? renderer.GetComponent<ParticleSystem>() : null;
        if (particleSystem == null)
        {
            return false;
        }

        Undo.RecordObject(particleSystem, "Configure Flame Particles");
        Undo.RecordObject(renderer, "Configure Flame Particle Renderer");

        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.58f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
        main.startColor = Color.white;
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
        main.maxParticles = 160;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(45f);

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.055f;
        shape.arc = 360f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateFlameGradient());

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.18f, 1f),
            new Keyframe(0.72f, 0.7f),
            new Keyframe(1f, 0f)));

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.45f, 1.2f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.12f);
        noise.frequency = 2.5f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.8f);

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 1f;
        renderer.sortingFudge = 0.1f;
        renderer.sortingLayerName = SortingLayer.NameToID("Background") != 0 ? "Background" : "Default";
        renderer.sortingOrder = 10;

        if (!Application.isPlaying)
        {
            particleSystem.Clear(true);
            particleSystem.Simulate(0.35f, true, true, true);
            particleSystem.Play(true);
        }

        EditorUtility.SetDirty(particleSystem);
        EditorUtility.SetDirty(renderer);

        Scene scene = particleSystem.gameObject.scene;
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        return true;
    }

    private static Gradient CreateFlameGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.45f), 0f),
                new GradientColorKey(new Color(1f, 0.48f, 0.04f), 0.42f),
                new GradientColorKey(new Color(0.42f, 0.05f, 0.01f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.85f, 0.58f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }

    private static bool NeedsRepair(Material material)
    {
        if (material == null || material.shader == null)
        {
            return true;
        }

        string materialName = material.name;
        string shaderName = material.shader.name;
        return shaderName.Equals("Hidden/InternalErrorShader", StringComparison.Ordinal) ||
            shaderName.StartsWith("Particles/", StringComparison.OrdinalIgnoreCase) ||
            shaderName.StartsWith("Legacy Shaders/Particles/", StringComparison.OrdinalIgnoreCase) ||
            materialName.IndexOf("Default-Particle", StringComparison.OrdinalIgnoreCase) >= 0 ||
            materialName.IndexOf("Default-Material", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSceneObject(Component component)
    {
        return component != null &&
            component.gameObject.scene.IsValid() &&
            !EditorUtility.IsPersistent(component);
    }

    private static bool IsFlameObject(GameObject gameObject)
    {
        for (Transform current = gameObject != null ? gameObject.transform : null; current != null; current = current.parent)
        {
            if (current.name.IndexOf("Flame", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
