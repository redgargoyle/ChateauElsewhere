using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Dreadforge/Navigation/Object Movement Blocker 2D")]
public sealed class ObjectMovementBlocker2D : MonoBehaviour
{
    [SerializeField] private Object sourceObject;
    [SerializeField] private string sourceObjectName;
    [SerializeField] private string sourceRoomName;
    [SerializeField] private string category;
    [SerializeField] [Min(0.001f)] private float footprintHeightFraction = 0.3f;
    [SerializeField] private bool generatedByCollisionBoxTool = true;
    [SerializeField] private bool sortSourceRenderers = true;
    [SerializeField] private string sourceSortingLayerName = "People";
    [SerializeField] private int sourceSortingOrderBase = 1000;
    [SerializeField] private float sourceSortingOrderPerYUnit = 100f;
    [SerializeField] private int sourceSortingOrderOffset;
    [SerializeField] private bool forceSourcePivotSortPoint = true;
    [SerializeField] private bool useRoomPerspectiveProfileSorting;
    [SerializeField] private RoomPerspectiveProfile roomPerspectiveProfile;
    [SerializeField] private Vector2 roomLocalSortingPoint;
    [SerializeField] private int roomPerspectiveSortingOffset;
    [SerializeField] [TextArea(2, 5)] private string authoringNote;

    private Object cachedSourceObject;
    private SpriteRenderer[] sourceRenderers;

    public Object SourceObject => sourceObject;
    public string SourceObjectName => sourceObjectName;
    public string SourceRoomName => sourceRoomName;
    public string Category => category;
    public float FootprintHeightFraction => footprintHeightFraction;
    public bool GeneratedByCollisionBoxTool => generatedByCollisionBoxTool;
    public bool SortSourceRenderers => sortSourceRenderers;
    public bool UseRoomPerspectiveProfileSorting => useRoomPerspectiveProfileSorting;
    public RoomPerspectiveProfile RoomPerspectiveProfile => roomPerspectiveProfile;
    public Vector2 RoomLocalSortingPoint => roomLocalSortingPoint;
    public int CurrentSortingOrder { get; private set; }
    public string AuthoringNote => authoringNote;
    public Collider2D BlockingCollider => GetComponent<Collider2D>();

    private void Reset()
    {
        ConfigureCollider();
    }

    private void OnEnable()
    {
        ConfigureCollider();
        RefreshSourceRenderers();
        ApplySourceSortingNow();
    }

    private void OnValidate()
    {
        footprintHeightFraction = Mathf.Max(0.001f, footprintHeightFraction);
        ConfigureCollider();
        RefreshSourceRenderers();
        ApplySourceSortingNow();
    }

    private void LateUpdate()
    {
        ApplySourceSortingNow();
    }

    public void Configure(
        Object source,
        string roomName,
        string blockerCategory,
        float heightFraction,
        string note,
        bool generated)
    {
        sourceObject = source;
        sourceObjectName = source != null ? source.name : string.Empty;
        sourceRoomName = string.IsNullOrWhiteSpace(roomName) ? string.Empty : roomName.Trim();
        category = string.IsNullOrWhiteSpace(blockerCategory) ? string.Empty : blockerCategory.Trim();
        footprintHeightFraction = Mathf.Max(0.001f, heightFraction);
        authoringNote = note ?? string.Empty;
        generatedByCollisionBoxTool = generated;
        ConfigureCollider();
        RefreshSourceRenderers();
        ApplySourceSortingNow();
    }

    public void SetRoomPerspectiveSorting(RoomPerspectiveProfile profile, Vector2 roomLocalPoint, int offset = 0)
    {
        roomPerspectiveProfile = profile;
        roomLocalSortingPoint = roomLocalPoint;
        roomPerspectiveSortingOffset = offset;
        useRoomPerspectiveProfileSorting = profile != null;
        ApplySourceSortingNow();
    }

    public void ApplySourceSortingNow()
    {
        if (!sortSourceRenderers)
        {
            return;
        }

        Collider2D collider = BlockingCollider;

        if (collider == null || !collider.enabled)
        {
            return;
        }

        if (sourceRenderers == null || cachedSourceObject != sourceObject)
        {
            RefreshSourceRenderers();
        }

        if (sourceRenderers == null || sourceRenderers.Length == 0)
        {
            return;
        }

        string layerName;
        if (useRoomPerspectiveProfileSorting && roomPerspectiveProfile != null)
        {
            layerName = GetSortingLayerName(roomPerspectiveProfile.SortingLayerName);
            CurrentSortingOrder = roomPerspectiveProfile.GetSortingOrder(
                roomLocalSortingPoint,
                sourceSortingOrderOffset + roomPerspectiveSortingOffset);
        }
        else
        {
            layerName = GetSortingLayerName(sourceSortingLayerName);
            CurrentSortingOrder = sourceSortingOrderBase -
                Mathf.RoundToInt(collider.bounds.min.y * sourceSortingOrderPerYUnit) +
                sourceSortingOrderOffset;
        }

        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = sourceRenderers[i];

            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.sortingLayerName = layerName;
            spriteRenderer.sortingOrder = CurrentSortingOrder;

            if (forceSourcePivotSortPoint)
            {
                spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }
    }

    private void ConfigureCollider()
    {
        Collider2D collider = GetComponent<Collider2D>();

        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    private void RefreshSourceRenderers()
    {
        cachedSourceObject = sourceObject;
        GameObject sourceGameObject = GetSourceGameObject();
        sourceRenderers = sourceGameObject != null
            ? sourceGameObject.GetComponentsInChildren<SpriteRenderer>(true)
            : null;
    }

    private GameObject GetSourceGameObject()
    {
        if (sourceObject is GameObject gameObjectSource)
        {
            return gameObjectSource;
        }

        if (sourceObject is Component componentSource)
        {
            return componentSource.gameObject;
        }

        return null;
    }

    private static string GetSortingLayerName(string requestedLayerName)
    {
        if (string.IsNullOrWhiteSpace(requestedLayerName))
        {
            return "Default";
        }

        if (string.Equals(requestedLayerName, "Default", System.StringComparison.OrdinalIgnoreCase) ||
            SortingLayer.NameToID(requestedLayerName) != 0)
        {
            return requestedLayerName;
        }

        return "Default";
    }
}
