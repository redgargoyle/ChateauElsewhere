using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StaticSetImagePlayer : MonoBehaviour
{
    public Image targetImage;
    public SpriteRenderer targetSpriteRenderer;
    public StaticSet set;
    public bool playOnEnable = true;
    public bool preserveAspect = false;
    public bool disableRaycastTarget = true;
    public bool overrideSpriteSorting = true;
    public string spriteSortingLayerName = "Default";
    public int spriteSortingOrder = 100;
    public bool bringImageToFront;
    public bool autoSelectRenderTarget = true;

    private readonly Dictionary<Texture2D, Sprite> spriteCache = new Dictionary<Texture2D, Sprite>();
    private Coroutine playRoutine;

    private void Reset()
    {
        targetImage = GetComponent<Image>();
        targetSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        ConfigureTargets();
    }

    private void OnValidate()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        ConfigureTargets();
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in spriteCache.Values)
        {
            if (sprite == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(sprite);
            }
            else
            {
                DestroyImmediate(sprite);
            }
        }

        spriteCache.Clear();
    }

    public void Play()
    {
        if (set == null || !set.IsValid() || (targetImage == null && targetSpriteRenderer == null))
        {
            return;
        }

        Stop();
        ConfigureTargets();
        playRoutine = StartCoroutine(PlayFrames());
    }

    public void Stop()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }

    private IEnumerator PlayFrames()
    {
        while (set != null && set.IsValid())
        {
            for (int i = 0; i < set.groups.Length; i++)
            {
                StaticFrameGroup group = set.groups[i];

                if (group == null || !group.IsValid())
                {
                    continue;
                }

                int[] order = BuildOrder(group.frames.Length, group.shuffle);

                for (int frameIndex = 0; frameIndex < order.Length; frameIndex++)
                {
                    Texture2D texture = group.frames[order[frameIndex]];

                    if (texture != null)
                    {
                        Sprite sprite = GetOrCreateSprite(texture);

                        if (targetImage != null)
                        {
                            targetImage.sprite = sprite;
                        }

                        if (targetSpriteRenderer != null)
                        {
                            targetSpriteRenderer.sprite = sprite;
                        }
                    }

                    yield return new WaitForSecondsRealtime(group.frameDuration);
                }
            }

            if (!set.loop)
            {
                break;
            }
        }

        playRoutine = null;
    }

    private int[] BuildOrder(int length, bool shuffle)
    {
        int[] order = new int[length];

        for (int i = 0; i < length; i++)
        {
            order[i] = i;
        }

        if (shuffle)
        {
            for (int i = 0; i < order.Length; i++)
            {
                int randomIndex = Random.Range(i, order.Length);
                int current = order[i];
                order[i] = order[randomIndex];
                order[randomIndex] = current;
            }
        }

        return order;
    }

    private Sprite GetOrCreateSprite(Texture2D texture)
    {
        if (spriteCache.TryGetValue(texture, out Sprite sprite) && sprite != null)
        {
            return sprite;
        }

        Rect rect = new Rect(0f, 0f, texture.width, texture.height);
        sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
        spriteCache[texture] = sprite;
        return sprite;
    }

    private void ConfigureTargets()
    {
        SelectRenderableTarget();
        ConfigureImage();
        ConfigureSpriteRenderer();
    }

    private void SelectRenderableTarget()
    {
        if (!autoSelectRenderTarget || (targetImage == null && targetSpriteRenderer == null))
        {
            return;
        }

        bool imageCanRender = targetImage != null && targetImage.GetComponentInParent<Canvas>() != null;

        if (targetImage != null)
        {
            targetImage.enabled = imageCanRender || targetSpriteRenderer == null;
        }

        if (targetSpriteRenderer != null)
        {
            targetSpriteRenderer.enabled = !imageCanRender;
        }
    }

    private void ConfigureImage()
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.preserveAspect = preserveAspect;

        if (disableRaycastTarget)
        {
            targetImage.raycastTarget = false;
        }

        if (bringImageToFront)
        {
            targetImage.transform.SetAsLastSibling();
        }
    }

    private void ConfigureSpriteRenderer()
    {
        if (targetSpriteRenderer == null || !overrideSpriteSorting)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(spriteSortingLayerName))
        {
            targetSpriteRenderer.sortingLayerName = spriteSortingLayerName;
        }

        targetSpriteRenderer.sortingOrder = spriteSortingOrder;
    }
}
