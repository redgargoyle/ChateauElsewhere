#!/usr/bin/env python3
"""Extract single-contact footstep one-shots from existing project audio.

The source walking clips in this project contain several generated steps.  This
tool slices those clips into short one-shot Foley hits, rejects candidates that
still look like loops, and builds preview rhythms for quick listening.
"""

from __future__ import annotations

import argparse
import csv
import math
import shutil
import subprocess
import uuid
import wave
from dataclasses import dataclass
from pathlib import Path

import numpy as np


SCRIPT_PATH = Path(__file__).resolve()
PROJECT_ROOT = SCRIPT_PATH.parents[2]
SAMPLE_RATE = 44100
TARGET_SECONDS = 0.22
MIN_SECONDS = 0.18
MAX_SECONDS = 0.28
IMPACT_LEAD_SECONDS = 0.016
TARGET_PEAK = 10 ** (-12.0 / 20.0)

OUTPUT_ROOT = PROJECT_ROOT / "Assets" / "Audio" / "SFX" / "Footsteps"
RAW_ROOT = OUTPUT_ROOT / "Wood" / "ExtractedRaw"
RAW_ACCEPTED_ROOT = RAW_ROOT / "Accepted"
RAW_REJECTED_ROOT = RAW_ROOT / "Rejected"
BUTLER_ROOT = OUTPUT_ROOT / "Wood" / "Butler"
GUEST_ROOT = OUTPUT_ROOT / "Wood" / "Guest"
PREVIEW_ROOT = OUTPUT_ROOT / "Preview"
REPORT_ROOT = PROJECT_ROOT / "Tools" / "Audio" / "reports"
DESKTOP_LISTEN_ROOT = Path.home() / "Desktop" / "Chateau_Footstep_OneShot_Listening"

SUPPORTED_EXTENSIONS = {".wav", ".aif", ".aiff", ".mp3", ".ogg"}
STRONG_TERMS = (
    "footstep",
    "footsteps",
    "step",
    "steps",
    "walk",
    "walking",
    "shoe",
    "floor",
    "wood",
    "stair",
    "floorboard",
    "hardwood",
)
WEAK_TERMS = ("butler", "guest")
EXCLUDED_PATH_PARTS = {
    "Assets/Audio/SFX/Footsteps",
    "Assets/Audio/Voice",
    "Tools/Voice",
}

FINAL_BUTLER_NAMES = [
    "FS_Wood_Butler_Soft_01.wav",
    "FS_Wood_Butler_Soft_02.wav",
    "FS_Wood_Butler_Soft_03.wav",
    "FS_Wood_Butler_Soft_04.wav",
    "FS_Wood_Butler_Soft_05.wav",
    "FS_Wood_Butler_Soft_06.wav",
]
FINAL_GUEST_NAMES = [
    "FS_Wood_Guest_Soft_01.wav",
    "FS_Wood_Guest_Soft_02.wav",
    "FS_Wood_Guest_Soft_03.wav",
    "FS_Wood_Guest_Soft_04.wav",
    "FS_Wood_Guest_Soft_05.wav",
    "FS_Wood_Guest_Soft_06.wav",
    "FS_Wood_Guest_Soft_07.wav",
    "FS_Wood_Guest_Soft_08.wav",
]


@dataclass
class Candidate:
    source: Path
    source_timestamp: float
    data: np.ndarray
    impact_seconds: float
    duration_seconds: float
    peak: float
    rms: float
    tail_rms: float
    second_ratio: float
    accepted: bool
    reason: str
    raw_output: Path | None = None
    final_output: Path | None = None

    @property
    def source_name(self) -> str:
        return self.source.name.lower()


def run_ffmpeg_decode(path: Path) -> np.ndarray:
    command = [
        "ffmpeg",
        "-v",
        "error",
        "-i",
        str(path),
        "-ac",
        "1",
        "-ar",
        str(SAMPLE_RATE),
        "-f",
        "f32le",
        "pipe:1",
    ]
    result = subprocess.run(command, check=True, stdout=subprocess.PIPE)
    if not result.stdout:
        return np.zeros(0, dtype=np.float32)

    audio = np.frombuffer(result.stdout, dtype=np.float32).copy()
    audio = np.nan_to_num(audio, nan=0.0, posinf=0.0, neginf=0.0)
    return np.clip(audio, -1.0, 1.0).astype(np.float32, copy=False)


def write_wav(path: Path, audio: np.ndarray, sample_rate: int = SAMPLE_RATE) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    clipped = np.clip(audio, -0.999, 0.999)
    pcm = (clipped * 32767.0).astype("<i2")
    with wave.open(str(path), "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(sample_rate)
        handle.writeframes(pcm.tobytes())
    write_unity_audio_meta(path)


def write_unity_audio_meta(audio_path: Path) -> None:
    meta_path = audio_path.with_suffix(audio_path.suffix + ".meta")
    if meta_path.exists():
        return

    meta_path.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {uuid.uuid4().hex}\n"
        "AudioImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 8\n"
        "  defaultSettings:\n"
        "    serializedVersion: 2\n"
        "    loadType: 0\n"
        "    sampleRateSetting: 0\n"
        "    sampleRateOverride: 44100\n"
        "    compressionFormat: 1\n"
        "    quality: 1\n"
        "    conversionMode: 0\n"
        "    preloadAudioData: 0\n"
        "  platformSettingOverrides: {}\n"
        "  forceToMono: 0\n"
        "  normalize: 1\n"
        "  loadInBackground: 0\n"
        "  ambisonic: 0\n"
        "  3D: 1\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n",
        encoding="utf-8",
    )


def moving_average(values: np.ndarray, samples: int) -> np.ndarray:
    samples = max(1, int(samples))
    if samples <= 1:
        return values
    kernel = np.ones(samples, dtype=np.float32) / float(samples)
    return np.convolve(values, kernel, mode="same").astype(np.float32, copy=False)


def high_pass(audio: np.ndarray, cutoff_hz: float = 75.0) -> np.ndarray:
    if audio.size == 0:
        return audio
    dt = 1.0 / SAMPLE_RATE
    rc = 1.0 / (2.0 * math.pi * cutoff_hz)
    alpha = rc / (rc + dt)
    out = np.zeros_like(audio)
    previous_x = float(audio[0])
    previous_y = 0.0
    for i, sample in enumerate(audio):
        current = float(sample)
        previous_y = alpha * (previous_y + current - previous_x)
        out[i] = previous_y
        previous_x = current
    return out


def fade(audio: np.ndarray, fade_in_ms: float = 4.0, fade_out_ms: float = 22.0) -> np.ndarray:
    out = audio.astype(np.float32, copy=True)
    fade_in = min(out.size, int(SAMPLE_RATE * fade_in_ms / 1000.0))
    fade_out = min(out.size, int(SAMPLE_RATE * fade_out_ms / 1000.0))
    if fade_in > 1:
        out[:fade_in] *= np.linspace(0.0, 1.0, fade_in, dtype=np.float32)
    if fade_out > 1:
        out[-fade_out:] *= np.linspace(1.0, 0.0, fade_out, dtype=np.float32)
    return out


def normalize_to_peak(audio: np.ndarray, peak: float = TARGET_PEAK) -> np.ndarray:
    current = float(np.max(np.abs(audio))) if audio.size else 0.0
    if current <= 1e-7:
        return audio
    return np.clip(audio * (peak / current), -0.98, 0.98).astype(np.float32, copy=False)


def dbfs(value: float) -> float:
    if value <= 1e-9:
        return -180.0
    return 20.0 * math.log10(value)


def relative(path: Path) -> str:
    try:
        return str(path.relative_to(PROJECT_ROOT))
    except ValueError:
        return str(path)


def is_likely_source(path: Path) -> bool:
    try:
        rel = path.relative_to(PROJECT_ROOT).as_posix()
    except ValueError:
        rel = path.as_posix()

    if any(part in rel for part in EXCLUDED_PATH_PARTS):
        return False

    text = rel.lower()
    strong = sum(1 for term in STRONG_TERMS if term in text)
    weak = sum(1 for term in WEAK_TERMS if term in text)
    return strong > 0 or (weak > 0 and ("audio" in text or "sfx" in text or "sound" in text))


def find_sources() -> list[Path]:
    roots = [
        PROJECT_ROOT / "Assets" / "Audio",
        PROJECT_ROOT / "Assets" / "Sounds",
        PROJECT_ROOT / "Assets" / "SFX",
        PROJECT_ROOT / "Assets" / "Resources" / "Audio",
    ]
    found: list[Path] = []
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if path.suffix.lower() in SUPPORTED_EXTENSIONS and is_likely_source(path):
                found.append(path)
    return sorted(dict.fromkeys(found))


def detect_impacts(audio: np.ndarray) -> list[int]:
    if audio.size < int(0.12 * SAMPLE_RATE):
        return []

    detected = np.abs(high_pass(audio, 85.0))
    envelope = moving_average(detected, int(0.004 * SAMPLE_RATE))
    if envelope.size == 0:
        return []

    threshold = max(
        0.006,
        float(np.percentile(envelope, 90)) * 1.2,
        float(np.percentile(envelope, 98)) * 0.36,
    )
    min_distance = int(0.12 * SAMPLE_RATE)
    peaks: list[int] = []
    last_peak = -min_distance

    for i in range(1, envelope.size - 1):
        if envelope[i] < threshold:
            continue
        if envelope[i] < envelope[i - 1] or envelope[i] < envelope[i + 1]:
            continue

        if i - last_peak < min_distance:
            if peaks and envelope[i] > envelope[peaks[-1]]:
                peaks[-1] = i
                last_peak = i
            continue

        peaks.append(i)
        last_peak = i

    refined: list[int] = []
    search_radius = int(0.012 * SAMPLE_RATE)
    for peak in peaks:
        left = max(0, peak - search_radius)
        right = min(audio.size, peak + search_radius)
        if right <= left:
            continue
        local = int(np.argmax(np.abs(audio[left:right])))
        refined_peak = left + local
        if not refined or refined_peak - refined[-1] >= min_distance:
            refined.append(refined_peak)
    return refined


def analyze_candidate(source: Path, audio: np.ndarray, impact_index: int) -> Candidate:
    target_samples = int(TARGET_SECONDS * SAMPLE_RATE)
    start = impact_index - int(IMPACT_LEAD_SECONDS * SAMPLE_RATE)
    end = start + target_samples
    if start < 0 or end > audio.size:
        return Candidate(
            source=source,
            source_timestamp=impact_index / SAMPLE_RATE,
            data=np.zeros(0, dtype=np.float32),
            impact_seconds=IMPACT_LEAD_SECONDS,
            duration_seconds=TARGET_SECONDS,
            peak=0.0,
            rms=0.0,
            tail_rms=0.0,
            second_ratio=0.0,
            accepted=False,
            reason="too close to source edge",
        )

    raw = audio[start:end].astype(np.float32, copy=True)
    raw -= float(np.mean(raw))

    cleaned = high_pass(raw, 65.0)
    cleaned = fade(cleaned)

    pre_peak = float(np.max(np.abs(cleaned))) if cleaned.size else 0.0
    rms = float(np.sqrt(np.mean(np.square(cleaned)))) if cleaned.size else 0.0
    normalized = normalize_to_peak(cleaned)
    tail = normalized[-int(0.035 * SAMPLE_RATE) :]
    tail_rms = float(np.sqrt(np.mean(np.square(tail)))) if tail.size else 0.0

    envelope = moving_average(np.abs(high_pass(normalized, 85.0)), int(0.004 * SAMPLE_RATE))
    impact_local = int(IMPACT_LEAD_SECONDS * SAMPLE_RATE)
    main_window_left = max(0, impact_local - int(0.015 * SAMPLE_RATE))
    main_window_right = min(envelope.size, impact_local + int(0.040 * SAMPLE_RATE))
    main_env = float(np.max(envelope[main_window_left:main_window_right])) if main_window_right > main_window_left else 0.0
    second_start = min(envelope.size, impact_local + int(0.075 * SAMPLE_RATE))
    second_end = min(envelope.size, impact_local + int(0.190 * SAMPLE_RATE))
    second_env = float(np.max(envelope[second_start:second_end])) if second_end > second_start else 0.0
    second_ratio = second_env / max(main_env, 1e-7)

    active = np.mean(np.abs(normalized) > 0.006) if normalized.size else 0.0
    accepted = True
    reasons: list[str] = []
    if pre_peak < 0.012 or rms < 0.001:
        accepted = False
        reasons.append("mostly silence")
    if IMPACT_LEAD_SECONDS > 0.030:
        accepted = False
        reasons.append("impact later than 0.030s")
    if TARGET_SECONDS < MIN_SECONDS or TARGET_SECONDS > MAX_SECONDS:
        accepted = False
        reasons.append("duration outside allowed range")
    if second_ratio > 0.72:
        accepted = False
        reasons.append("second strong transient")
    if tail_rms > 0.045:
        accepted = False
        reasons.append("tail not quiet")
    if active > 0.84:
        accepted = False
        reasons.append("continuous ambience/noise")

    return Candidate(
        source=source,
        source_timestamp=impact_index / SAMPLE_RATE,
        data=normalized,
        impact_seconds=IMPACT_LEAD_SECONDS,
        duration_seconds=TARGET_SECONDS,
        peak=float(np.max(np.abs(normalized))) if normalized.size else 0.0,
        rms=float(np.sqrt(np.mean(np.square(normalized)))) if normalized.size else 0.0,
        tail_rms=tail_rms,
        second_ratio=second_ratio,
        accepted=accepted,
        reason=", ".join(reasons) if reasons else "accepted",
    )


def source_priority(candidate: Candidate, role: str) -> tuple[float, str, float]:
    name = candidate.source.as_posix().lower()
    score = 0.0
    if "footstep" in name or "walk" in name or "step" in name:
        score -= 8.0
    if "hardwood" in name:
        score -= 4.0
    if "wood" in name:
        score -= 2.5
    if "floorboard" in name:
        score -= 2.0
    if "stair" in name:
        score -= 1.5
    if "slow_cautious" in name:
        score -= 2.0
    if "marble" in name:
        score += 3.5
    if "carpet" in name:
        score += 1.5
    if "sound exports" in name:
        score += 14.0
    if "woodcreak" in name or "wood_tapping" in name or "tapping" in name:
        score += 18.0
    if role == "butler" and "man" in name:
        score -= 1.2
    if role == "guest" and "woman" in name:
        score -= 0.6
    score += candidate.tail_rms * 20.0
    score += candidate.second_ratio * 3.0
    score += abs(candidate.rms - 0.035) * 4.0
    return (score, relative(candidate.source), candidate.source_timestamp)


def select_candidates(candidates: list[Candidate], count: int, role: str) -> list[Candidate]:
    pool = [candidate for candidate in candidates if candidate.accepted]
    preferred = [
        candidate
        for candidate in pool
        if (
            any(term in candidate.source.as_posix().lower() for term in ("footstep", "walk", "step"))
            and any(term in candidate.source.as_posix().lower() for term in ("wood", "hardwood", "floorboard", "stair"))
            and "sound exports" not in candidate.source.as_posix().lower()
        )
    ]
    if len(preferred) >= count:
        pool = preferred

    pool = sorted(pool, key=lambda item: source_priority(item, role))
    selected: list[Candidate] = []
    used_sources: set[Path] = set()

    while len(selected) < count and pool:
        for candidate in list(pool):
            if candidate.source not in used_sources or len(used_sources) >= len({item.source for item in pool}):
                selected.append(candidate)
                used_sources.add(candidate.source)
                pool.remove(candidate)
                break
        else:
            selected.append(pool.pop(0))

    return selected


def make_safe_stem(path: Path) -> str:
    stem = path.stem
    safe = "".join(ch.lower() if ch.isalnum() else "_" for ch in stem)
    while "__" in safe:
        safe = safe.replace("__", "_")
    return safe.strip("_")


def write_raw_candidates(candidates: list[Candidate]) -> None:
    for folder in (RAW_ACCEPTED_ROOT, RAW_REJECTED_ROOT):
        folder.mkdir(parents=True, exist_ok=True)

    for index, candidate in enumerate(candidates, 1):
        source_stem = make_safe_stem(candidate.source)
        timestamp = int(round(candidate.source_timestamp * 1000.0))
        name = f"{index:04d}_{source_stem}_{timestamp:06d}ms.wav"
        out_root = RAW_ACCEPTED_ROOT if candidate.accepted else RAW_REJECTED_ROOT
        out_path = out_root / name
        if candidate.data.size:
            write_wav(out_path, candidate.data)
        candidate.raw_output = out_path


def write_final_candidates(selected: list[Candidate], names: list[str], folder: Path) -> list[Path]:
    folder.mkdir(parents=True, exist_ok=True)
    outputs: list[Path] = []
    for candidate, name in zip(selected, names):
        out_path = folder / name
        write_wav(out_path, candidate.data)
        candidate.final_output = out_path
        outputs.append(out_path)
    return outputs


def read_wav_mono(path: Path) -> np.ndarray:
    with wave.open(str(path), "rb") as handle:
        channels = handle.getnchannels()
        sample_rate = handle.getframerate()
        sample_width = handle.getsampwidth()
        frames = handle.readframes(handle.getnframes())
    if sample_width != 2:
        return run_ffmpeg_decode(path)
    data = np.frombuffer(frames, dtype="<i2").astype(np.float32) / 32768.0
    if channels > 1:
        data = data.reshape((-1, channels)).mean(axis=1)
    if sample_rate != SAMPLE_RATE:
        return run_ffmpeg_decode(path)
    return data.astype(np.float32, copy=False)


def overlay(target: np.ndarray, clip: np.ndarray, start: int, gain: float) -> None:
    if start >= target.size:
        return
    end = min(target.size, start + clip.size)
    if end <= start:
        return
    target[start:end] += clip[: end - start] * gain


def make_preview(name: str, clips: list[Path], interval: float, offsets: list[float], seconds: float = 5.2) -> Path:
    PREVIEW_ROOT.mkdir(parents=True, exist_ok=True)
    loaded = [read_wav_mono(path) for path in clips]
    total = int(seconds * SAMPLE_RATE)
    mix = np.zeros(total, dtype=np.float32)
    stream_gain = min(0.85, 1.0 / max(1.0, math.sqrt(len(offsets))))

    for stream_index, offset in enumerate(offsets):
        t = offset
        step_index = stream_index
        while t < seconds:
            clip = loaded[step_index % len(loaded)]
            overlay(mix, clip, int(t * SAMPLE_RATE), stream_gain)
            step_index += len(offsets)
            t += interval

    peak = float(np.max(np.abs(mix))) if mix.size else 0.0
    if peak > 0.74:
        mix *= 0.74 / peak
    out_path = PREVIEW_ROOT / name
    write_wav(out_path, mix)
    return out_path


def write_reports(candidates: list[Candidate], final_paths: list[Path], preview_paths: list[Path], sources: list[Path]) -> tuple[Path, Path]:
    REPORT_ROOT.mkdir(parents=True, exist_ok=True)
    csv_path = REPORT_ROOT / "footstep_one_shot_extraction_report.csv"
    md_path = REPORT_ROOT / "footstep_one_shot_extraction_report.md"

    with csv_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(
            [
                "source_file",
                "source_timestamp_seconds",
                "raw_output_file",
                "final_output_file",
                "detected_impact_time_seconds",
                "duration_seconds",
                "peak_dbfs",
                "tail_rms",
                "second_transient_ratio",
                "decision",
                "reason",
            ]
        )
        for candidate in candidates:
            writer.writerow(
                [
                    relative(candidate.source),
                    f"{candidate.source_timestamp:.4f}",
                    relative(candidate.raw_output) if candidate.raw_output else "",
                    relative(candidate.final_output) if candidate.final_output else "",
                    f"{candidate.impact_seconds:.4f}",
                    f"{candidate.duration_seconds:.4f}",
                    f"{dbfs(candidate.peak):.2f}",
                    f"{candidate.tail_rms:.6f}",
                    f"{candidate.second_ratio:.3f}",
                    "accept" if candidate.accepted else "reject",
                    candidate.reason,
                ]
            )

    accepted_count = sum(1 for candidate in candidates if candidate.accepted)
    rejected_count = len(candidates) - accepted_count
    lines = [
        "# Footstep One-Shot Extraction Report",
        "",
        f"- Existing source clips found: {len(sources)}",
        f"- One-shot candidates extracted: {len(candidates)}",
        f"- Accepted candidates: {accepted_count}",
        f"- Rejected candidates: {rejected_count}",
        "- Model generation needed: no",
        "- Model used: none",
        "",
        "## Final Output Files",
    ]
    for path in final_paths:
        lines.append(f"- `{relative(path)}`")
    lines.extend(["", "## Preview Files"])
    for path in preview_paths:
        lines.append(f"- `{relative(path)}`")
    lines.extend(["", "## Sources"])
    for path in sources:
        lines.append(f"- `{relative(path)}`")
    lines.extend(["", "## Accepted Candidates"])
    for candidate in candidates:
        if not candidate.accepted:
            continue
        final = f", final `{relative(candidate.final_output)}`" if candidate.final_output else ""
        lines.append(
            f"- `{relative(candidate.raw_output)}` from `{relative(candidate.source)}` at "
            f"{candidate.source_timestamp:.3f}s{final}"
        )
    lines.extend(["", "## Rejected Candidates"])
    for candidate in candidates:
        if candidate.accepted:
            continue
        lines.append(
            f"- `{relative(candidate.raw_output)}` from `{relative(candidate.source)}` at "
            f"{candidate.source_timestamp:.3f}s: {candidate.reason}"
        )
    md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return csv_path, md_path


def copy_listening_folder(final_paths: list[Path], preview_paths: list[Path], report_paths: tuple[Path, Path]) -> None:
    if DESKTOP_LISTEN_ROOT.exists():
        shutil.rmtree(DESKTOP_LISTEN_ROOT)
    (DESKTOP_LISTEN_ROOT / "Butler").mkdir(parents=True, exist_ok=True)
    (DESKTOP_LISTEN_ROOT / "Guest").mkdir(parents=True, exist_ok=True)
    (DESKTOP_LISTEN_ROOT / "Preview").mkdir(parents=True, exist_ok=True)
    (DESKTOP_LISTEN_ROOT / "Reports").mkdir(parents=True, exist_ok=True)

    for path in final_paths:
        target_dir = "Butler" if "Butler" in path.name else "Guest"
        shutil.copy2(path, DESKTOP_LISTEN_ROOT / target_dir / path.name)
    for path in preview_paths:
        shutil.copy2(path, DESKTOP_LISTEN_ROOT / "Preview" / path.name)
    for path in report_paths:
        shutil.copy2(path, DESKTOP_LISTEN_ROOT / "Reports" / path.name)


def clean_output_dirs() -> None:
    for folder in (RAW_ACCEPTED_ROOT, RAW_REJECTED_ROOT, BUTLER_ROOT, GUEST_ROOT, PREVIEW_ROOT):
        if folder.exists():
            shutil.rmtree(folder)
        folder.mkdir(parents=True, exist_ok=True)


def extract_candidates(sources: list[Path]) -> list[Candidate]:
    candidates: list[Candidate] = []
    for source in sources:
        try:
            audio = run_ffmpeg_decode(source)
        except (subprocess.CalledProcessError, FileNotFoundError) as exc:
            candidates.append(
                Candidate(
                    source=source,
                    source_timestamp=0.0,
                    data=np.zeros(0, dtype=np.float32),
                    impact_seconds=0.0,
                    duration_seconds=0.0,
                    peak=0.0,
                    rms=0.0,
                    tail_rms=0.0,
                    second_ratio=0.0,
                    accepted=False,
                    reason=f"decode failed: {exc}",
                )
            )
            continue

        if audio.size == 0:
            continue

        for impact in detect_impacts(audio):
            candidate = analyze_candidate(source, audio, impact)
            if candidate.data.size > 0:
                candidates.append(candidate)
    return candidates


def validate_finals(paths: list[Path]) -> list[str]:
    issues: list[str] = []
    for path in paths:
        audio = read_wav_mono(path)
        duration = audio.size / SAMPLE_RATE
        peak = float(np.max(np.abs(audio))) if audio.size else 0.0
        impacts = detect_impacts(audio)
        impact_time = impacts[0] / SAMPLE_RATE if impacts else -1.0
        late_impacts = [impact for impact in impacts[1:] if impact / SAMPLE_RATE > 0.075]
        if not (MIN_SECONDS <= duration <= MAX_SECONDS):
            issues.append(f"{relative(path)} duration {duration:.3f}s outside range")
        if not (0.010 <= impact_time <= 0.030):
            issues.append(f"{relative(path)} impact {impact_time:.3f}s outside range")
        if late_impacts:
            issues.append(f"{relative(path)} has multiple strong impacts")
        if peak > TARGET_PEAK * 1.05:
            issues.append(f"{relative(path)} peak {dbfs(peak):.2f} dBFS too hot")
    return issues


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Extract footstep one-shots from existing project walking clips.")
    parser.add_argument("--keep-existing", action="store_true", help="Do not clear previous generated output folders.")
    parser.add_argument("--no-desktop-copy", action="store_true", help="Skip copying previews/finals to the Desktop listening folder.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if not args.keep_existing:
        clean_output_dirs()

    sources = find_sources()
    candidates = extract_candidates(sources)
    write_raw_candidates(candidates)

    accepted = [candidate for candidate in candidates if candidate.accepted]
    if len(accepted) < len(FINAL_BUTLER_NAMES) + len(FINAL_GUEST_NAMES):
        print(
            "Warning: fewer accepted candidates than final slots. "
            "The script will fill what it can from accepted candidates."
        )

    butler = select_candidates(accepted, len(FINAL_BUTLER_NAMES), "butler")
    remaining = [candidate for candidate in accepted if candidate not in butler]
    guest_pool = remaining if len(remaining) >= len(FINAL_GUEST_NAMES) else accepted
    guest = select_candidates(guest_pool, len(FINAL_GUEST_NAMES), "guest")

    butler_paths = write_final_candidates(butler, FINAL_BUTLER_NAMES, BUTLER_ROOT)
    guest_paths = write_final_candidates(guest, FINAL_GUEST_NAMES, GUEST_ROOT)
    final_paths = butler_paths + guest_paths

    preview_paths = [
        make_preview("Preview_Butler_Walk_0p60s.wav", butler_paths, 0.60, [0.0]),
        make_preview("Preview_Guest_Walk_0p54s.wav", guest_paths, 0.54, [0.0]),
        make_preview("Preview_FastWalk_0p42s.wav", guest_paths, 0.42, [0.0]),
        make_preview("Preview_GuestPair_Walk_0p54s_offset_0p12s.wav", guest_paths, 0.54, [0.0, 0.12]),
        make_preview("Preview_FourGuests_Walk_0p54s_offsets.wav", guest_paths, 0.54, [0.0, 0.11, 0.24, 0.37]),
    ]
    report_paths = write_reports(candidates, final_paths, preview_paths, sources)

    if not args.no_desktop_copy:
        copy_listening_folder(final_paths, preview_paths, report_paths)

    issues = validate_finals(final_paths)
    accepted_count = sum(1 for candidate in candidates if candidate.accepted)
    rejected_count = len(candidates) - accepted_count

    print("Footstep one-shot extraction complete.")
    print(f"Existing source clips found: {len(sources)}")
    print(f"One-shots extracted: {len(candidates)}")
    print(f"Accepted: {accepted_count}")
    print(f"Rejected: {rejected_count}")
    print("Model generation needed: no")
    print("Model used: none")
    print("Final output files:")
    for path in final_paths:
        print(f"  {relative(path)}")
    print("Preview files:")
    for path in preview_paths:
        print(f"  {relative(path)}")
    print(f"Reports: {relative(report_paths[0])}, {relative(report_paths[1])}")
    if not args.no_desktop_copy:
        print(f"Desktop listening folder: {DESKTOP_LISTEN_ROOT}")
    if issues:
        print("Validation issues:")
        for issue in issues:
            print(f"  {issue}")
        return 2
    print("Validation: all final clips are 44.1 kHz mono PCM WAV one-shots with early impacts.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
