using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class FlameLocalLight : MonoBehaviour
{
    public const string GlowObjectName = "LocalFlameGlow";

    private const string DefaultTargetSortingLayerName = "Background";
    private const int AdditiveBlendStyleIndex = 1;

    [Header("Post Processing")]
    [SerializeField] private bool isolateFromGlobalPostProcessing = true;
    [SerializeField] private string noPostProcessLayerName = NoPostProcessRenderLayer.DefaultLayerName;

    [Header("2D Light")]
    [SerializeField] private bool createRuntimeLight2D = true;
    [SerializeField, ColorUsage(false, true)] private Color lightColor = new Color(1f, 0.48f, 0.14f, 1f);
    [SerializeField, Min(0f)] private float lightIntensity = 1.25f;
    [SerializeField, Min(0f)] private float innerRadius = 0.06f;
    [SerializeField, Min(0.01f)] private float outerRadius = 1.15f;
    [SerializeField, Range(0f, 1f)] private float falloffIntensity = 0.72f;
    [SerializeField] private string targetSortingLayerName = DefaultTargetSortingLayerName;

    [Header("Visible Glow")]
    [SerializeField] private bool createRuntimeGlowSprite = true;
    [SerializeField, ColorUsage(false, true)] private Color glowColor = new Color(1f, 0.42f, 0.12f, 0.34f);
    [SerializeField] private Vector2 glowScale = new Vector2(1.6f, 1.15f);
    [SerializeField] private Vector3 glowOffset = new Vector3(0f, 0.04f, 0f);
    [SerializeField] private int glowSortingOrderOffset = -1;

    [Header("Flicker")]
    [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.18f;
    [SerializeField, Min(0.01f)] private float flickerSpeed = 5.5f;
    [SerializeField] private float phase;

    private static Sprite sharedGlowSprite;

    private ParticleSystem particleSystemCache;
    private ParticleSystemRenderer particleRenderer;
    private Light2D localLight;
    private SpriteRenderer glowRenderer;

    public static FlameLocalLight EnsureFor(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return null;
        }

        FlameLocalLight localLight = particleSystem.GetComponent<FlameLocalLight>();

        if (localLight == null && Application.isPlaying)
        {
            localLight = particleSystem.gameObject.AddComponent<FlameLocalLight>();
        }

        if (localLight != null)
        {
            localLight.ConfigureNow();
        }

        return localLight;
    }

    public static bool IsLikelyFlame(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return false;
        }

        Transform current = particleSystem.transform;

        while (current != null)
        {
            if (ContainsFlameToken(current.name))
            {
                return true;
            }

            if (current.GetComponent<RoomContentGroup>() != null)
            {
                break;
            }

            current = current.parent;
        }

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();

        if (renderer == null)
        {
            return false;
        }

        Material[] materials = renderer.sharedMaterials;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];

            if (material == null)
            {
                continue;
            }

            if (ContainsFlameToken(material.name) ||
                (material.shader != null && ContainsFlameToken(material.shader.name)) ||
                (material.mainTexture != null && ContainsFlameToken(material.mainTexture.name)))
            {
                return true;
            }
        }

        return false;
    }

    public void ConfigureNow()
    {
        CacheComponents();
        ApplyNoPostProcessLayer();
        Configure2DLight();
        ConfigureGlowSprite();
        ApplyAnimatedValues();
    }

    private void Reset()
    {
        phase = Mathf.Abs(StableHash(name) % 1000) * 0.013f;
    }

    private void OnEnable()
    {
        ConfigureNow();
    }

    private void OnValidate()
    {
        ConfigureNow();
    }

    private void LateUpdate()
    {
        ConfigureNow();
    }

    private void CacheComponents()
    {
        if (particleSystemCache == null)
        {
            particleSystemCache = GetComponent<ParticleSystem>();
        }

        if (particleRenderer == null)
        {
            particleRenderer = GetComponent<ParticleSystemRenderer>();
        }

        if (localLight == null)
        {
            localLight = GetComponent<Light2D>();
        }

        if (localLight == null && createRuntimeLight2D && Application.isPlaying)
        {
            localLight = gameObject.AddComponent<Light2D>();
        }

        if (glowRenderer == null)
        {
            Transform glowTransform = transform.Find(GlowObjectName);

            if (glowTransform != null)
            {
                glowRenderer = glowTransform.GetComponent<SpriteRenderer>();
            }
        }

        if (glowRenderer == null && createRuntimeGlowSprite && Application.isPlaying)
        {
            GameObject glowObject = new GameObject(GlowObjectName);
            glowObject.transform.SetParent(transform, false);
            glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        }
    }

    private void ApplyNoPostProcessLayer()
    {
        if (!isolateFromGlobalPostProcessing)
        {
            return;
        }

        int layer = LayerMask.NameToLayer(string.IsNullOrWhiteSpace(noPostProcessLayerName)
            ? NoPostProcessRenderLayer.DefaultLayerName
            : noPostProcessLayerName.Trim());

        if (layer < 0)
        {
            return;
        }

        SetLayerRecursively(transform, layer);
    }

    private void Configure2DLight()
    {
        if (localLight == null)
        {
            return;
        }

        localLight.lightType = Light2D.LightType.Point;
        localLight.blendStyleIndex = AdditiveBlendStyleIndex;
        localLight.color = lightColor;
        localLight.pointLightInnerRadius = innerRadius;
        localLight.pointLightOuterRadius = outerRadius;
        localLight.falloffIntensity = falloffIntensity;
        localLight.overlapOperation = Light2D.OverlapOperation.Additive;
        localLight.shadowsEnabled = false;
        localLight.volumetricEnabled = false;
        localLight.targetSortingLayers = ResolveTargetSortingLayers();
    }

    private void ConfigureGlowSprite()
    {
        if (glowRenderer == null)
        {
            return;
        }

        glowRenderer.enabled = createRuntimeGlowSprite;
        glowRenderer.sprite = GetGlowSprite();
        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;

        if (particleRenderer != null)
        {
            glowRenderer.sortingLayerID = particleRenderer.sortingLayerID;
            glowRenderer.sortingOrder = particleRenderer.sortingOrder + glowSortingOrderOffset;
        }
        else
        {
            glowRenderer.sortingLayerName = ResolveSortingLayerName(targetSortingLayerName);
            glowRenderer.sortingOrder = glowSortingOrderOffset;
        }
    }

    private void ApplyAnimatedValues()
    {
        float flicker = 1f + flickerAmount * (Wave(GetPreviewTime() * flickerSpeed + phase) - 0.5f) * 2f;
        flicker = Mathf.Max(0f, flicker);

        if (localLight != null)
        {
            localLight.intensity = lightIntensity * flicker;
        }

        if (glowRenderer != null)
        {
            Color color = glowColor;
            color.a = Mathf.Clamp01(glowColor.a * flicker);
            glowRenderer.color = color;
            Transform glowTransform = glowRenderer.transform;
            glowTransform.localPosition = glowOffset;
            glowTransform.localRotation = Quaternion.identity;
            glowTransform.localScale = new Vector3(
                Mathf.Max(0.01f, glowScale.x) * flicker,
                Mathf.Max(0.01f, glowScale.y) * Mathf.Lerp(1f, flicker, 0.45f),
                1f);
        }
    }

    private int[] ResolveTargetSortingLayers()
    {
        string layerName = ResolveSortingLayerName(targetSortingLayerName);
        int layerId = SortingLayer.NameToID(layerName);

        if (SortingLayer.IsValid(layerId))
        {
            return new[] { layerId };
        }

        return new[] { SortingLayer.NameToID(DefaultTargetSortingLayerName) };
    }

    private static string ResolveSortingLayerName(string requestedName)
    {
        string layerName = string.IsNullOrWhiteSpace(requestedName)
            ? DefaultTargetSortingLayerName
            : requestedName.Trim();

        int layerId = SortingLayer.NameToID(layerName);
        return SortingLayer.IsValid(layerId) ? layerName : "Default";
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private static bool ContainsFlameToken(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.IndexOf("flame", StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf("hearth", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            string safeValue = value ?? string.Empty;

            for (int i = 0; i < safeValue.Length; i++)
            {
                hash = hash * 31 + safeValue[i];
            }

            return hash;
        }
    }

    private static Sprite GetGlowSprite()
    {
        if (sharedGlowSprite != null)
        {
            return sharedGlowSprite;
        }

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Generated_LocalFlameGlow",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0f, 0.32f, radius));
                float halo = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.12f, 1f, radius));
                float alpha = Mathf.Clamp01(0.58f * core + 0.42f * halo);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        sharedGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sharedGlowSprite.name = "Generated_LocalFlameGlow";
        sharedGlowSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedGlowSprite;
    }

    private static float GetPreviewTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return (float)UnityEditor.EditorApplication.timeSinceStartup;
        }
#endif
        return Time.time;
    }

    private static float Wave(float value)
    {
        return 0.5f + 0.5f * Mathf.Sin(value);
    }
}
