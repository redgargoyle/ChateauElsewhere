using System;
using UnityEngine;

namespace Chateau.Architecture
{
    internal static class StableIdRules
    {
        public static bool TryNormalize(string raw, string requiredPrefix, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();

            if (normalized.Length <= requiredPrefix.Length ||
                !normalized.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                normalized = string.Empty;
                return false;
            }

            bool previousWasSeparator = false;

            for (int i = 0; i < normalized.Length; i++)
            {
                char character = normalized[i];
                bool isLowerAscii = character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';
                bool isSeparator = character == '.' || character == '-' || character == '_';

                if (!isLowerAscii && !isDigit && !isSeparator)
                {
                    normalized = string.Empty;
                    return false;
                }

                if (i == 0 && !isLowerAscii)
                {
                    normalized = string.Empty;
                    return false;
                }

                if (isSeparator && (previousWasSeparator || i == normalized.Length - 1))
                {
                    normalized = string.Empty;
                    return false;
                }

                previousWasSeparator = isSeparator;
            }

            return true;
        }

        public static string Parse(string raw, string requiredPrefix, string typeName)
        {
            if (TryNormalize(raw, requiredPrefix, out string normalized))
            {
                return normalized;
            }

            throw new ArgumentException(
                $"{typeName} must begin with '{requiredPrefix}' and contain only canonical lowercase stable-ID characters.",
                nameof(raw));
        }

        public static bool IsCanonical(string value, string requiredPrefix)
        {
            return TryNormalize(value, requiredPrefix, out string normalized) &&
                string.Equals(value, normalized, StringComparison.Ordinal);
        }

        public static int GetOrdinalHashCode(string value)
        {
            return StringComparer.Ordinal.GetHashCode(value ?? string.Empty);
        }
    }

    [Serializable]
    public struct RoomId : IEquatable<RoomId>
    {
        public const string RequiredPrefix = "room.";

        [SerializeField] private string value;

        private RoomId(string normalizedValue)
        {
            value = normalizedValue;
        }

        public string Value => value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(value);
        public bool IsValid => StableIdRules.IsCanonical(value, RequiredPrefix);

        public static RoomId Parse(string raw)
        {
            return new RoomId(StableIdRules.Parse(raw, RequiredPrefix, nameof(RoomId)));
        }

        public static bool TryParse(string raw, out RoomId result)
        {
            if (StableIdRules.TryNormalize(raw, RequiredPrefix, out string normalized))
            {
                result = new RoomId(normalized);
                return true;
            }

            result = default;
            return false;
        }

        public bool Equals(RoomId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RoomId other && Equals(other);
        public override int GetHashCode() => StableIdRules.GetOrdinalHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(RoomId left, RoomId right) => left.Equals(right);
        public static bool operator !=(RoomId left, RoomId right) => !left.Equals(right);
    }

    [Serializable]
    public struct PassageId : IEquatable<PassageId>
    {
        public const string RequiredPrefix = "passage.";

        [SerializeField] private string value;

        private PassageId(string normalizedValue)
        {
            value = normalizedValue;
        }

        public string Value => value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(value);
        public bool IsValid => StableIdRules.IsCanonical(value, RequiredPrefix);

        public static PassageId Parse(string raw)
        {
            return new PassageId(StableIdRules.Parse(raw, RequiredPrefix, nameof(PassageId)));
        }

        public static bool TryParse(string raw, out PassageId result)
        {
            if (StableIdRules.TryNormalize(raw, RequiredPrefix, out string normalized))
            {
                result = new PassageId(normalized);
                return true;
            }

            result = default;
            return false;
        }

        public bool Equals(PassageId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PassageId other && Equals(other);
        public override int GetHashCode() => StableIdRules.GetOrdinalHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(PassageId left, PassageId right) => left.Equals(right);
        public static bool operator !=(PassageId left, PassageId right) => !left.Equals(right);
    }

    [Serializable]
    public struct ActorId : IEquatable<ActorId>
    {
        public const string RequiredPrefix = "actor.";

        [SerializeField] private string value;

        private ActorId(string normalizedValue)
        {
            value = normalizedValue;
        }

        public string Value => value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(value);
        public bool IsValid => StableIdRules.IsCanonical(value, RequiredPrefix);

        public static ActorId Parse(string raw)
        {
            return new ActorId(StableIdRules.Parse(raw, RequiredPrefix, nameof(ActorId)));
        }

        public static bool TryParse(string raw, out ActorId result)
        {
            if (StableIdRules.TryNormalize(raw, RequiredPrefix, out string normalized))
            {
                result = new ActorId(normalized);
                return true;
            }

            result = default;
            return false;
        }

        public bool Equals(ActorId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ActorId other && Equals(other);
        public override int GetHashCode() => StableIdRules.GetOrdinalHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(ActorId left, ActorId right) => left.Equals(right);
        public static bool operator !=(ActorId left, ActorId right) => !left.Equals(right);
    }

    [Serializable]
    public struct ChapterId : IEquatable<ChapterId>
    {
        public const string RequiredPrefix = "chapter_";

        [SerializeField] private string value;

        private ChapterId(string normalizedValue)
        {
            value = normalizedValue;
        }

        public string Value => value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(value);
        public bool IsValid => StableIdRules.IsCanonical(value, RequiredPrefix);

        public static ChapterId Parse(string raw)
        {
            return new ChapterId(StableIdRules.Parse(raw, RequiredPrefix, nameof(ChapterId)));
        }

        public static bool TryParse(string raw, out ChapterId result)
        {
            if (StableIdRules.TryNormalize(raw, RequiredPrefix, out string normalized))
            {
                result = new ChapterId(normalized);
                return true;
            }

            result = default;
            return false;
        }

        public bool Equals(ChapterId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ChapterId other && Equals(other);
        public override int GetHashCode() => StableIdRules.GetOrdinalHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(ChapterId left, ChapterId right) => left.Equals(right);
        public static bool operator !=(ChapterId left, ChapterId right) => !left.Equals(right);
    }

    [Serializable]
    public struct BeatId : IEquatable<BeatId>
    {
        public const string RequiredPrefix = "beat.";

        [SerializeField] private string value;

        private BeatId(string normalizedValue)
        {
            value = normalizedValue;
        }

        public string Value => value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(value);
        public bool IsValid => StableIdRules.IsCanonical(value, RequiredPrefix);

        public static BeatId Parse(string raw)
        {
            return new BeatId(StableIdRules.Parse(raw, RequiredPrefix, nameof(BeatId)));
        }

        public static bool TryParse(string raw, out BeatId result)
        {
            if (StableIdRules.TryNormalize(raw, RequiredPrefix, out string normalized))
            {
                result = new BeatId(normalized);
                return true;
            }

            result = default;
            return false;
        }

        public bool Equals(BeatId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is BeatId other && Equals(other);
        public override int GetHashCode() => StableIdRules.GetOrdinalHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(BeatId left, BeatId right) => left.Equals(right);
        public static bool operator !=(BeatId left, BeatId right) => !left.Equals(right);
    }

    [Serializable]
    public struct ObjectiveId : IEquatable<ObjectiveId>
    {
        public const string RequiredPrefix = "objective.";

        [SerializeField] private string value;

        private ObjectiveId(string normalizedValue)
        {
            value = normalizedValue;
        }

        public string Value => value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(value);
        public bool IsValid => StableIdRules.IsCanonical(value, RequiredPrefix);

        public static ObjectiveId Parse(string raw)
        {
            return new ObjectiveId(StableIdRules.Parse(raw, RequiredPrefix, nameof(ObjectiveId)));
        }

        public static bool TryParse(string raw, out ObjectiveId result)
        {
            if (StableIdRules.TryNormalize(raw, RequiredPrefix, out string normalized))
            {
                result = new ObjectiveId(normalized);
                return true;
            }

            result = default;
            return false;
        }

        public bool Equals(ObjectiveId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ObjectiveId other && Equals(other);
        public override int GetHashCode() => StableIdRules.GetOrdinalHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(ObjectiveId left, ObjectiveId right) => left.Equals(right);
        public static bool operator !=(ObjectiveId left, ObjectiveId right) => !left.Equals(right);
    }
}
