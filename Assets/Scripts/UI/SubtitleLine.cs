using System;
using UnityEngine;

[Serializable]
public sealed class SubtitleLine
{
    public string lineId;
    public string speakerId;
    public string speakerDisplayName;
    [TextArea] public string text;
    [Min(0f)] public float minDuration = 1.25f;
    [Min(0f)] public float maxDuration = 5f;
    public bool requireAdvance;
}
