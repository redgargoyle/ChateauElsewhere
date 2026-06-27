using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StaticSetImagePlayer : MonoBehaviour
{
    public Image targetImage;
    public SpriteRenderer targetSpriteRenderer;
    public StaticSet set;
    public bool pingPong;
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

    private void LateUpdate()
    {
        if (bringImageToFront &&
            targetImage != null &&
            targetImage.transform.parent != null &&
            !IsDepthSortingOwnedByProjectionOrYSort())
        {
            targetImage.transform.SetAsLastSibling();
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

                int[] order = BuildOrder(group.frames.Length, group.shuffle, pingPong);

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

    private int[] BuildOrder(int length, bool shuffle, bool usePingPong)
    {
        int[] forwardOrder = new int[length];

        for (int i = 0; i < length; i++)
        {
            forwardOrder[i] = i;
        }

        if (shuffle)
        {
            for (int i = 0; i < forwardOrder.Length; i++)
            {
                int randomIndex = Random.Range(i, forwardOrder.Length);
                int current = forwardOrder[i];
                forwardOrder[i] = forwardOrder[randomIndex];
                forwardOrder[randomIndex] = current;
            }
        }

        int orderLength = usePingPong && length > 2 ? length * 2 - 2 : length;
        int[] order = new int[orderLength];

        for (int i = 0; i < length; i++)
        {
            order[i] = forwardOrder[i];
        }

        if (usePingPong && length > 2)
        {
            int writeIndex = length;

            for (int i = length - 2; i > 0; i--)
            {
                order[writeIndex] = forwardOrder[i];
                writeIndex++;
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

        targetImage.enabled = true;
        targetImage.preserveAspect = preserveAspect;

        if (disableRaycastTarget)
        {
            targetImage.raycastTarget = false;
        }

        if (bringImageToFront && !IsDepthSortingOwnedByProjectionOrYSort())
        {
            targetImage.transform.SetAsLastSibling();
        }
    }

    private void ConfigureSpriteRenderer()
    {
        if (targetSpriteRenderer == null || !overrideSpriteSorting || IsDepthSortingOwnedByProjectionOrYSort())
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(spriteSortingLayerName))
        {
            targetSpriteRenderer.sortingLayerName = spriteSortingLayerName;
        }

        targetSpriteRenderer.sortingOrder = spriteSortingOrder;
    }

    private bool IsDepthSortingOwnedByProjectionOrYSort()
    {
        return GetComponentInParent<RoomProjectedEntity>(true) != null ||
            GetComponentInParent<WorldYSortSpriteRenderer>(true) != null ||
            GetComponentInParent<YSortOcclusionFootprint2D>(true) != null;
    }
}
