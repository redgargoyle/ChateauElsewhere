using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SecurityRoomConfig", menuName = "Dreadforge/Security Room Config")]
public class SecurityRoomConfig : ScriptableObject
{
    public Texture2D fallbackFrame;
    public Texture2D[] lightOffFrames;
    public Texture2D[] lightOnFrames;
    public int initialFrameIndex = 6;
    public SecurityRoomDoorTrack leftDoor = new SecurityRoomDoorTrack
    {
        powerDrawId = "SecurityRoom.LeftDoor"
    };
    public SecurityRoomDoorTrack rightDoor = new SecurityRoomDoorTrack
    {
        powerDrawId = "SecurityRoom.RightDoor"
    };

    public bool HasFrames
    {
        get
        {
            return HasAnyFrame(lightOffFrames) || HasAnyFrame(lightOnFrames) || fallbackFrame != null;
        }
    }

    public Texture2D GetFrame(int frameIndex, bool lightsOn)
    {
        Texture2D frame = GetFrameFrom(lightsOn ? lightOnFrames : lightOffFrames, frameIndex);

        if (frame != null)
        {
            return frame;
        }

        frame = GetFrameFrom(lightsOn ? lightOffFrames : lightOnFrames, frameIndex);
        return frame != null ? frame : fallbackFrame;
    }

    public int ClampFrameIndex(int frameIndex)
    {
        int maxLength = Mathf.Max(GetLength(lightOffFrames), GetLength(lightOnFrames));

        if (maxLength <= 0)
        {
            return 0;
        }

        return Mathf.Clamp(frameIndex, 0, maxLength - 1);
    }

    private static Texture2D GetFrameFrom(Texture2D[] frames, int frameIndex)
    {
        if (frames == null || frames.Length == 0)
        {
            return null;
        }

        int clampedIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
        return frames[clampedIndex];
    }

    private static bool HasAnyFrame(Texture2D[] frames)
    {
        if (frames == null)
        {
            return false;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetLength(Texture2D[] frames)
    {
        return frames != null ? frames.Length : 0;
    }
}

[Serializable]
public class SecurityRoomDoorTrack
{
    public string powerDrawId = "SecurityRoom.Door";
    public bool startsClosed;
    public int openFrameIndex;
    public int closedFrameIndex = 5;
    public int[] openFrames;
    public int[] closeFrames;
    public float frameDuration = 0.08f;
    public float powerDrawRate = 0.55f;
}
