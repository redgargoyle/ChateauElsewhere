using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraAreaController : MonoBehaviour
{
    public Texture roomBackgroundTexture;
    public Image cameraButtonImage;
    public Color normalColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    public Color blinkColor = Color.white;
    public float blinkInterval = 0.5f;
    public float blinkDuration = 3f;

    private Coroutine blinkRoutine;
    private Graphic[] blinkGraphics;
    private Color[] originalColors;

    public Texture EffectiveRoomBackgroundTexture => GetEffectiveRoomBackgroundTexture();

    private void Reset()
    {
        cameraButtonImage = FindCameraButtonImage();
    }

    private void Awake()
    {
        ConfigureRaycastTargets();

        if (cameraButtonImage == null)
        {
            cameraButtonImage = FindCameraButtonImage();
        }

        RepairMissingRoomBackgroundTextureFromImage();
        CacheBlinkGraphics();
    }

    private void OnValidate()
    {
        if (cameraButtonImage == null)
        {
            cameraButtonImage = FindCameraButtonImage();
        }

        RepairMissingRoomBackgroundTextureFromImage();
        ConfigureRaycastTargets();
    }

    public Texture GetEffectiveRoomBackgroundTexture()
    {
        if (roomBackgroundTexture != null)
        {
            return roomBackgroundTexture;
        }

        return FindRoomTextureFromButtonImage();
    }

    public void StartBlinking()
    {
        StopBlinking();
        CacheBlinkGraphics();

        if (blinkGraphics.Length == 0)
        {
            Debug.LogWarning("CameraAreaController needs at least one child UI Graphic to blink.", this);
            return;
        }

        blinkRoutine = StartCoroutine(BlinkLoop());
    }

    public void StopBlinking()
    {
        if (blinkRoutine != null) { StopCoroutine(blinkRoutine); blinkRoutine = null; }
        ApplyOriginalColors();
    }

    private IEnumerator BlinkLoop()
    {
        bool bright = true;
        float delay = Mathf.Max(0.05f, blinkInterval);
        float endTime = Time.realtimeSinceStartup + Mathf.Max(0f, blinkDuration);

        while (Time.realtimeSinceStartup < endTime)
        {
            ApplyBlinkState(bright);
            bright = !bright;
            yield return new WaitForSecondsRealtime(delay);
        }

        blinkRoutine = null;
        ApplyOriginalColors();
    }

    private void CacheBlinkGraphics()
    {
        if (cameraButtonImage == null)
        {
            cameraButtonImage = FindCameraButtonImage();
        }

        List<Graphic> graphics = new List<Graphic>();
        Graphic rootGraphic = GetComponent<Graphic>();

        if (rootGraphic != null)
        {
            graphics.Add(rootGraphic);
        }

        Graphic[] childGraphics = GetComponentsInChildren<Graphic>(true);

        foreach (Graphic childGraphic in childGraphics)
        {
            if (!graphics.Contains(childGraphic))
            {
                graphics.Add(childGraphic);
            }
        }

        if (graphics.Count == 0 && cameraButtonImage != null)
        {
            graphics.Add(cameraButtonImage);
        }

        blinkGraphics = graphics.ToArray();
        originalColors = new Color[blinkGraphics.Length];

        for (int i = 0; i < blinkGraphics.Length; i++)
        {
            originalColors[i] = blinkGraphics[i].color;
        }
    }

    private void ApplyBlinkState(bool bright)
    {
        for (int i = 0; i < blinkGraphics.Length; i++)
        {
            blinkGraphics[i].color = bright ? blinkColor : originalColors[i];
        }
    }

    private void ApplyOriginalColors()
    {
        if (blinkGraphics == null || originalColors == null)
        {
            return;
        }

        for (int i = 0; i < blinkGraphics.Length; i++)
        {
            if (blinkGraphics[i] != null)
            {
                blinkGraphics[i].color = originalColors[i];
            }
        }
    }

    private Image FindCameraButtonImage()
    {
        Image[] childImages = GetComponentsInChildren<Image>(true);

        foreach (Image childImage in childImages)
        {
            if (childImage.gameObject.name == "Cam_BG")
            {
                return childImage;
            }
        }

        foreach (Image childImage in childImages)
        {
            if (childImage.gameObject != gameObject)
            {
                return childImage;
            }
        }

        return GetComponent<Image>();
    }

    private void RepairMissingRoomBackgroundTextureFromImage()
    {
        if (roomBackgroundTexture != null)
        {
            return;
        }

        Texture fallbackTexture = FindRoomTextureFromButtonImage();

        if (fallbackTexture != null)
        {
            // The map button image and the room background are often the same
            // imported picture. If the serialized Texture field loses its GUID,
            // recover from the child Image sprite instead of rendering white.
            roomBackgroundTexture = fallbackTexture;
        }
    }

    private Texture FindRoomTextureFromButtonImage()
    {
        if (cameraButtonImage == null)
        {
            cameraButtonImage = FindCameraButtonImage();
        }

        if (cameraButtonImage == null || cameraButtonImage.sprite == null)
        {
            return null;
        }

        return cameraButtonImage.sprite.texture;
    }

    private void ConfigureRaycastTargets()
    {
        Graphic rootGraphic = GetComponent<Graphic>();

        if (rootGraphic != null)
        {
            rootGraphic.raycastTarget = true;
        }

        Graphic[] childGraphics = GetComponentsInChildren<Graphic>(true);

        foreach (Graphic childGraphic in childGraphics)
        {
            if (childGraphic.gameObject != gameObject)
            {
                childGraphic.raycastTarget = false;
            }
        }
    }
}
