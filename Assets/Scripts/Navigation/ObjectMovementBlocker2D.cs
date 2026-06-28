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
    [SerializeField] [TextArea(2, 5)] private string authoringNote;

    public Object SourceObject => sourceObject;
    public string SourceObjectName => sourceObjectName;
    public string SourceRoomName => sourceRoomName;
    public string Category => category;
    public float FootprintHeightFraction => footprintHeightFraction;
    public bool GeneratedByCollisionBoxTool => generatedByCollisionBoxTool;
    public string AuthoringNote => authoringNote;
    public Collider2D BlockingCollider => GetComponent<Collider2D>();

    private void Reset()
    {
        ConfigureCollider();
    }

    private void OnEnable()
    {
        ConfigureCollider();
    }

    private void OnValidate()
    {
        footprintHeightFraction = Mathf.Max(0.001f, footprintHeightFraction);
        ConfigureCollider();
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
    }

    private void ConfigureCollider()
    {
        Collider2D collider = GetComponent<Collider2D>();

        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }
}
