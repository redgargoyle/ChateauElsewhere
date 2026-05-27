using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class RoomEnvironmentAuthoringWindow : EditorWindow
{
    private const string FlamePrefabPath = "Assets/Prefabs/Flame.prefab";
    private const string FlameMaterialPath = "Assets/Art/Flame/M_FlameParticleVertexColor.mat";

    private Vector2 scrollPosition;
    private bool createLights = true;
    private bool createParticleFire = true;
    private bool createPrerenderedPatchPlaceholders = true;

    [MenuItem("Dreadforge/Rooms/Environment Authoring")]
    public static void Open()
    {
        GetWindow<RoomEnvironmentAuthoringWindow>("Room Environment");
    }

    [MenuItem("Dreadforge/Rooms/Create Suggested Environment Placeholders")]
    public static void CreateSuggestedEnvironmentPlaceholders()
    {
        ApplyPlans(true, true, true);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Room Environment Authoring", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Creates missing editable scene objects only. Existing objects are left alone so Hamza can safely tweak positions, sizes, colors, and animation fields without this tool resetting them.",
            MessageType.Info);

        createLights = EditorGUILayout.ToggleLeft("Overlay lights / bloom sources", createLights);
        createParticleFire = EditorGUILayout.ToggleLeft("True particle fire placeholders", createParticleFire);
        createPrerenderedPatchPlaceholders = EditorGUILayout.ToggleLeft("Prerendered image patch placeholders", createPrerenderedPatchPlaceholders);

        if (GUILayout.Button("Create Missing Suggested Items In Open Scene"))
        {
            ApplyPlans(createLights, createParticleFire, createPrerenderedPatchPlaceholders);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Room Checklist", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (RoomPlan plan in Plans)
        {
            EditorGUILayout.LabelField(plan.roomName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Lights: {plan.lights.Length}  Fires: {plan.fires.Length}  Patches: {plan.patches.Length}");
            EditorGUILayout.Space(3f);
        }
        EditorGUILayout.EndScrollView();
    }

    private static void ApplyPlans(bool includeLights, bool includeFires, bool includePatches)
    {
        RoomContentGroup[] roomGroups = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);
        Dictionary<string, RoomContentGroup> roomsByName = new Dictionary<string, RoomContentGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (RoomContentGroup roomGroup in roomGroups)
        {
            if (roomGroup != null && !string.IsNullOrWhiteSpace(roomGroup.RoomName))
            {
                roomsByName[roomGroup.RoomName] = roomGroup;
            }
        }

        int createdCount = 0;

        foreach (RoomPlan plan in Plans)
        {
            if (!roomsByName.TryGetValue(plan.roomName, out RoomContentGroup roomGroup) || roomGroup == null)
            {
                Debug.LogWarning($"Environment plan skipped '{plan.roomName}' because no matching RoomContentGroup exists in the open scene.");
                continue;
            }

            if (includePatches)
            {
                foreach (PatchSpec patch in plan.patches)
                {
                    createdCount += CreatePatchPlaceholder(roomGroup, patch) ? 1 : 0;
                }
            }

            if (includeFires)
            {
                foreach (FireSpec fire in plan.fires)
                {
                    createdCount += CreateParticleFire(roomGroup, fire) ? 1 : 0;
                }
            }

            if (includeLights)
            {
                foreach (LightSpec light in plan.lights)
                {
                    createdCount += CreateOverlayLight(roomGroup, light) ? 1 : 0;
                }
            }

            NormalizeRoomAuthoringRootOrder(roomGroup.transform);
        }

        if (createdCount > 0)
        {
            Debug.Log($"Created {createdCount} room environment authoring object(s).");
        }
        else
        {
            Debug.Log("No new room environment authoring objects were needed.");
        }
    }

    private static bool CreateOverlayLight(RoomContentGroup roomGroup, LightSpec spec)
    {
        RectTransform root = EnsureRoot(roomGroup.transform, "Lighting");
        string objectName = "RoomLight_" + SanitizeName(spec.name);

        if (root.Find(objectName) != null)
        {
            return false;
        }

        GameObject lightObject = CreateUiObject(objectName, root, typeof(CanvasRenderer), typeof(Image), typeof(RoomLightOverlay), typeof(RoomEnvironmentMarker));
        RoomLightOverlay overlay = lightObject.GetComponent<RoomLightOverlay>();
        overlay.ApplyDefinition(spec.ToDefinition(roomGroup.RoomName));

        RoomEnvironmentMarker marker = lightObject.GetComponent<RoomEnvironmentMarker>();
        marker.Configure(roomGroup.RoomName, RoomEnvironmentItemKind.OverlayLight, spec.name, spec.notes);

        MarkDirty(lightObject);
        return true;
    }

    private static bool CreateParticleFire(RoomContentGroup roomGroup, FireSpec spec)
    {
        RectTransform root = EnsureRoot(roomGroup.transform, "TrueParticleFire");
        string objectName = "ParticleFire_" + SanitizeName(spec.name);

        if (root.Find(objectName) != null)
        {
            return false;
        }

        GameObject flamePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlamePrefabPath);
        GameObject fireObject = null;

        if (flamePrefab != null)
        {
            fireObject = PrefabUtility.InstantiatePrefab(flamePrefab, root) as GameObject;
            if (fireObject != null)
            {
                Undo.RegisterCreatedObjectUndo(fireObject, $"Create {objectName}");
            }
        }

        if (fireObject == null)
        {
            fireObject = CreateSceneObject(objectName, typeof(ParticleSystem), typeof(ParticleSystemRenderer));
            Undo.SetTransformParent(fireObject.transform, root, $"Parent {objectName}");
        }

        fireObject.name = objectName;
        fireObject.transform.localPosition = new Vector3(spec.position.x, spec.position.y, 0f);
        fireObject.transform.localRotation = Quaternion.identity;
        fireObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, spec.scale);

        RoomEnvironmentMarker marker = EnsureComponent<RoomEnvironmentMarker>(fireObject);
        marker.Configure(roomGroup.RoomName, RoomEnvironmentItemKind.TrueParticleFire, spec.name, spec.notes);

        ConfigureFlameParticle(fireObject);
        MarkDirty(fireObject);
        return true;
    }

    private static bool CreatePatchPlaceholder(RoomContentGroup roomGroup, PatchSpec spec)
    {
        RectTransform root = EnsureRoot(roomGroup.transform, "AnimatedPatches");
        string objectName = "PatchCandidate_" + SanitizeName(spec.name);

        if (root.Find(objectName) != null)
        {
            return false;
        }

        GameObject patchObject = CreateUiObject(objectName, root, typeof(CanvasRenderer), typeof(Image), typeof(StaticSetImagePlayer), typeof(RoomEnvironmentMarker));
        RectTransform rect = patchObject.GetComponent<RectTransform>();
        ApplyRect(rect, spec.position, spec.size, new Vector2(0.5f, 0.5f));

        Image image = patchObject.GetComponent<Image>();
        image.color = new Color(0.35f, 0.75f, 1f, 0.12f);
        image.raycastTarget = false;

        StaticSetImagePlayer player = patchObject.GetComponent<StaticSetImagePlayer>();
        player.targetImage = image;
        player.playOnEnable = true;
        player.preserveAspect = true;
        player.disableRaycastTarget = true;
        player.bringImageToFront = false;

        RoomEnvironmentMarker marker = patchObject.GetComponent<RoomEnvironmentMarker>();
        marker.Configure(roomGroup.RoomName, RoomEnvironmentItemKind.PrerenderedPatch, spec.name, spec.notes);

        patchObject.SetActive(false);
        MarkDirty(patchObject);
        return true;
    }

    private static RectTransform EnsureRoot(Transform roomTransform, string rootName)
    {
        Transform existing = roomTransform.Find(rootName);

        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject rootObject = CreateSceneObject(rootName, typeof(RectTransform));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        Undo.SetTransformParent(root, roomTransform, $"Create {rootName}");
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.localScale = Vector3.one;
        SetUiLayer(rootObject);
        MarkDirty(rootObject);
        return root;
    }

    private static GameObject CreateUiObject(string objectName, RectTransform parent, params Type[] components)
    {
        GameObject gameObject = CreateSceneObject(objectName, BuildComponentList(typeof(RectTransform), components));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        Undo.SetTransformParent(rect, parent, $"Create {objectName}");
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        SetUiLayer(gameObject);
        return gameObject;
    }

    private static GameObject CreateSceneObject(string objectName, params Type[] components)
    {
        GameObject gameObject = new GameObject(objectName, components);
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {objectName}");
        SetUiLayer(gameObject);
        return gameObject;
    }

    private static Type[] BuildComponentList(Type requiredFirstComponent, Type[] additionalComponents)
    {
        List<Type> types = new List<Type> { requiredFirstComponent };
        types.AddRange(additionalComponents);
        return types.ToArray();
    }

    private static void ApplyRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureFlameParticle(GameObject fireObject)
    {
        ParticleSystem particleSystem = fireObject.GetComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = fireObject.GetComponent<ParticleSystemRenderer>();

        if (particleSystem != null)
        {
            Undo.RecordObject(particleSystem, "Configure Room Flame");
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;

            if (!Application.isPlaying)
            {
                particleSystem.Clear(true);
                particleSystem.Simulate(0.35f, true, true, true);
                particleSystem.Play(true);
            }
        }

        if (renderer != null)
        {
            Undo.RecordObject(renderer, "Configure Room Flame Renderer");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);

            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            renderer.sortingLayerName = HasSortingLayer("Background") ? "Background" : "Default";
            renderer.sortingOrder = 45;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        }
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component != null)
        {
            return component;
        }

        return Undo.AddComponent<T>(target);
    }

    private static void NormalizeRoomAuthoringRootOrder(Transform roomTransform)
    {
        string[] preferredOrder =
        {
            "AnimatedPatches",
            "People",
            "TrueParticleFire",
            "Lighting",
            "Doors"
        };

        int siblingIndex = 0;

        foreach (string rootName in preferredOrder)
        {
            Transform child = roomTransform.Find(rootName);

            if (child == null)
            {
                continue;
            }

            child.SetSiblingIndex(siblingIndex);
            siblingIndex++;
        }
    }

    private static void MarkDirty(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return;
        }

        EditorUtility.SetDirty(gameObject);
        Scene scene = gameObject.scene;

        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static void SetUiLayer(GameObject gameObject)
    {
        int uiLayer = LayerMask.NameToLayer("UI");

        if (uiLayer >= 0)
        {
            gameObject.layer = uiLayer;
        }
    }

    private static bool HasSortingLayer(string sortingLayerName)
    {
        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.name == sortingLayerName)
            {
                return true;
            }
        }

        return false;
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Item";
        }

        char[] chars = value.Trim().ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars).Trim('_');
    }

    private static readonly RoomPlan[] Plans =
    {
        new RoomPlan(
            "Ballroom",
            new[]
            {
                Light("Ballroom Window Silver Wash", RoomLightAnimationStyle.WindowGlow, 420f, 46f, 520f, 340f, new Color(0.58f, 0.76f, 1f, 1f), 0.1f, 0.01f, 0.01f, 0.08f, 0.25f, "Cool moving window spill; polish against the existing chandelier bloom.")
            },
            Array.Empty<FireSpec>(),
            new[] { Patch("Ballroom Chandelier Candle Frames", 0f, 124f, 390f, 190f, "Use prerendered candle glints if the soft overlay feels too floaty.") }),

        new RoomPlan(
            "Billiard Room",
            Array.Empty<LightSpec>(),
            new[] { Fire("Billiard Fireplace", -676f, -92f, 56f, "True particle fire for the left fireplace. Pair with Billiard Fireplace Source and Hearth Spill overlays.") },
            new[] { Patch("Billiard Table Lamp Flicker", -632f, -42f, 180f, 210f, "Small prerendered lamp shade shimmer candidate.") }),

        new RoomPlan(
            "Blue Bedroom",
            Array.Empty<LightSpec>(),
            Array.Empty<FireSpec>(),
            new[] { Patch("Blue Bedroom Curtain Moon Flicker", 574f, 94f, 330f, 330f, "Prerendered curtain/window shimmer candidate.") }),

        new RoomPlan(
            "Butlers Pantry",
            new[]
            {
                Light("Butlers Pantry Chandelier Bloom", RoomLightAnimationStyle.ChandelierBloom, 14f, 148f, 680f, 310f, new Color(1f, 0.7f, 0.33f, 1f), 0.24f, 0f, 0.04f, 0.08f, 0.72f, "Main chandelier warmth across bottles and glass."),
                Light("Butlers Pantry Glass Sparkle", RoomLightAnimationStyle.CandleCluster, -402f, -8f, 360f, 250f, new Color(1f, 0.62f, 0.28f, 1f), 0.18f, 0f, 0.12f, 0.03f, 1.25f, "Small flicker over the glass shelves.")
            },
            Array.Empty<FireSpec>(),
            new[] { Patch("Butlers Pantry Glass Glints", -376f, 16f, 360f, 280f, "Use prerendered glass sparkle frames here.") }),

        new RoomPlan(
            "Chapel",
            Array.Empty<LightSpec>(),
            Array.Empty<FireSpec>(),
            new[] { Patch("Chapel Candle Flames", 0f, -50f, 230f, 150f, "Prerendered candle flame cluster on the altar.") }),

        new RoomPlan(
            "Conservatory",
            Array.Empty<LightSpec>(),
            Array.Empty<FireSpec>(),
            new[] { Patch("Conservatory Glass Ripple", 0f, 120f, 900f, 420f, "Soft prerendered glass/reflection drift candidate.") }),

        new RoomPlan(
            "Dining Room",
            Array.Empty<LightSpec>(),
            new[] { Fire("Dining Fireplace", 540f, -92f, 58f, "True particle fire for the right hearth.") },
            new[] { Patch("Dining Table Candle Line Frames", -38f, -86f, 500f, 180f, "Use tiny prerendered flame frames along the table candles.") }),

        new RoomPlan(
            "Drawing Room",
            Array.Empty<LightSpec>(),
            new[] { Fire("Drawing Fireplace", 0f, -76f, 58f, "True particle fire for the central fireplace.") },
            new[] { Patch("Drawing Fireplace Prerendered Flame", 0f, -76f, 170f, 130f, "Optional hand-painted flame frames over the firebox.") }),

        new RoomPlan(
            "Grand Entrance Hall",
            Array.Empty<LightSpec>(),
            Array.Empty<FireSpec>(),
            new[] { Patch("GEH Chandelier Candle Frames", 0f, 120f, 420f, 190f, "Prerendered candle sparkle candidate for the chandelier.") }),

        new RoomPlan(
            "Grand Entrance Hall Rear view",
            new[]
            {
                Light("Great Hall Rear Chandelier Bloom", RoomLightAnimationStyle.ChandelierBloom, 0f, 128f, 640f, 300f, new Color(1f, 0.7f, 0.34f, 1f), 0.24f, 0f, 0.04f, 0.08f, 0.7f, "Rear-view chandelier warmth."),
                Light("Great Hall Rear Window Moonwash", RoomLightAnimationStyle.WindowGlow, 0f, 58f, 620f, 380f, new Color(0.58f, 0.78f, 1f, 1f), 0.13f, 0.01f, 0.01f, 0.08f, 0.24f, "Cool window wash from the large rear glass.")
            },
            Array.Empty<FireSpec>(),
            new[] { Patch("Great Hall Rear Window Flicker", 0f, 84f, 500f, 350f, "Window shimmer/reflection patch candidate.") }),

        new RoomPlan(
            "Kitchen",
            Array.Empty<LightSpec>(),
            new[] { Fire("Kitchen Firebox", 302f, -78f, 60f, "True particle fire for the stove/firebox.") },
            new[] { Patch("Kitchen Pot Steam And Fire", 180f, -36f, 420f, 220f, "Prerendered steam/fire glints around pots and hanging pans.") }),

        new RoomPlan(
            "Library",
            Array.Empty<LightSpec>(),
            new[] { Fire("Library Fireplace", 166f, -34f, 54f, "True particle fire if the central hearth stays active.") },
            new[] { Patch("Library Bookcase Candle Glints", 332f, 46f, 420f, 300f, "Tiny prerendered shelf/candle sparkle candidate.") }),

        new RoomPlan(
            "Master Bedroom Suite",
            Array.Empty<LightSpec>(),
            new[] { Fire("Master Fireplace", -360f, -84f, 58f, "True particle fire for the suite fireplace.") },
            new[] { Patch("Master Bed Curtain Moon Drift", 44f, 88f, 520f, 320f, "Slow prerendered curtain/window drift candidate.") }),

        new RoomPlan(
            "Music Room",
            Array.Empty<LightSpec>(),
            new[] { Fire("Music Fireplace", -305f, 12f, 54f, "True particle fire for the left fireplace.") },
            new[] { Patch("Music Piano Candle Frames", -160f, -8f, 310f, 200f, "Small piano candle animation candidate.") }),

        new RoomPlan(
            "Nursery",
            new[]
            {
                Light("Nursery Chandelier Bloom", RoomLightAnimationStyle.ChandelierBloom, 0f, 150f, 560f, 260f, new Color(1f, 0.7f, 0.36f, 1f), 0.2f, 0f, 0.03f, 0.08f, 0.65f, "Warm nursery chandelier."),
                Light("Nursery Dollhouse Glow", RoomLightAnimationStyle.CandleCluster, 506f, -166f, 320f, 230f, new Color(1f, 0.55f, 0.26f, 1f), 0.2f, 0f, 0.16f, 0.03f, 1.2f, "Tiny lively glow near the dollhouse/play table.")
            },
            Array.Empty<FireSpec>(),
            new[] { Patch("Nursery Mobile Or Toy Flicker", 492f, -178f, 360f, 240f, "Prerendered toy/candle flicker candidate.") }),

        new RoomPlan(
            "Service Corridor",
            new[]
            {
                Light("Service Corridor Hanging Lantern", RoomLightAnimationStyle.SconceFlicker, 0f, 156f, 500f, 300f, new Color(1f, 0.64f, 0.28f, 1f), 0.24f, 0f, 0.12f, 0.04f, 1.15f, "Main hanging lantern."),
                Light("Service Corridor Side Sconces", RoomLightAnimationStyle.CandleCluster, -520f, 18f, 360f, 300f, new Color(1f, 0.58f, 0.25f, 1f), 0.22f, 0f, 0.18f, 0.03f, 1.4f, "Left/right side sconce shimmer; duplicate or split if needed.")
            },
            Array.Empty<FireSpec>(),
            new[] { Patch("Service Corridor Window Glow Frames", -644f, -12f, 260f, 300f, "Billiard-room window/door glow movement candidate.") }),

        new RoomPlan(
            "Side Stair Mudroom",
            new[]
            {
                Light("Side Stair Window Moonwash", RoomLightAnimationStyle.WindowGlow, 368f, 152f, 420f, 360f, new Color(0.55f, 0.72f, 1f, 1f), 0.14f, 0.01f, 0.01f, 0.1f, 0.22f, "Cool light from stair window."),
                Light("Side Stair Hall Lantern", RoomLightAnimationStyle.SconceFlicker, -214f, 112f, 480f, 300f, new Color(1f, 0.62f, 0.28f, 1f), 0.2f, 0f, 0.12f, 0.04f, 1.05f, "Warm hall lantern/sconce wash.")
            },
            Array.Empty<FireSpec>(),
            new[] { Patch("Side Stair Window Dust", 376f, 150f, 380f, 320f, "Prerendered window dust/light pulse candidate.") }),

        new RoomPlan(
            "Upper Gallery",
            Array.Empty<LightSpec>(),
            new[] { Fire("Upper Gallery Distant Fireplace", -710f, 8f, 45f, "Small distant true-particle fire; keep subtle.") },
            new[] { Patch("Upper Gallery Oculus Moon Drift", 0f, 280f, 720f, 260f, "Prerendered oculus/cloud shimmer candidate.") }),

        new RoomPlan(
            "Upper Sitting Hall",
            new[]
            {
                Light("Upper Sitting Hall Chandelier Bloom", RoomLightAnimationStyle.ChandelierBloom, 0f, 150f, 560f, 260f, new Color(1f, 0.68f, 0.32f, 1f), 0.2f, 0f, 0.04f, 0.08f, 0.7f, "Central hall chandelier."),
                Light("Upper Sitting Hall Far Door Glow", RoomLightAnimationStyle.WindowGlow, 214f, 6f, 420f, 330f, new Color(0.64f, 0.78f, 1f, 1f), 0.12f, 0.01f, 0.01f, 0.07f, 0.28f, "Cool far-door/window softness.")
            },
            Array.Empty<FireSpec>(),
            new[] { Patch("Upper Sitting Hall Chandelier Frames", 0f, 150f, 340f, 190f, "Subtle prerendered candle glints.") })
    };

    private static LightSpec Light(string name, RoomLightAnimationStyle style, float x, float y, float width, float height, Color color, float alpha, float offAlpha, float flicker, float drift, float speed, string notes)
    {
        return new LightSpec(name, style, new Vector2(x, y), new Vector2(width, height), 0f, color, alpha, offAlpha, flicker, drift, speed, 0f, notes);
    }

    private static FireSpec Fire(string name, float x, float y, float scale, string notes)
    {
        return new FireSpec(name, new Vector2(x, y), scale, notes);
    }

    private static PatchSpec Patch(string name, float x, float y, float width, float height, string notes)
    {
        return new PatchSpec(name, new Vector2(x, y), new Vector2(width, height), notes);
    }

    private sealed class RoomPlan
    {
        public readonly string roomName;
        public readonly LightSpec[] lights;
        public readonly FireSpec[] fires;
        public readonly PatchSpec[] patches;

        public RoomPlan(string roomName, LightSpec[] lights, FireSpec[] fires, PatchSpec[] patches)
        {
            this.roomName = roomName;
            this.lights = lights;
            this.fires = fires;
            this.patches = patches;
        }
    }

    private readonly struct LightSpec
    {
        public readonly string name;
        public readonly RoomLightAnimationStyle style;
        public readonly Vector2 position;
        public readonly Vector2 size;
        public readonly float rotation;
        public readonly Color color;
        public readonly float onAlpha;
        public readonly float offAlpha;
        public readonly float flicker;
        public readonly float drift;
        public readonly float speed;
        public readonly float phase;
        public readonly string notes;

        public LightSpec(string name, RoomLightAnimationStyle style, Vector2 position, Vector2 size, float rotation, Color color, float onAlpha, float offAlpha, float flicker, float drift, float speed, float phase, string notes)
        {
            this.name = name;
            this.style = style;
            this.position = position;
            this.size = size;
            this.rotation = rotation;
            this.color = color;
            this.onAlpha = onAlpha;
            this.offAlpha = offAlpha;
            this.flicker = flicker;
            this.drift = drift;
            this.speed = speed;
            this.phase = phase;
            this.notes = notes;
        }

        public RoomLightDefinition ToDefinition(string roomName)
        {
            return new RoomLightDefinition
            {
                roomName = roomName,
                lightName = name,
                animationStyle = style,
                anchoredPosition = position,
                size = size,
                rotationDegrees = rotation,
                color = color,
                onAlpha = onAlpha,
                offAlpha = offAlpha,
                flickerAmount = flicker,
                driftAmount = drift,
                speed = speed,
                phase = phase
            };
        }
    }

    private readonly struct FireSpec
    {
        public readonly string name;
        public readonly Vector2 position;
        public readonly float scale;
        public readonly string notes;

        public FireSpec(string name, Vector2 position, float scale, string notes)
        {
            this.name = name;
            this.position = position;
            this.scale = scale;
            this.notes = notes;
        }
    }

    private readonly struct PatchSpec
    {
        public readonly string name;
        public readonly Vector2 position;
        public readonly Vector2 size;
        public readonly string notes;

        public PatchSpec(string name, Vector2 position, Vector2 size, string notes)
        {
            this.name = name;
            this.position = position;
            this.size = size;
            this.notes = notes;
        }
    }
}
