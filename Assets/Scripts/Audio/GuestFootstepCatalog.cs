using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GuestFootstepCatalog", menuName = "Dreadforge/Audio/Guest Footstep Catalog")]
public sealed class GuestFootstepCatalog : ScriptableObject
{
#pragma warning disable 0649
    [Serializable]
    private struct GuestFootstepAssignment
    {
        public int guestNumber;
        public AudioClip clip;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume;
        [Min(0.05f)] public float stepIntervalSeconds;
    }
#pragma warning restore 0649

    [SerializeField] private GuestFootstepAssignment[] assignments = Array.Empty<GuestFootstepAssignment>();
    [SerializeField] private AudioClip butlerClip;
    [SerializeField] private AudioClip[] butlerClips = Array.Empty<AudioClip>();
    [SerializeField, Range(0f, 1f)] private float butlerVolume = 0.24f;
    [SerializeField, Range(0f, 1f)] private float defaultVolume = 0.28f;
    [SerializeField, Min(0.05f)] private float guestStepIntervalSeconds = 0.54f;
    [SerializeField, Min(0.05f)] private float butlerStepIntervalSeconds = 0.6f;
    [SerializeField, Min(0f)] private float stepIntervalJitterSeconds = 0.025f;
    [SerializeField, Min(10f)] private float highPassCutoffFrequency = 180f;
    [SerializeField, Range(0.1f, 10f)] private float highPassResonanceQ = 1.1f;
    [SerializeField, Min(10f)] private float lowPassCutoffFrequency = 9000f;

    public bool TryGetFootstepsForGuest(
        int guestNumber,
        out AudioClip clip,
        out float volume,
        out float cutoffFrequency,
        out float resonanceQ)
    {
        bool found = TryGetFootstepVariantsForGuest(
            guestNumber,
            out AudioClip[] clips,
            out volume,
            out cutoffFrequency,
            out resonanceQ,
            out _,
            out _);
        clip = clips != null && clips.Length > 0 ? clips[0] : null;
        return found;
    }

    public bool TryGetFootstepVariantsForGuest(
        int guestNumber,
        out AudioClip[] clips,
        out float volume,
        out float cutoffFrequency,
        out float resonanceQ,
        out float stepInterval,
        out float stepJitter)
    {
        clips = Array.Empty<AudioClip>();
        volume = defaultVolume;
        cutoffFrequency = Mathf.Max(10f, highPassCutoffFrequency);
        resonanceQ = Mathf.Clamp(highPassResonanceQ, 0.1f, 10f);
        stepInterval = Mathf.Max(0.05f, guestStepIntervalSeconds);
        stepJitter = Mathf.Max(0f, stepIntervalJitterSeconds);

        if (assignments == null || guestNumber <= 0)
        {
            return false;
        }

        for (int i = 0; i < assignments.Length; i++)
        {
            GuestFootstepAssignment assignment = assignments[i];

            if (assignment.guestNumber != guestNumber)
            {
                continue;
            }

            clips = BuildClipList(assignment.clip, assignment.clips);
            if (clips.Length == 0)
            {
                return false;
            }

            volume = assignment.volume > 0f ? Mathf.Clamp01(assignment.volume) : defaultVolume;
            stepInterval = assignment.stepIntervalSeconds > 0f
                ? Mathf.Max(0.05f, assignment.stepIntervalSeconds)
                : Mathf.Max(0.05f, guestStepIntervalSeconds);
            return true;
        }

        return false;
    }

    public bool TryGetFootstepsForButler(
        out AudioClip clip,
        out float volume,
        out float cutoffFrequency,
        out float resonanceQ,
        out float lowPassFrequency)
    {
        bool found = TryGetButlerFootstepVariants(
            out AudioClip[] clips,
            out volume,
            out cutoffFrequency,
            out resonanceQ,
            out lowPassFrequency,
            out _,
            out _);
        clip = clips != null && clips.Length > 0 ? clips[0] : null;
        return found;
    }

    public bool TryGetButlerFootstepVariants(
        out AudioClip[] clips,
        out float volume,
        out float cutoffFrequency,
        out float resonanceQ,
        out float lowPassFrequency,
        out float stepInterval,
        out float stepJitter)
    {
        clips = BuildClipList(butlerClip, butlerClips);
        volume = butlerVolume > 0f ? Mathf.Clamp01(butlerVolume) : defaultVolume;
        cutoffFrequency = Mathf.Max(10f, highPassCutoffFrequency);
        resonanceQ = Mathf.Clamp(highPassResonanceQ, 0.1f, 10f);
        lowPassFrequency = Mathf.Max(10f, lowPassCutoffFrequency);
        stepInterval = Mathf.Max(0.05f, butlerStepIntervalSeconds);
        stepJitter = Mathf.Max(0f, stepIntervalJitterSeconds);
        return clips.Length > 0;
    }

    private static AudioClip[] BuildClipList(AudioClip legacyClip, AudioClip[] variants)
    {
        List<AudioClip> validClips = new List<AudioClip>();
        if (variants != null)
        {
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] != null && !validClips.Contains(variants[i]))
                {
                    validClips.Add(variants[i]);
                }
            }
        }

        if (legacyClip != null && !validClips.Contains(legacyClip))
        {
            validClips.Add(legacyClip);
        }

        return validClips.ToArray();
    }
}
