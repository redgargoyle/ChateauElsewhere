using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chateau.Architecture
{
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Chateau/Architecture/Game Database")]
    public sealed class GameDatabase : ScriptableObject, IArchitectureValidatable
    {
        [SerializeField] private List<DefinitionAssetBase> definitions = new List<DefinitionAssetBase>();

        public IReadOnlyList<DefinitionAssetBase> Definitions => definitions;

        public void ValidateConfiguration(ValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            Dictionary<string, DefinitionAssetBase> byId = new Dictionary<string, DefinitionAssetBase>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < definitions.Count; i++)
            {
                DefinitionAssetBase definition = definitions[i];

                if (definition == null)
                {
                    report.AddError($"GameDatabase definition slot {i} is null.", this);
                    continue;
                }

                definition.ValidateConfiguration(report);
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
        }
    }
}
