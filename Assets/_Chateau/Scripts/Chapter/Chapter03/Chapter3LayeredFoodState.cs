using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Chapter3LayeredFoodState : MonoBehaviour
{
    [SerializeField] private Image coveredImage;
    [SerializeField] private Image fullImage;
    [SerializeField] private Image halfImage;
    [SerializeField] private Image emptyImage;

    [SerializeField] private SpriteRenderer coveredRenderer;
    [SerializeField] private SpriteRenderer fullRenderer;
    [SerializeField] private SpriteRenderer halfRenderer;
    [SerializeField] private SpriteRenderer emptyRenderer;

    public string CurrentState { get; private set; } = "Hidden";

    public void Configure(
        Image covered,
        Image full,
        Image half,
        Image empty,
        SpriteRenderer coveredSprite,
        SpriteRenderer fullSprite,
        SpriteRenderer halfSprite,
        SpriteRenderer emptySprite)
    {
        coveredImage = covered;
        fullImage = full;
        halfImage = half;
        emptyImage = empty;
        coveredRenderer = coveredSprite;
        fullRenderer = fullSprite;
        halfRenderer = halfSprite;
        emptyRenderer = emptySprite;
        HideAll();
    }

    public void ShowCovered()
    {
        ShowOnly(coveredImage, coveredRenderer);
        CurrentState = coveredImage != null || coveredRenderer != null ? "Covered" : "Hidden";
    }

    public void ShowFull()
    {
        if (fullImage == null && fullRenderer == null)
        {
            HideAll();
            CurrentState = "FullMissing";
            return;
        }

        ShowOnly(fullImage, fullRenderer);
        CurrentState = "Full";
    }

    public void ShowHalf()
    {
        if (halfImage == null && halfRenderer == null)
        {
            ShowFull();
            CurrentState = "HalfMissingKeepingFull";
            return;
        }

        ShowOnly(halfImage, halfRenderer);
        CurrentState = "Half";
    }

    public void ShowEmpty()
    {
        if (emptyImage == null && emptyRenderer == null)
        {
            HideAll();
            CurrentState = "EmptyMissing";
            return;
        }

        ShowOnly(emptyImage, emptyRenderer);
        CurrentState = "Empty";
    }

    public void HideAll()
    {
        SetVisible(coveredImage, coveredRenderer, false);
        SetVisible(fullImage, fullRenderer, false);
        SetVisible(halfImage, halfRenderer, false);
        SetVisible(emptyImage, emptyRenderer, false);
        CurrentState = "Hidden";
    }

    private void ShowOnly(Image image, SpriteRenderer renderer)
    {
        SetVisible(coveredImage, coveredRenderer, coveredImage == image && coveredRenderer == renderer);
        SetVisible(fullImage, fullRenderer, fullImage == image && fullRenderer == renderer);
        SetVisible(halfImage, halfRenderer, halfImage == image && halfRenderer == renderer);
        SetVisible(emptyImage, emptyRenderer, emptyImage == image && emptyRenderer == renderer);
    }

    private static void SetVisible(Image image, SpriteRenderer renderer, bool visible)
    {
        if (image != null)
        {
            image.enabled = visible && image.sprite != null;
        }

        if (renderer != null)
        {
            renderer.enabled = visible && renderer.sprite != null;
        }
    }
}
