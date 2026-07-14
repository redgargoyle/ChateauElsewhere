using System;
using Chateau.Architecture;
using Chateau.World.Rooms;
using UnityEngine;

namespace Chateau.World.Rooms.Passages
{
    [CreateAssetMenu(fileName = "PassageDefinition", menuName = "Chateau/World/Passage Definition")]
    public sealed class PassageDefinition : DefinitionAssetBase
    {
        [SerializeField] private RoomDefinition sourceRoom;
        [SerializeField] private RoomDefinition destinationRoom;
        [SerializeField] private PassageDefinition reverse;
        [SerializeField] private PassageKind kind = PassageKind.Door;
        [SerializeField] private string promptText = "Open Door";
        [SerializeField] private string legacyDoorId = string.Empty;

        public RoomDefinition SourceRoom => sourceRoom;
        public RoomDefinition DestinationRoom => destinationRoom;
        public PassageDefinition Reverse => reverse;
        public PassageKind Kind => kind;
        public string PromptText => Clean(promptText);
        public string LegacyDoorId => Clean(legacyDoorId);
        public PassageId Id => PassageId.Parse(StableId);

        public bool TryGetId(out PassageId id)
        {
            return PassageId.TryParse(StableId, out id);
        }

        public override void ValidateConfiguration(ValidationReport report)
        {
            base.ValidateConfiguration(report);

            if (!string.IsNullOrWhiteSpace(StableId) &&
                !StableId.StartsWith("passage.", StringComparison.Ordinal))
            {
                report.AddError($"PassageDefinition '{name}' stable ID must start with 'passage.'.", this);
            }
            else if (!string.IsNullOrWhiteSpace(StableId) && !TryGetId(out _))
            {
                report.AddError(
                    $"PassageDefinition '{name}' stable ID must be a canonical lowercase PassageId.",
                    this);
            }

            if (sourceRoom == null)
            {
                report.AddError($"PassageDefinition '{name}' requires a source room.", this);
            }

            if (destinationRoom == null)
            {
                report.AddError($"PassageDefinition '{name}' requires a destination room.", this);
            }
            else if (destinationRoom == sourceRoom)
            {
                report.AddError($"PassageDefinition '{name}' source and destination rooms must differ.", this);
            }

            if (!Enum.IsDefined(typeof(PassageKind), kind))
            {
                report.AddError($"PassageDefinition '{name}' has an unknown passage kind.", this);
            }

            if (string.IsNullOrWhiteSpace(PromptText))
            {
                report.AddError($"PassageDefinition '{name}' requires prompt text.", this);
            }

            if (reverse == null)
            {
                report.AddError($"PassageDefinition '{name}' requires a reverse passage.", this);
                return;
            }

            if (reverse == this)
            {
                report.AddError($"PassageDefinition '{name}' cannot reverse to itself.", this);
                return;
            }

            if (reverse.reverse != this)
            {
                report.AddError($"PassageDefinition '{name}' reverse passage must link back to it.", this);
            }

            if (sourceRoom != null && destinationRoom != null &&
                (reverse.sourceRoom != destinationRoom || reverse.destinationRoom != sourceRoom))
            {
                report.AddError($"PassageDefinition '{name}' reverse passage must swap its room endpoints.", this);
            }
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
