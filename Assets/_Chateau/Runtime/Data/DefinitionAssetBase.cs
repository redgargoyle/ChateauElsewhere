using System;
using UnityEngine;

namespace Chateau.Architecture
{
    public abstract class DefinitionAssetBase : ScriptableObject, IArchitectureValidatable
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField, Min(1)] private int schemaVersion = 1;

        public string StableId => stableId == null ? string.Empty : stableId.Trim();
        public int SchemaVersion => Mathf.Max(1, schemaVersion);

        public virtual void ValidateConfiguration(ValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (string.IsNullOrWhiteSpace(StableId))
            {
                report.AddError($"{GetType().Name} asset '{name}' has no stable ID.", this);
            }
        }
    }
}
