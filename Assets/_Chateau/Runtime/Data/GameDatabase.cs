using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using CanonicalPassageDefinition = Chateau.World.Rooms.Passages.PassageDefinition;
using CanonicalRoomDefinition = Chateau.World.Rooms.RoomDefinition;

namespace Chateau.Architecture
{
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Chateau/Architecture/Game Database")]
    public sealed class GameDatabase : ScriptableObject, IArchitectureValidatable
    {
        [SerializeField] private List<DefinitionAssetBase> definitions = new List<DefinitionAssetBase>();

        [NonSerialized] private bool indexesAreCurrent;
        [NonSerialized] private bool indexedDefinitionsWereNull;
        [NonSerialized] private DefinitionAssetBase[] indexedDefinitionReferences;
        [NonSerialized] private string[] indexedStableIds;
        [NonSerialized] private ReadOnlyCollection<CanonicalRoomDefinition> roomDefinitions;
        [NonSerialized] private ReadOnlyCollection<CanonicalPassageDefinition> passageDefinitions;
        [NonSerialized] private Dictionary<RoomId, CanonicalRoomDefinition> roomsById;
        [NonSerialized] private Dictionary<PassageId, CanonicalPassageDefinition> passagesById;
        [NonSerialized] private HashSet<RoomId> duplicateRoomIds;
        [NonSerialized] private HashSet<PassageId> duplicatePassageIds;

        public IReadOnlyList<DefinitionAssetBase> Definitions => definitions;
        public IReadOnlyList<CanonicalRoomDefinition> RoomDefinitions
        {
            get
            {
                EnsureIndexes();
                return roomDefinitions;
            }
        }

        public IReadOnlyList<CanonicalPassageDefinition> PassageDefinitions
        {
            get
            {
                EnsureIndexes();
                return passageDefinitions;
            }
        }

        public bool TryGetRoomDefinition(RoomId id, out CanonicalRoomDefinition definition)
        {
            definition = null;

            if (!id.IsValid)
            {
                return false;
            }

            EnsureIndexes();
            return !duplicateRoomIds.Contains(id) && roomsById.TryGetValue(id, out definition);
        }

        public bool TryGetPassageDefinition(PassageId id, out CanonicalPassageDefinition definition)
        {
            definition = null;

            if (!id.IsValid)
            {
                return false;
            }

            EnsureIndexes();
            return !duplicatePassageIds.Contains(id) && passagesById.TryGetValue(id, out definition);
        }

        private void OnEnable()
        {
            InvalidateIndexes();
            RebuildIndexes();
        }

        private void OnValidate()
        {
            InvalidateIndexes();
            RebuildIndexes();
        }

        public void ValidateConfiguration(ValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            RebuildIndexes();

            if (definitions == null)
            {
                report.AddError("GameDatabase definitions list is null.", this);
                return;
            }

            Dictionary<string, DefinitionAssetBase> byId = new Dictionary<string, DefinitionAssetBase>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, CanonicalRoomDefinition> roomAliases =
                new Dictionary<string, CanonicalRoomDefinition>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> conflictingRoomAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<DefinitionAssetBase> registeredDefinitions = new HashSet<DefinitionAssetBase>();

            for (int i = 0; i < definitions.Count; i++)
            {
                DefinitionAssetBase definition = definitions[i];

                if (definition == null)
                {
                    report.AddError($"GameDatabase definition slot {i} is null.", this);
                    continue;
                }

                registeredDefinitions.Add(definition);
                definition.ValidateConfiguration(report);

                if (definition is CanonicalRoomDefinition roomDefinition)
                {
                    RegisterRoomAlias(
                        roomDefinition.DisplayName,
                        roomDefinition,
                        roomAliases,
                        conflictingRoomAliases,
                        report);

                    IReadOnlyList<string> legacyNames = roomDefinition.LegacyNames;

                    for (int legacyIndex = 0; legacyIndex < legacyNames.Count; legacyIndex++)
                    {
                        RegisterRoomAlias(
                            legacyNames[legacyIndex],
                            roomDefinition,
                            roomAliases,
                            conflictingRoomAliases,
                            report);
                    }
                }

                string stableId = definition.StableId;

                if (string.IsNullOrWhiteSpace(stableId))
                {
                    continue;
                }

                if (byId.TryGetValue(stableId, out DefinitionAssetBase existing))
                {
                    report.AddError($"Duplicate definition ID '{stableId}' on '{existing.name}' and '{definition.name}'.", definition);
                }
                else
                {
                    byId.Add(stableId, definition);
                }
            }

            for (int i = 0; i < passageDefinitions.Count; i++)
            {
                CanonicalPassageDefinition passageDefinition = passageDefinitions[i];

                RequireRegisteredDefinition(
                    passageDefinition,
                    passageDefinition.SourceRoom,
                    "source room",
                    registeredDefinitions,
                    report);
                RequireRegisteredDefinition(
                    passageDefinition,
                    passageDefinition.DestinationRoom,
                    "destination room",
                    registeredDefinitions,
                    report);
                RequireRegisteredDefinition(
                    passageDefinition,
                    passageDefinition.Reverse,
                    "reverse passage",
                    registeredDefinitions,
                    report);
            }
        }

        private void EnsureIndexes()
        {
            if (!indexesAreCurrent || IndexedSourcesChanged())
            {
                RebuildIndexes();
            }
        }

        private void InvalidateIndexes()
        {
            indexesAreCurrent = false;
        }

        private void RebuildIndexes()
        {
            List<CanonicalRoomDefinition> rebuiltRoomDefinitions = new List<CanonicalRoomDefinition>();
            List<CanonicalPassageDefinition> rebuiltPassageDefinitions =
                new List<CanonicalPassageDefinition>();
            roomsById = new Dictionary<RoomId, CanonicalRoomDefinition>();
            passagesById = new Dictionary<PassageId, CanonicalPassageDefinition>();
            duplicateRoomIds = new HashSet<RoomId>();
            duplicatePassageIds = new HashSet<PassageId>();
            indexedDefinitionsWereNull = definitions == null;
            int definitionCount = definitions == null ? 0 : definitions.Count;
            indexedDefinitionReferences = new DefinitionAssetBase[definitionCount];
            indexedStableIds = new string[definitionCount];

            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    DefinitionAssetBase definition = definitions[i];
                    indexedDefinitionReferences[i] = definition;
                    indexedStableIds[i] = definition == null ? string.Empty : definition.StableId;

                    if (definition is CanonicalRoomDefinition roomDefinition)
                    {
                        rebuiltRoomDefinitions.Add(roomDefinition);

                        if (roomDefinition.TryGetId(out RoomId roomId))
                        {
                            AddFailClosed(
                                roomId,
                                roomDefinition,
                                roomsById,
                                duplicateRoomIds);
                        }
                    }
                    else if (definition is CanonicalPassageDefinition passageDefinition)
                    {
                        rebuiltPassageDefinitions.Add(passageDefinition);

                        if (passageDefinition.TryGetId(out PassageId passageId))
                        {
                            AddFailClosed(
                                passageId,
                                passageDefinition,
                                passagesById,
                                duplicatePassageIds);
                        }
                    }
                }
            }

            roomDefinitions = rebuiltRoomDefinitions.AsReadOnly();
            passageDefinitions = rebuiltPassageDefinitions.AsReadOnly();
            indexesAreCurrent = true;
        }

        private bool IndexedSourcesChanged()
        {
            if (indexedDefinitionsWereNull != (definitions == null))
            {
                return true;
            }

            int definitionCount = definitions == null ? 0 : definitions.Count;

            if (indexedDefinitionReferences == null ||
                indexedStableIds == null ||
                indexedDefinitionReferences.Length != definitionCount ||
                indexedStableIds.Length != definitionCount)
            {
                return true;
            }

            for (int i = 0; i < definitionCount; i++)
            {
                DefinitionAssetBase definition = definitions[i];
                string stableId = definition == null ? string.Empty : definition.StableId;

                if (!ReferenceEquals(indexedDefinitionReferences[i], definition) ||
                    !string.Equals(indexedStableIds[i], stableId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddFailClosed<TId, TDefinition>(
            TId id,
            TDefinition definition,
            Dictionary<TId, TDefinition> index,
            HashSet<TId> duplicates)
        {
            if (duplicates.Contains(id))
            {
                return;
            }

            if (index.ContainsKey(id))
            {
                index.Remove(id);
                duplicates.Add(id);
                return;
            }

            index.Add(id, definition);
        }

        private static void RegisterRoomAlias(
            string rawAlias,
            CanonicalRoomDefinition roomDefinition,
            Dictionary<string, CanonicalRoomDefinition> roomAliases,
            HashSet<string> conflictingRoomAliases,
            ValidationReport report)
        {
            string alias = string.IsNullOrWhiteSpace(rawAlias) ? string.Empty : rawAlias.Trim();

            if (string.IsNullOrEmpty(alias) || conflictingRoomAliases.Contains(alias))
            {
                return;
            }

            if (!roomAliases.TryGetValue(alias, out CanonicalRoomDefinition existing))
            {
                roomAliases.Add(alias, roomDefinition);
                return;
            }

            if (existing == roomDefinition)
            {
                return;
            }

            conflictingRoomAliases.Add(alias);
            report.AddError(
                $"Room alias '{alias}' is shared by RoomDefinitions '{existing.name}' and '{roomDefinition.name}'.",
                roomDefinition);
        }

        private static void RequireRegisteredDefinition(
            CanonicalPassageDefinition owner,
            DefinitionAssetBase referencedDefinition,
            string role,
            HashSet<DefinitionAssetBase> registeredDefinitions,
            ValidationReport report)
        {
            if (referencedDefinition != null && !registeredDefinitions.Contains(referencedDefinition))
            {
                report.AddError(
                    $"PassageDefinition '{owner.name}' {role} '{referencedDefinition.name}' is not registered in GameDatabase.",
                    owner);
            }
        }
    }
}
