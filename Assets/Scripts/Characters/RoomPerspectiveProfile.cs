using System;
using UnityEngine;

[CreateAssetMenu(fileName = "RoomPerspectiveProfile", menuName = "ChataeuChatilly/Rooms/Room Perspective Profile")]
public sealed class RoomPerspectiveProfile : ScriptableObject
{
    [SerializeField] private string roomId = "Drawing Room";
    [SerializeField] private Vector2 nativeRoomReferenceSize = new Vector2(1366f, 768f);
    [SerializeField] private float nearFootY = -360f;
    [SerializeField] private float farFootY = 120f;
    [SerializeField] private AnimationCurve scaleByDepth = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.52f);
    [SerializeField] private Gradient tintByDepth = CreateDefaultTintGradient();
    [SerializeField] private string sortingLayerName = "People";
    [SerializeField] private int sortingOrderBase = 1000;
    [SerializeField] private int sortingOrderRange = 8000;
    [SerializeField] private AnimationCurve sortingOrderByDepth = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve shadowScaleByDepth = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.62f);
    [SerializeField] private AnimationCurve shadowOpacityByDepth = AnimationCurve.EaseInOut(0f, 0.38f, 1f, 0.14f);
    [SerializeField] private Vector2[] floorPolygon = Array.Empty<Vector2>();

    public string RoomId => roomId;
    public Vector2 NativeRoomReferenceSize => nativeRoomReferenceSize;
    public float NearFootY => nearFootY;
    public float FarFootY => farFootY;
    public string SortingLayerName => GetSafeSortingLayerName();
    public int SortingOrderBase => sortingOrderBase;
    public int SortingOrderRange => sortingOrderRange;
    public Vector2[] FloorPolygon => floorPolygon;

    private void OnValidate()
    {
        Sanitize();
    }

    public void Configure(
        string profileRoomId,
        Vector2 referenceSize,
        float nearY,
        float farY,
        AnimationCurve scaleCurve,
        Gradient tintGradient,
        int orderBase,
        int orderRange,
        AnimationCurve orderCurve)
    {
        roomId = string.IsNullOrWhiteSpace(profileRoomId) ? "Room" : profileRoomId.Trim();
        nativeRoomReferenceSize = referenceSize;
        nearFootY = nearY;
        farFootY = farY;
        scaleByDepth = scaleCurve ?? AnimationCurve.EaseInOut(0f, 1f, 1f, 0.52f);
        tintByDepth = tintGradient ?? CreateDefaultTintGradient();
        sortingOrderBase = orderBase;
        sortingOrderRange = orderRange;
        sortingOrderByDepth = orderCurve ?? AnimationCurve.Linear(0f, 1f, 1f, 0f);
        Sanitize();
    }

    public void ConfigureDrawingRoomDefaults()
    {
        roomId = "Drawing Room";
        nativeRoomReferenceSize = new Vector2(1366f, 768f);
        nearFootY = -360f;
        farFootY = 140f;
        scaleByDepth = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.54f);
        tintByDepth = CreateDefaultTintGradient();
        sortingLayerName = "People";
        sortingOrderBase = 1000;
        sortingOrderRange = 8000;
        sortingOrderByDepth = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        shadowScaleByDepth = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.62f);
        shadowOpacityByDepth = AnimationCurve.EaseInOut(0f, 0.38f, 1f, 0.14f);
        floorPolygon = Array.Empty<Vector2>();
        Sanitize();
    }

    public float GetDepth01(Vector2 roomLocalFootPoint)
    {
        if (Mathf.Approximately(nearFootY, farFootY))
        {
            return 0f;
        }

        return Mathf.Clamp01(Mathf.InverseLerp(nearFootY, farFootY, roomLocalFootPoint.y));
    }

    public float GetScale(Vector2 roomLocalFootPoint)
    {
        EnsureCurves();
        return Mathf.Max(0.001f, scaleByDepth.Evaluate(GetDepth01(roomLocalFootPoint)));
    }

    public Color GetTint(Vector2 roomLocalFootPoint)
    {
        if (tintByDepth == null)
        {
            tintByDepth = CreateDefaultTintGradient();
        }

        return tintByDepth.Evaluate(GetDepth01(roomLocalFootPoint));
    }

    public int GetSortingOrder(Vector2 roomLocalFootPoint, int offset = 0)
    {
        EnsureCurves();
        float order01 = Mathf.Clamp01(sortingOrderByDepth.Evaluate(GetDepth01(roomLocalFootPoint)));
        return sortingOrderBase + Mathf.RoundToInt(order01 * sortingOrderRange) + offset;
    }

    public float GetShadowScale(Vector2 roomLocalFootPoint)
    {
        EnsureCurves();
        return Mathf.Max(0.001f, shadowScaleByDepth.Evaluate(GetDepth01(roomLocalFootPoint)));
    }

    public float GetShadowOpacity(Vector2 roomLocalFootPoint)
    {
        EnsureCurves();
        return Mathf.Clamp01(shadowOpacityByDepth.Evaluate(GetDepth01(roomLocalFootPoint)));
    }

    public bool ContainsFloorPoint(Vector2 roomLocalFootPoint)
    {
        if (floorPolygon == null || floorPolygon.Length < 3)
        {
            return true;
        }

        bool inside = false;
        int previousIndex = floorPolygon.Length - 1;

        for (int i = 0; i < floorPolygon.Length; i++)
        {
            Vector2 current = floorPolygon[i];
            Vector2 previous = floorPolygon[previousIndex];

            bool crossesY = current.y > roomLocalFootPoint.y != previous.y > roomLocalFootPoint.y;
            if (crossesY)
            {
                float projectedX = (previous.x - current.x) *
                    (roomLocalFootPoint.y - current.y) /
                    Mathf.Max(0.0001f, previous.y - current.y) +
                    current.x;

                if (roomLocalFootPoint.x < projectedX)
                {
                    inside = !inside;
                }
            }

            previousIndex = i;
        }

        return inside;
    }

    private void Sanitize()
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            roomId = "Room";
        }

        nativeRoomReferenceSize = new Vector2(
            Mathf.Max(1f, nativeRoomReferenceSize.x),
            Mathf.Max(1f, nativeRoomReferenceSize.y));
        sortingOrderRange = Mathf.Max(0, sortingOrderRange);
        EnsureCurves();
    }

    private void EnsureCurves()
    {
        if (scaleByDepth == null || scaleByDepth.length == 0)
        {
            scaleByDepth = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.52f);
        }

        if (sortingOrderByDepth == null || sortingOrderByDepth.length == 0)
        {
            sortingOrderByDepth = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        }

        if (shadowScaleByDepth == null || shadowScaleByDepth.length == 0)
        {
            shadowScaleByDepth = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.62f);
        }

        if (shadowOpacityByDepth == null || shadowOpacityByDepth.length == 0)
        {
            shadowOpacityByDepth = AnimationCurve.EaseInOut(0f, 0.38f, 1f, 0.14f);
        }

        if (tintByDepth == null)
        {
            tintByDepth = CreateDefaultTintGradient();
        }
    }

    private string GetSafeSortingLayerName()
    {
        if (string.IsNullOrWhiteSpace(sortingLayerName))
        {
            return "Default";
        }

        return string.Equals(sortingLayerName, "Default", StringComparison.OrdinalIgnoreCase) ||
            SortingLayer.NameToID(sortingLayerName) != 0
                ? sortingLayerName
                : "Default";
    }

    private static Gradient CreateDefaultTintGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.96f, 0.88f), 0f),
                new GradientColorKey(new Color(0.72f, 0.76f, 0.78f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.82f, 1f)
            });
        return gradient;
    }
}
