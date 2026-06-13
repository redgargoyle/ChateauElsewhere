using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoomLightingPreset", menuName = "ChataeuChatilly/Lighting/Room Lighting Preset")]
public sealed class RoomLightingPreset : ScriptableObject
{
    [SerializeField] private bool startLightsOn = true;
    [SerializeField] private float toggleFadeSeconds = 0.65f;
    [SerializeField] private List<RoomLightDefinition> lights = new List<RoomLightDefinition>();

    public bool StartLightsOn => startLightsOn;
    public float ToggleFadeSeconds => Mathf.Max(0.01f, toggleFadeSeconds);
    public IReadOnlyList<RoomLightDefinition> Lights => lights;
}

[Serializable]
public sealed class RoomLightDefinition
{
    public string roomName;
    public string lightName;
    public RoomLightAnimationStyle animationStyle = RoomLightAnimationStyle.SconceFlicker;
    public Vector2 anchoredPosition;
    public Vector2 size = new Vector2(240f, 180f);
    public float rotationDegrees;
    [ColorUsage(false, true)] public Color color = new Color(1f, 0.72f, 0.34f, 1f);
    [Range(0f, 1f)] public float onAlpha = 0.32f;
    [Range(0f, 1f)] public float offAlpha;
    [Range(0f, 1f)] public float flickerAmount = 0.16f;
    [Range(0f, 1f)] public float driftAmount = 0.03f;
    public float speed = 1f;
    public float phase;
}

public enum RoomLightAnimationStyle
{
    SconceFlicker,
    ChandelierBloom,
    HearthBreath,
    WindowGlow,
    CandleCluster,
    FireplaceSource
}
