using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class Chapter3DiningTableForegroundOccluder : MonoBehaviour
{
    private static readonly string[] TableNameFragments =
    {
        "correct_dining_table",
        "dining_table_set"
    };

    private const string OccluderName = "Ch3_TableForegroundOccluder";
    private const string ResourcePath = "Chapter3/Dining/Layered/table_front_occluder";
    private const string EditorAssetPath = "Assets/Resources/Chapter3/Dining/Layered/table_front_occluder.png";

    [SerializeField] private string diningRoomId = "Dining Room";
    [SerializeField] private int sortingOrderOffset = 6000;
    [SerializeField] private int minimumSortingOrder = 20000;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer occluderRenderer;
    private Sprite occluderSprite;
    private bool warnedMissingSource;
    private bool warnedMissingSprite;

    public SpriteRenderer OccluderRenderer => occluderRenderer;

    private void LateUpdate()
    {
        if (occluderRenderer != null)
        {
            SyncFromSource();
        }
    }

    [ContextMenu("Ensure Table Foreground Occluder")]
    private void EnsureOccluderFromContextMenu()
    {
        EnsureOccluder();
    }

    public bool EnsureOccluder()
    {
        ResolveSourceRendererIfNeeded();

        if (sourceRenderer == null)
        {
            WarnMissingSourceOnce();
            return false;
        }

        ResolveOccluderSpriteIfNeeded();

        if (occluderSprite == null)
        {
            WarnMissingSpriteOnce();
            occluderSprite = sourceRenderer.sprite;
        }

        if (occluderSprite == null)
        {
            return false;
        }

        Transform parent = sourceRenderer.transform.parent != null ? sourceRenderer.transform.parent : sourceRenderer.transform;
        Transform existing = FindDirectChild(parent, OccluderName);

        if (existing == null)
        {
            GameObject occluderObject = new GameObject(OccluderName);
            existing = occluderObject.transform;
            existing.SetParent(parent, false);
        }

        occluderRenderer = existing.GetComponent<SpriteRenderer>();

        if (occluderRenderer == null)
        {
            occluderRenderer = existing.gameObject.AddComponent<SpriteRenderer>();
        }

        occluderRenderer.sprite = occluderSprite;
        occluderRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        occluderRenderer.color = Color.white;
        occluderRenderer.enabled = true;
        existing.gameObject.SetActive(true);
        SyncFromSource();
        return true;
    }

    public void HideOccluder()
    {
        if (occluderRenderer != null)
        {
            occluderRenderer.enabled = false;
        }
    }

    private void SyncFromSource()
    {
        if (sourceRenderer == null || occluderRenderer == null)
        {
            return;
        }

        Transform sourceTransform = sourceRenderer.transform;
        Transform occluderTransform = occluderRenderer.transform;
        occluderTransform.SetParent(sourceTransform.parent, false);
        occluderTransform.localPosition = sourceTransform.localPosition;
        occluderTransform.localRotation = sourceTransform.localRotation;
        occluderTransform.localScale = sourceTransform.localScale;
        occluderRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        occluderRenderer.sortingOrder = Mathf.Max(sourceRenderer.sortingOrder + sortingOrderOffset, minimumSortingOrder);
        occluderRenderer.enabled = occluderSprite != null;
    }

    private void ResolveSourceRendererIfNeeded()
    {
        if (sourceRenderer != null)
        {
            return;
        }

        Transform diningRoomRoot = FindDiningRoomRoot();

        if (diningRoomRoot == null)
        {
            return;
        }

        SpriteRenderer[] renderers = diningRoomRoot.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null || renderer.transform == null)
            {
                continue;
            }

            if (renderer.transform.name == OccluderName)
            {
                continue;
            }

            if (IsKnownTableRenderer(renderer))
            {
                sourceRenderer = renderer;
                return;
            }
        }
    }

    private void ResolveOccluderSpriteIfNeeded()
    {
        if (occluderSprite != null)
        {
            return;
        }

        occluderSprite = Resources.Load<Sprite>(ResourcePath);

#if UNITY_EDITOR
        if (occluderSprite == null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(EditorAssetPath) as TextureImporter;

            if (importer != null)
            {
                bool changed = false;
                changed |= SetIfDifferent(importer.textureType, TextureImporterType.Sprite, value => importer.textureType = value);
                changed |= SetIfDifferent(importer.spriteImportMode, SpriteImportMode.Single, value => importer.spriteImportMode = value);
                changed |= SetIfDifferent(importer.alphaIsTransparency, true, value => importer.alphaIsTransparency = value);
                changed |= SetIfDifferent(importer.mipmapEnabled, false, value => importer.mipmapEnabled = value);
                changed |= SetIfDifferent(importer.spritePixelsPerUnit, 100f, value => importer.spritePixelsPerUnit = value);
                changed |= SetIfDifferent(importer.maxTextureSize, 2048, value => importer.maxTextureSize = value);

                if (importer.spritePivot != Vector2.zero)
                {
                    importer.spritePivot = Vector2.zero;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }

            occluderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorAssetPath);
        }
#endif
    }

    private Transform FindDiningRoomRoot()
    {
        RoomContentGroup[] roomGroups = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);

        for (int i = 0; i < roomGroups.Length; i++)
        {
            RoomContentGroup group = roomGroups[i];

            if (group != null &&
                string.Equals(group.RoomName, diningRoomId, System.StringComparison.OrdinalIgnoreCase))
            {
                return group.transform;
            }
        }

        return null;
    }

    private static bool IsKnownTableRenderer(SpriteRenderer renderer)
    {
        string objectName = renderer.transform.name;
        string spriteName = renderer.sprite != null ? renderer.sprite.name : string.Empty;

        for (int i = 0; i < TableNameFragments.Length; i++)
        {
            string fragment = TableNameFragments[i];

            if ((!string.IsNullOrEmpty(objectName) &&
                    objectName.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(spriteName) &&
                    spriteName.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindDirectChild(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child != null &&
                string.Equals(child.name, objectName, System.StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private void WarnMissingSourceOnce()
    {
        if (warnedMissingSource)
        {
            return;
        }

        warnedMissingSource = true;
        Debug.LogWarning("[Ch3Dining] Could not find an existing correct_dining_table renderer to align the table foreground occluder.", this);
    }

    private void WarnMissingSpriteOnce()
    {
        if (warnedMissingSprite)
        {
            return;
        }

        warnedMissingSprite = true;
        Debug.LogWarning("[Ch3Dining] Missing table_front_occluder sprite; falling back to the full scene table sprite.", this);
    }

#if UNITY_EDITOR
    private static bool SetIfDifferent<T>(T current, T desired, System.Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(current, desired))
        {
            return false;
        }

        setter(desired);
        return true;
    }
#endif
}
