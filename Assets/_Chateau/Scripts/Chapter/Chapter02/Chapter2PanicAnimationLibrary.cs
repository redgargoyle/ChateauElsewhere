using System;
using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName = "Dreadforge/Chapter 2/Panic Animation Library", fileName = "PanicAnimationLibrary")]
public sealed class Chapter2PanicAnimationLibrary : ScriptableObject
{
    public const string ResourcesPath = "Chapter2/PanicAnimationLibrary";

    [SerializeField] private Chapter2PanicCharacterAnimation[] characters = Array.Empty<Chapter2PanicCharacterAnimation>();

    public Chapter2PanicCharacterAnimation[] Characters => characters;

    public bool TryGetCharacter(string characterId, out Chapter2PanicCharacterAnimation animation)
    {
        animation = null;

        if (string.IsNullOrWhiteSpace(characterId) || characters == null)
        {
            return false;
        }

        string cleanId = characterId.Trim();

        for (int i = 0; i < characters.Length; i++)
        {
            Chapter2PanicCharacterAnimation candidate = characters[i];

            if (candidate != null &&
                string.Equals(candidate.CharacterId, cleanId, StringComparison.OrdinalIgnoreCase))
            {
                animation = candidate;
                return true;
            }
        }

        return false;
    }

    public bool HasCompleteRoster(out string report)
    {
        StringBuilder missing = new StringBuilder();

        for (int i = 0; i < Chapter2PanicRoster.CharacterIds.Length; i++)
        {
            string characterId = Chapter2PanicRoster.CharacterIds[i];

            if (!TryGetCharacter(characterId, out Chapter2PanicCharacterAnimation animation))
            {
                AppendMissing(missing, characterId, "character entry");
                continue;
            }

            if (!animation.HasRequiredFrames(out string actionReport))
            {
                AppendMissing(missing, characterId, actionReport);
            }
        }

        report = missing.ToString();
        return report.Length == 0;
    }

    public void Configure(Chapter2PanicCharacterAnimation[] nextCharacters)
    {
        characters = nextCharacters ?? Array.Empty<Chapter2PanicCharacterAnimation>();
    }

    private static void AppendMissing(StringBuilder builder, string characterId, string detail)
    {
        if (builder.Length > 0)
        {
            builder.Append("; ");
        }

        builder.Append(characterId);
        builder.Append(": ");
        builder.Append(detail);
    }
}

[Serializable]
public sealed class Chapter2PanicCharacterAnimation
{
    [SerializeField] private string characterId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite[] panicHandsUp = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] panicPop = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] panicRunDown = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] panicRunLeft = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] panicRunRight = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] panicRunUp = Array.Empty<Sprite>();

    public string CharacterId => characterId;
    public string DisplayName => displayName;
    public Sprite[] PanicHandsUp => panicHandsUp;
    public Sprite[] PanicPop => panicPop;
    public Sprite[] PanicRunDown => panicRunDown;
    public Sprite[] PanicRunLeft => panicRunLeft;
    public Sprite[] PanicRunRight => panicRunRight;
    public Sprite[] PanicRunUp => panicRunUp;

    public void Configure(
        string nextCharacterId,
        string nextDisplayName,
        Sprite[] nextPanicHandsUp,
        Sprite[] nextPanicPop,
        Sprite[] nextPanicRunDown,
        Sprite[] nextPanicRunLeft,
        Sprite[] nextPanicRunRight,
        Sprite[] nextPanicRunUp)
    {
        characterId = nextCharacterId;
        displayName = nextDisplayName;
        panicHandsUp = nextPanicHandsUp ?? Array.Empty<Sprite>();
        panicPop = nextPanicPop ?? Array.Empty<Sprite>();
        panicRunDown = nextPanicRunDown ?? Array.Empty<Sprite>();
        panicRunLeft = nextPanicRunLeft ?? Array.Empty<Sprite>();
        panicRunRight = nextPanicRunRight ?? Array.Empty<Sprite>();
        panicRunUp = nextPanicRunUp ?? Array.Empty<Sprite>();
    }

    public bool HasRequiredFrames(out string report)
    {
        StringBuilder missing = new StringBuilder();
        AppendCountMismatch(missing, "panic_hands_up", panicHandsUp, 4);
        AppendCountMismatch(missing, "panic_pop", panicPop, 8);
        AppendCountMismatch(missing, "panic_run_down", panicRunDown, 4);
        AppendCountMismatch(missing, "panic_run_left", panicRunLeft, 4);
        AppendCountMismatch(missing, "panic_run_right", panicRunRight, 4);
        AppendCountMismatch(missing, "panic_run_up", panicRunUp, 4);
        report = missing.ToString();
        return report.Length == 0;
    }

    private static void AppendCountMismatch(StringBuilder builder, string actionId, Sprite[] sprites, int expectedCount)
    {
        int actualCount = sprites != null ? sprites.Length : 0;

        if (actualCount == expectedCount)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(", ");
        }

        builder.Append(actionId);
        builder.Append(" expected ");
        builder.Append(expectedCount);
        builder.Append(" got ");
        builder.Append(actualCount);
    }
}

public static class Chapter2PanicRoster
{
    public static readonly string[] CharacterIds =
    {
        "Lady",
        "ButlerGuest",
        "MisterFlorianKnell",
        "CountessElowenDusk",
        "BaronHectorGlass",
        "LadySabineMarrow",
        "LordAmbroseVeil",
        "MadameCoralieThread",
    };

    public static readonly string[] DisplayNames =
    {
        "Ava",
        "Marcus",
        "Mister Florian Knell",
        "Countess Elowen Dusk",
        "Baron Hector Glass",
        "Lady Sabine Marrow",
        "Lord Ambrose Veil",
        "Madame Coralie Thread",
    };

    public static bool TryGetCharacterIdForGuestNumber(int guestNumber, out string characterId)
    {
        characterId = string.Empty;

        if (guestNumber < 1 || guestNumber > CharacterIds.Length)
        {
            return false;
        }

        characterId = CharacterIds[guestNumber - 1];
        return true;
    }
}
