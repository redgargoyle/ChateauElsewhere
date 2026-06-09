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
    [SerializeField] private Sprite[] panicReactionDown = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] panicShriekDown = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] panicRunLeft = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] panicRunRight = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] panicTurnaround = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] coverFaceCower = Array.Empty<Sprite>();

    public string CharacterId => characterId;
    public string DisplayName => displayName;
    public Sprite[] PanicReactionDown => panicReactionDown;
    public Sprite[] PanicShriekDown => panicShriekDown;
    public Sprite[] PanicRunLeft => panicRunLeft;
    public Sprite[] PanicRunRight => panicRunRight;
    public Sprite[] PanicTurnaround => panicTurnaround;
    public Sprite[] CoverFaceCower => coverFaceCower;

    public void Configure(
        string nextCharacterId,
        string nextDisplayName,
        Sprite[] nextPanicReactionDown,
        Sprite[] nextPanicShriekDown,
        Sprite[] nextPanicRunLeft,
        Sprite[] nextPanicRunRight,
        Sprite[] nextPanicTurnaround,
        Sprite[] nextCoverFaceCower)
    {
        characterId = nextCharacterId;
        displayName = nextDisplayName;
        panicReactionDown = nextPanicReactionDown ?? Array.Empty<Sprite>();
        panicShriekDown = nextPanicShriekDown ?? Array.Empty<Sprite>();
        panicRunLeft = nextPanicRunLeft ?? Array.Empty<Sprite>();
        panicRunRight = nextPanicRunRight ?? Array.Empty<Sprite>();
        panicTurnaround = nextPanicTurnaround ?? Array.Empty<Sprite>();
        coverFaceCower = nextCoverFaceCower ?? Array.Empty<Sprite>();
    }

    public bool HasRequiredFrames(out string report)
    {
        StringBuilder missing = new StringBuilder();
        AppendCountMismatch(missing, "panic_reaction_down", panicReactionDown, 6);
        AppendCountMismatch(missing, "panic_shriek_down", panicShriekDown, 8);
        AppendCountMismatch(missing, "panic_run_left", panicRunLeft, 8);
        AppendCountMismatch(missing, "panic_run_right", panicRunRight, 8);
        AppendCountMismatch(missing, "panic_turnaround", panicTurnaround, 6);
        AppendCountMismatch(missing, "cover_face_cower", coverFaceCower, 6);
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
        "Lady",
        "Butler Guest",
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
