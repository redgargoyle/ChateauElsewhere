using System;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class IdeaDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [TextArea(2, 5)]
    [SerializeField] private string premise;
    [SerializeField] private Color tint = Color.clear;
    [Range(0f, 1f)]
    [SerializeField] private float tintStrength = 0.18f;
    [SerializeField] private bool elsewhere;

    public string Id => NormalizeId(id);
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
    public string Premise => premise ?? string.Empty;
    public Color Tint => tint;
    public float TintStrength => Mathf.Clamp01(tintStrength);
    public bool IsElsewhere => elsewhere;

    public IdeaDefinition(string id, string displayName, string premise, Color tint, float tintStrength, bool elsewhere = false)
    {
        this.id = NormalizeId(id);
        this.displayName = string.IsNullOrWhiteSpace(displayName) ? this.id : displayName.Trim();
        this.premise = premise ?? string.Empty;
        this.tint = tint;
        this.tintStrength = Mathf.Clamp01(tintStrength);
        this.elsewhere = elsewhere;
    }

    public static string NormalizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        bool wroteSeparator = false;
        string trimmed = value.Trim().ToLowerInvariant();

        for (int i = 0; i < trimmed.Length; i++)
        {
            char character = trimmed[i];

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                wroteSeparator = false;
                continue;
            }

            if (character == '_' || character == '-' || char.IsWhiteSpace(character))
            {
                if (builder.Length > 0 && !wroteSeparator)
                {
                    builder.Append('_');
                    wroteSeparator = true;
                }
            }
        }

        if (builder.Length > 0 && builder[builder.Length - 1] == '_')
        {
            builder.Length--;
        }

        return builder.ToString();
    }
}
