using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName = "Chateau/Chapter 3/Layered Dinner Asset Manifest")]
public sealed class Chapter3LayeredDinnerAssetManifest : ScriptableObject
{
    public Vector2Int canvasSize = new Vector2Int(1448, 1086);

    public Sprite tableBack;
    public Sprite tableTopProps;
    public Sprite tableFrontOverlay;
    public Sprite coveredDish;
    public Sprite foodFull;
    public Sprite foodHalf;
    public Sprite foodEmpty;

    public Chapter3SeatLayerSet[] seats = new Chapter3SeatLayerSet[8];

    public bool Validate(out string message)
    {
        StringBuilder builder = new StringBuilder();
        bool valid = true;

        if (canvasSize.x != 1448 || canvasSize.y != 1086)
        {
            valid = false;
            builder.AppendLine($"Canvas size must be 1448x1086. Current value is {canvasSize.x}x{canvasSize.y}.");
        }

        valid &= ValidateRequiredSprite(tableBack, "table_back.png", builder);
        valid &= ValidateRequiredSprite(tableFrontOverlay, "table_front_overlay.png", builder);

        if (coveredDish == null)
        {
            builder.AppendLine("Optional layer missing: covered_dish.png.");
        }

        if (foodFull == null)
        {
            builder.AppendLine("Optional layer missing: food_full.png.");
        }

        if (foodHalf == null)
        {
            builder.AppendLine("Optional layer missing: food_half.png.");
        }

        if (foodEmpty == null)
        {
            builder.AppendLine("Optional layer missing: food_empty.png.");
        }

        if (seats == null || seats.Length != 8)
        {
            valid = false;
            builder.AppendLine("Manifest must contain exactly 8 seat layer sets.");
        }

        for (int i = 0; i < 8; i++)
        {
            Chapter3SeatLayerSet seat = GetSeat(i);
            string seatLabel = $"Seat{i + 1:00}";

            if (seat == null)
            {
                valid = false;
                builder.AppendLine($"{seatLabel} is missing.");
                continue;
            }

            if (!HasFrames(seat.idleFrames, 1))
            {
                valid = false;
                builder.AppendLine($"{seatLabel} requires at least one idle_*.png frame.");
            }

            if (!HasFrames(seat.eatFrames, 2))
            {
                valid = false;
                builder.AppendLine($"{seatLabel} requires at least two eat_*.png frames.");
            }

            ValidateSpriteArray(seat.idleFrames, $"{seatLabel} idle", builder, ref valid);
            ValidateSpriteArray(seat.eatFrames, $"{seatLabel} eat", builder, ref valid);
            ValidateSpriteArray(seat.talkFrames, $"{seatLabel} talk", builder, ref valid);
            ValidateSpriteArray(seat.headFrames, $"{seatLabel} head", builder, ref valid);
            ValidateSpriteArray(seat.utensilFrames, $"{seatLabel} utensil", builder, ref valid);
            ValidateSpriteArray(seat.handOverlayFrames, $"{seatLabel} hand_overlay", builder, ref valid);
        }

        ValidateOptionalSprite(tableTopProps, "table_top_props.png", builder, ref valid);
        ValidateOptionalSprite(coveredDish, "covered_dish.png", builder, ref valid);
        ValidateOptionalSprite(foodFull, "food_full.png", builder, ref valid);
        ValidateOptionalSprite(foodHalf, "food_half.png", builder, ref valid);
        ValidateOptionalSprite(foodEmpty, "food_empty.png", builder, ref valid);

        if (builder.Length == 0)
        {
            builder.Append("Layered dinner manifest is valid.");
        }

        message = builder.ToString().TrimEnd();
        return valid;
    }

    public Chapter3SeatLayerSet GetSeat(int index)
    {
        if (seats == null || index < 0 || index >= seats.Length)
        {
            return null;
        }

        return seats[index];
    }

    private bool ValidateRequiredSprite(Sprite sprite, string label, StringBuilder builder)
    {
        if (sprite == null)
        {
            builder.AppendLine($"Required layer missing: {label}.");
            return false;
        }

        bool valid = true;
        ValidateSpriteDimensions(sprite, label, builder, ref valid);
        return valid;
    }

    private void ValidateOptionalSprite(Sprite sprite, string label, StringBuilder builder, ref bool valid)
    {
        if (sprite != null)
        {
            ValidateSpriteDimensions(sprite, label, builder, ref valid);
        }
    }

    private void ValidateSpriteArray(Sprite[] frames, string label, StringBuilder builder, ref bool valid)
    {
        if (frames == null)
        {
            return;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                ValidateSpriteDimensions(frames[i], $"{label}[{i}]", builder, ref valid);
            }
        }
    }

    private void ValidateSpriteDimensions(Sprite sprite, string label, StringBuilder builder, ref bool valid)
    {
        if (sprite == null)
        {
            return;
        }

        int rectWidth = Mathf.RoundToInt(sprite.rect.width);
        int rectHeight = Mathf.RoundToInt(sprite.rect.height);

        if (rectWidth != canvasSize.x || rectHeight != canvasSize.y)
        {
            valid = false;
            builder.AppendLine($"{label} sprite rect is {rectWidth}x{rectHeight}; expected {canvasSize.x}x{canvasSize.y}.");
        }

        Texture2D texture = sprite.texture;

        if (texture != null && (texture.width != canvasSize.x || texture.height != canvasSize.y))
        {
            valid = false;
            builder.AppendLine($"{label} texture is {texture.width}x{texture.height}; expected {canvasSize.x}x{canvasSize.y} for unatlased full-canvas art.");
        }
    }

    private static bool HasFrames(IReadOnlyList<Sprite> frames, int requiredCount)
    {
        if (frames == null)
        {
            return false;
        }

        int count = 0;

        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i] != null)
            {
                count++;
            }
        }

        return count >= requiredCount;
    }

    [System.Serializable]
    public sealed class Chapter3SeatLayerSet
    {
        public string seatId;
        public Sprite[] idleFrames;
        public Sprite[] eatFrames;
        public Sprite[] talkFrames;
        public Sprite[] headFrames;
        public Sprite[] utensilFrames;
        public Sprite[] handOverlayFrames;
    }
}
