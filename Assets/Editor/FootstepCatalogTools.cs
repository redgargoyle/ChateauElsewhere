using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FootstepCatalogTools
{
    private const string CatalogPath = "Assets/Resources/Audio/GuestFootstepCatalog.asset";
    private const string ButlerFolder = "Assets/Audio/SFX/Footsteps/Wood/Butler";
    private const string GuestFolder = "Assets/Audio/SFX/Footsteps/Wood/Guest";
    private const string PreviewFolder = "Assets/Audio/SFX/Footsteps/Preview";
    private const int RequiredButlerCount = 6;
    private const int RequiredGuestCount = 8;
    private const int SampleRate = 44100;

    private static readonly string[] PreviewFiles =
    {
        "Preview_Butler_Walk_0p60s.wav",
        "Preview_Guest_Walk_0p54s.wav",
        "Preview_FastWalk_0p42s.wav",
        "Preview_GuestPair_Walk_0p54s_offset_0p12s.wav",
        "Preview_FourGuests_Walk_0p54s_offsets.wav"
    };

    [MenuItem("Tools/Chateau/Rebuild Footstep Catalogs")]
    public static void RebuildFootstepCatalogs()
    {
        AssetDatabase.Refresh();

        GuestFootstepCatalog catalog = AssetDatabase.LoadAssetAtPath<GuestFootstepCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"Footstep catalog not found at {CatalogPath}.");
            return;
        }

        AudioClip[] butlerClips = LoadClips(ButlerFolder, "FS_Wood_Butler_Soft_");
        AudioClip[] guestClips = LoadClips(GuestFolder, "FS_Wood_Guest_Soft_");
        if (butlerClips.Length < RequiredButlerCount || guestClips.Length < RequiredGuestCount)
        {
            Debug.LogError(
                $"Footstep one-shots are incomplete. Butler={butlerClips.Length}/{RequiredButlerCount}, " +
                $"Guest={guestClips.Length}/{RequiredGuestCount}.");
            return;
        }

        SerializedObject serializedCatalog = new SerializedObject(catalog);
        SetClip(serializedCatalog.FindProperty("butlerClip"), butlerClips[0]);
        SetClipArray(serializedCatalog.FindProperty("butlerClips"), butlerClips);
        serializedCatalog.FindProperty("butlerVolume").floatValue = 0.23f;
        serializedCatalog.FindProperty("defaultVolume").floatValue = 0.28f;
        serializedCatalog.FindProperty("guestStepIntervalSeconds").floatValue = 0.54f;
        serializedCatalog.FindProperty("butlerStepIntervalSeconds").floatValue = 0.6f;
        serializedCatalog.FindProperty("stepIntervalJitterSeconds").floatValue = 0.025f;
        serializedCatalog.FindProperty("highPassCutoffFrequency").floatValue = 180f;
        serializedCatalog.FindProperty("highPassResonanceQ").floatValue = 1.1f;
        serializedCatalog.FindProperty("lowPassCutoffFrequency").floatValue = 9000f;

        SerializedProperty assignments = serializedCatalog.FindProperty("assignments");
        assignments.arraySize = 8;
        for (int i = 0; i < 8; i++)
        {
            SerializedProperty assignment = assignments.GetArrayElementAtIndex(i);
            assignment.FindPropertyRelative("guestNumber").intValue = i + 1;
            SetClip(assignment.FindPropertyRelative("clip"), guestClips[i % guestClips.Length]);
            SetClipArray(assignment.FindPropertyRelative("clips"), guestClips);
            assignment.FindPropertyRelative("volume").floatValue = i == 1 ? 0.3f : 0.28f;
            assignment.FindPropertyRelative("stepIntervalSeconds").floatValue = 0.54f;
        }

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Rebuilt footstep catalog with {butlerClips.Length} Butler one-shots and " +
            $"{guestClips.Length} guest one-shots.");
    }

    [MenuItem("Tools/Chateau/Validate Footstep One-Shots")]
    public static void ValidateFootstepOneShots()
    {
        List<string> issues = new List<string>();
        ValidateRequiredOneShots(ButlerFolder, "FS_Wood_Butler_Soft_", RequiredButlerCount, issues);
        ValidateRequiredOneShots(GuestFolder, "FS_Wood_Guest_Soft_", RequiredGuestCount, issues);
        ValidatePreviewFiles(issues);
        ValidateCatalogAssignments(issues);

        if (issues.Count == 0)
        {
            Debug.Log(
                "Footstep validation passed. Final clips exist, are 44.1 kHz mono PCM one-shots, " +
                "impact timing is early, catalog variants are assigned, and previews exist.");
            return;
        }

        Debug.LogError("Footstep validation failed:\n- " + string.Join("\n- ", issues));
    }

    private static AudioClip[] LoadClips(string folder, string prefix)
    {
        string[] guids = AssetDatabase.FindAssets($"{prefix} t:AudioClip", new[] { folder });
        Array.Sort(guids, (left, right) => string.CompareOrdinal(
            AssetDatabase.GUIDToAssetPath(left),
            AssetDatabase.GUIDToAssetPath(right)));

        List<AudioClip> clips = new List<AudioClip>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                clips.Add(clip);
            }
        }

        return clips.ToArray();
    }

    private static void SetClip(SerializedProperty property, AudioClip clip)
    {
        if (property != null)
        {
            property.objectReferenceValue = clip;
        }
    }

    private static void SetClipArray(SerializedProperty property, AudioClip[] clips)
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }
    }

    private static void ValidateRequiredOneShots(string folder, string prefix, int count, List<string> issues)
    {
        for (int i = 1; i <= count; i++)
        {
            string path = $"{folder}/{prefix}{i:00}.wav";
            if (!File.Exists(path))
            {
                issues.Add($"Missing {path}");
                continue;
            }

            if (!TryReadPcm16Mono(path, out int sampleRate, out float[] samples, out string error))
            {
                issues.Add($"{path}: {error}");
                continue;
            }

            float duration = samples.Length / (float)sampleRate;
            if (sampleRate != SampleRate)
            {
                issues.Add($"{path}: expected {SampleRate} Hz, got {sampleRate} Hz");
            }

            if (duration < 0.18f || duration > 0.28f)
            {
                issues.Add($"{path}: duration {duration:0.000}s outside 0.18-0.28s");
            }

            int searchEnd = Mathf.Min(samples.Length, Mathf.RoundToInt(sampleRate * 0.05f));
            int impactIndex = FindPeakIndex(samples, 0, searchEnd);
            float impactSeconds = impactIndex / (float)sampleRate;
            if (impactSeconds < 0.01f || impactSeconds > 0.03f)
            {
                issues.Add($"{path}: impact at {impactSeconds:0.000}s outside 0.010-0.030s");
            }

            float mainPeak = AbsAt(samples, impactIndex);
            int secondStart = Mathf.Min(samples.Length, Mathf.RoundToInt(sampleRate * 0.075f));
            int secondEnd = Mathf.Min(samples.Length, Mathf.RoundToInt(sampleRate * 0.19f));
            float secondPeak = secondEnd > secondStart ? AbsAt(samples, FindPeakIndex(samples, secondStart, secondEnd)) : 0f;
            if (mainPeak > 0f && secondPeak / mainPeak > 0.72f)
            {
                issues.Add($"{path}: likely contains a second strong impact");
            }

            int tailStart = Mathf.Max(0, samples.Length - Mathf.RoundToInt(sampleRate * 0.035f));
            if (Rms(samples, tailStart, samples.Length) > 0.045f)
            {
                issues.Add($"{path}: tail is not quiet enough");
            }
        }
    }

    private static void ValidatePreviewFiles(List<string> issues)
    {
        for (int i = 0; i < PreviewFiles.Length; i++)
        {
            string path = $"{PreviewFolder}/{PreviewFiles[i]}";
            if (!File.Exists(path))
            {
                issues.Add($"Missing preview {path}");
            }
        }
    }

    private static void ValidateCatalogAssignments(List<string> issues)
    {
        GuestFootstepCatalog catalog = AssetDatabase.LoadAssetAtPath<GuestFootstepCatalog>(CatalogPath);
        if (catalog == null)
        {
            issues.Add($"Missing catalog {CatalogPath}");
            return;
        }

        SerializedObject serializedCatalog = new SerializedObject(catalog);
        if (CountObjectArray(serializedCatalog.FindProperty("butlerClips")) < 4)
        {
            issues.Add("Butler catalog has fewer than 4 assigned variants");
        }

        SerializedProperty assignments = serializedCatalog.FindProperty("assignments");
        if (assignments == null || assignments.arraySize < 8)
        {
            issues.Add("Guest catalog has fewer than 8 assignments");
            return;
        }

        for (int i = 0; i < assignments.arraySize; i++)
        {
            SerializedProperty clips = assignments.GetArrayElementAtIndex(i).FindPropertyRelative("clips");
            if (CountObjectArray(clips) < 4)
            {
                issues.Add($"Guest assignment {i + 1} has fewer than 4 variants");
            }
        }
    }

    private static int CountObjectArray(SerializedProperty property)
    {
        if (property == null || !property.isArray)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).objectReferenceValue != null)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryReadPcm16Mono(string path, out int sampleRate, out float[] samples, out string error)
    {
        sampleRate = 0;
        samples = Array.Empty<float>();
        error = string.Empty;

        short channels = 0;
        short bitsPerSample = 0;
        byte[] data = Array.Empty<byte>();

        using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
        {
            if (new string(reader.ReadChars(4)) != "RIFF")
            {
                error = "not a RIFF WAV";
                return false;
            }

            reader.ReadInt32();
            if (new string(reader.ReadChars(4)) != "WAVE")
            {
                error = "not a WAVE file";
                return false;
            }

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                string chunkId = new string(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();
                long nextChunk = reader.BaseStream.Position + chunkSize;

                if (chunkId == "fmt ")
                {
                    short audioFormat = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();
                    if (audioFormat != 1)
                    {
                        error = $"expected PCM format, got {audioFormat}";
                        return false;
                    }
                }
                else if (chunkId == "data")
                {
                    data = reader.ReadBytes(chunkSize);
                }

                reader.BaseStream.Position = nextChunk;
            }
        }

        if (channels != 1)
        {
            error = $"expected mono, got {channels} channels";
            return false;
        }

        if (bitsPerSample != 16)
        {
            error = $"expected 16-bit PCM, got {bitsPerSample}-bit";
            return false;
        }

        samples = new float[data.Length / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short value = BitConverter.ToInt16(data, i * 2);
            samples[i] = value / 32768f;
        }

        return true;
    }

    private static int FindPeakIndex(float[] samples, int start, int end)
    {
        int bestIndex = start;
        float bestValue = 0f;
        for (int i = start; i < end; i++)
        {
            float value = AbsAt(samples, i);
            if (value > bestValue)
            {
                bestValue = value;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static float AbsAt(float[] samples, int index)
    {
        if (index < 0 || index >= samples.Length)
        {
            return 0f;
        }

        return Mathf.Abs(samples[index]);
    }

    private static float Rms(float[] samples, int start, int end)
    {
        if (end <= start)
        {
            return 0f;
        }

        double total = 0.0;
        for (int i = start; i < end; i++)
        {
            total += samples[i] * samples[i];
        }

        return Mathf.Sqrt((float)(total / (end - start)));
    }
}
