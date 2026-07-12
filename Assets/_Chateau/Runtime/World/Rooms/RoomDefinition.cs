using System;
using System.Collections.Generic;
using Chateau.Architecture;
using UnityEngine;

namespace Chateau.World.Rooms
{
    [CreateAssetMenu(fileName = "RoomDefinition", menuName = "Chateau/World/Room Definition")]
    public sealed class RoomDefinition : DefinitionAssetBase
    {
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string[] legacyNames = Array.Empty<string>();
        [SerializeField] private Texture backgroundTexture;
        [SerializeField] private global::RoomPerspectiveProfile perspectiveProfile;

        public string DisplayName => Clean(displayName);
        public IReadOnlyList<string> LegacyNames => legacyNames ?? Array.Empty<string>();
        public Texture BackgroundTexture => backgroundTexture;
        public global::RoomPerspectiveProfile PerspectiveProfile => perspectiveProfile;

        public string PrimaryLegacyName
        {
            get
            {
                string[] names = legacyNames ?? Array.Empty<string>();

                for (int i = 0; i < names.Length; i++)
                {
                    string legacyName = Clean(names[i]);

                    if (!string.IsNullOrEmpty(legacyName))
                    {
                        return legacyName;
                    }
                }

                return DisplayName;
            }
        }

        public bool MatchesLegacyName(string value)
        {
            string candidate = Clean(value);

            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            if (SameName(candidate, DisplayName))
            {
                return true;
            }

            string[] names = legacyNames ?? Array.Empty<string>();

            for (int i = 0; i < names.Length; i++)
            {
                if (SameName(candidate, Clean(names[i])))
                {
                    return true;
                }
            }

            return false;
        }

        public override void ValidateConfiguration(ValidationReport report)
        {
            base.ValidateConfiguration(report);

            if (!string.IsNullOrWhiteSpace(StableId) &&
                !StableId.StartsWith("room.", StringComparison.Ordinal))
            {
                report.AddError($"RoomDefinition '{name}' stable ID must start with 'room.'.", this);
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                report.AddError($"RoomDefinition '{name}' has no display name.", this);
            }

            if (backgroundTexture == null)
            {
                report.AddError($"RoomDefinition '{name}' requires a background texture.", this);
            }

            HashSet<string> uniqueLegacyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] names = legacyNames ?? Array.Empty<string>();

            for (int i = 0; i < names.Length; i++)
            {
                string legacyName = Clean(names[i]);

                if (string.IsNullOrEmpty(legacyName))
                {
                    report.AddError($"RoomDefinition '{name}' legacy-name slot {i} is empty.", this);
                }
                else if (!uniqueLegacyNames.Add(legacyName))
                {
                    report.AddError($"RoomDefinition '{name}' repeats legacy name '{legacyName}'.", this);
                }
            }
        }

        private static bool SameName(string left, string right)
        {
            return !string.IsNullOrEmpty(left) &&
                !string.IsNullOrEmpty(right) &&
                string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
