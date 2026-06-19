#!/usr/bin/env python
from __future__ import annotations

import argparse
import csv
import ctypes
import hashlib
import inspect
import io
import os
import random
import re
import shlex
import site
import sys
import textwrap
import time
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import torch
import torchaudio


os.environ.setdefault("HF_HUB_OFFLINE", "1")
os.environ.setdefault("TRANSFORMERS_OFFLINE", "1")

PROJECT_ROOT = Path(__file__).resolve().parents[2]
VOICE_ROOT = PROJECT_ROOT / "Assets" / "Audio" / "Voice" / "Guests"
TOOLS_VOICE_ROOT = PROJECT_ROOT / "Tools" / "Voice"
REFERENCE_ROOT = TOOLS_VOICE_ROOT / "reference_clips"
ANCHOR_ROOT = TOOLS_VOICE_ROOT / "generated_voice_anchors"
REPORT_ROOT = TOOLS_VOICE_ROOT / "reports"
REPORT_PATH = REPORT_ROOT / "guest_voice_generation_report.md"
TARGET_PEAK = 10 ** (-3.0 / 20.0)
LEADING_PAUSE_SECONDS = 0.08
TRAILING_PAUSE_SECONDS = 0.12

GUEST_SEEDS = {
    1: 1101,
    2: 1202,
    3: 1303,
    4: 1404,
    5: 1505,
    6: 1606,
    7: 1707,
    8: 1808,
}

VOICE_PROFILES = {
    1: "Older upper-class English gentleman; dry baritone; precise diction; judgmental, controlled, faintly afraid underneath.",
    2: "Gentle English lady; soft, nervous, warm; breath tightens when frightened; polite even when rattled.",
    3: "Theatrical English woman; witty, bright, slightly flamboyant; sharp comic timing over real fear.",
    4: "Stern older English man; low, gravelly, severe; dry fatalist humor; slow and deliberate.",
    5: "Calm practical English voice; composed, protective, steady; sounds like the first person to take charge in a crisis.",
    6: "Anxious refined English gentleman; quick breath, fragile composure, elegant phrasing under panic.",
    7: "Haunted English mystic; quiet, intense, whisper-edged; sounds like they noticed the monster before everyone else.",
    8: "Commanding English matriarch/aristocrat; cool, severe, fearless mask over dread; clipped consonants.",
}

ANCHOR_TEXT = {
    1: "Good evening. I trust the house remembers its manners better than the weather does.",
    2: "Thank you. The drive was longer in the dark than I care to admit.",
    3: "Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?",
    4: "Good evening, Butler. The road up here has the cheerful shape of a warning.",
    5: "Good evening. I hope the evening has not started without us.",
    6: "Thank you. I nearly mistook the bell pull for a funeral cord.",
    7: "Lovely to see you. The chateau looks almost awake tonight.",
    8: "Good evening, Butler. I see the house has chosen its most severe face.",
}

QUALITY_SETTINGS = {
    "normal": {"exaggeration": 0.50, "cfg_weight": 0.50, "temperature": 0.75},
    "uneasy": {"exaggeration": 0.58, "cfg_weight": 0.45, "temperature": 0.80},
    "panic": {"exaggeration": 0.75, "cfg_weight": 0.30, "temperature": 0.85},
    "found": {"exaggeration": 0.62, "cfg_weight": 0.40, "temperature": 0.80},
    "dining": {"exaggeration": 0.52, "cfg_weight": 0.45, "temperature": 0.75},
}

DIALOGUE_CSV = """line_id,speaker,text
CH1_G01_ENTRY,Guest 1,Good evening. I trust the house remembers its manners better than the weather does.
CH1_G01_DELAYED,Guest 1,We were beginning to wonder if anyone was home.
CH1_G01_COAT_HANDOFF,Guest 1,"Careful with the collar, if you please. It has survived worse evenings than this one."
CH1_G01_TO_DRAWING_ROOM,Guest 1,"A proper house is judged by its wardrobe first. So far, Chateau Chantilly remains under review."
CH1_G01_AMBIENT_01,Guest 1,This house is colder than I expected.
CH1_G01_AMBIENT_02,Guest 1,The fire looks arranged rather than lit.
CH1_G01_EMPTY_BELL_REACTION,Guest 1,"Then who, precisely, rang?"
CH1_G02_ENTRY,Guest 2,Thank you. The drive was longer in the dark than I care to admit.
CH1_G02_DELAYED,Guest 2,It is rather cold out there.
CH1_G02_COAT_HANDOFF,Guest 2,Thank you. The damp seems to cling to everything tonight.
CH1_G02_TO_DRAWING_ROOM,Guest 2,The Drawing Room sounds heavenly. I would settle for any room with a pulse of warmth.
CH1_G02_AMBIENT_01,Guest 2,"The host is late, isn't he?"
CH1_G02_AMBIENT_02,Guest 2,I keep thinking someone is standing just behind the curtains.
CH1_G02_EMPTY_BELL_REACTION,Guest 2,Please do not say the wind. The wind has better manners.
CH1_G03_ENTRY,Guest 3,"Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?"
CH1_G03_DELAYED,Guest 3,We have been waiting at the door for some time.
CH1_G03_COAT_HANDOFF,Guest 3,With gratitude. Do hang it somewhere it can be admired.
CH1_G03_TO_DRAWING_ROOM,Guest 3,Prepared? How promising. I adore a room that knows guests are coming.
CH1_G03_AMBIENT_01,Guest 3,Did you hear something upstairs?
CH1_G03_AMBIENT_02,Guest 3,"If the house is settling, it is doing so with theatrical timing."
CH1_G03_EMPTY_BELL_REACTION,Guest 3,A phantom caller? How rude to arrive without a coat.
CH1_G04_ENTRY,Guest 4,"Good evening, Butler. The road up here has the cheerful shape of a warning."
CH1_G04_DELAYED,Guest 4,At last. I had begun composing my obituary in the frost.
CH1_G04_COAT_HANDOFF,Guest 4,Take it before it decides to stay here without me.
CH1_G04_TO_DRAWING_ROOM,Guest 4,Prepared rooms and prepared excuses often look alike. Lead on.
CH1_G04_AMBIENT_01,Guest 4,The drawing room should be warmer.
CH1_G04_AMBIENT_02,Guest 4,Old houses groan. This one seems to choose its words.
CH1_G04_EMPTY_BELL_REACTION,Guest 4,"Doors do not summon themselves. Not respectable doors, anyway."
CH1_G05_ENTRY,Guest 5,Good evening. I hope the evening has not started without us.
CH1_G05_DELAYED,Guest 5,We were beginning to wonder if anyone was home.
CH1_G05_COAT_HANDOFF,Guest 5,Of course. There is nothing in the pockets but travel dust and bad omens.
CH1_G05_TO_DRAWING_ROOM,Guest 5,Then let us not keep the Drawing Room from its purpose.
CH1_G05_AMBIENT_01,Guest 5,This house is colder than I expected.
CH1_G05_AMBIENT_02,Guest 5,The portraits look recently offended.
CH1_G05_EMPTY_BELL_REACTION,Guest 5,Let us pretend it was a mistake. Pretending is useful in old houses.
CH1_G06_ENTRY,Guest 6,Thank you. I nearly mistook the bell pull for a funeral cord.
CH1_G06_DELAYED,Guest 6,"It is rather cold out there, and colder still when one is expected."
CH1_G06_COAT_HANDOFF,Guest 6,"Yes, please. I have been wearing half the road since the lower gate."
CH1_G06_TO_DRAWING_ROOM,Guest 6,"If the fire is real, I may forgive the road."
CH1_G06_AMBIENT_01,Guest 6,"The host is late, isn't he?"
CH1_G06_AMBIENT_02,Guest 6,I dislike a clock that seems to be waiting for me personally.
CH1_G06_EMPTY_BELL_REACTION,Guest 6,I should very much like that to be the last surprise before dinner.
CH1_G07_ENTRY,Guest 7,Lovely to see you. The chateau looks almost awake tonight.
CH1_G07_DELAYED,Guest 7,We have been waiting at the door for some time. The house was listening with us.
CH1_G07_COAT_HANDOFF,Guest 7,"Yes. And if it whispers, do not answer it."
CH1_G07_TO_DRAWING_ROOM,Guest 7,Prepared is good. Protected would be better.
CH1_G07_AMBIENT_01,Guest 7,Did you hear something upstairs?
CH1_G07_AMBIENT_02,Guest 7,"The ceiling has footsteps in it, and not all of them are human."
CH1_G07_EMPTY_BELL_REACTION,Guest 7,It wanted us all in here. That is what I think.
CH1_G08_ENTRY,Guest 8,"Good evening, Butler. I see the house has chosen its most severe face."
CH1_G08_DELAYED,Guest 8,At last. A closed door should not feel so pleased with itself.
CH1_G08_COAT_HANDOFF,Guest 8,Take it. The night has left fingerprints on the sleeves.
CH1_G08_TO_DRAWING_ROOM,Guest 8,Very well. Let us see what sort of welcome the room has rehearsed.
CH1_G08_AMBIENT_01,Guest 8,The drawing room should be warmer.
CH1_G08_AMBIENT_02,Guest 8,There is a draft here that does not come from any door.
CH1_G08_EMPTY_BELL_REACTION,Guest 8,Then we are here. I hope it is satisfied.
CH2_G01_PRESPEECH_BARK,Guest 1,"Do begin, Butler. Formality is all that stands between dinner and nonsense."
CH2_G02_PRESPEECH_BARK,Guest 2,Are we all meant to be waiting like this?
CH2_G03_PRESPEECH_BARK,Guest 3,"This is deliciously awkward. I approve, with reservations."
CH2_G04_PRESPEECH_BARK,Guest 4,It is never a good sign when the servants make speeches.
CH2_G05_PRESPEECH_BARK,Guest 5,Let him speak. The hour has turned strange.
CH2_G06_PRESPEECH_BARK,Guest 6,I dislike a room that listens back.
CH2_G07_PRESPEECH_BARK,Guest 7,That sound in the walls—did anyone else hear it before the bell?
CH2_G08_PRESPEECH_BARK,Guest 8,"Say what you came to say, Butler. The room is holding its breath."
CH2_G01_PANIC,Guest 1,"Do not run! Do not—oh Lord, run!"
CH2_G02_PANIC,Guest 2,It has too many legs!
CH2_G03_PANIC,Guest 3,That is not a dog. Someone tell me that is not a dog.
CH2_G04_PANIC,Guest 4,Down! Get down!
CH2_G05_PANIC,Guest 5,Away from the windows!
CH2_G06_PANIC,Guest 6,The violin—make it stop!
CH2_G07_PANIC,Guest 7,I saw its hair move before it moved!
CH2_G08_PANIC,Guest 8,No one touch it! No one breathe at it!
CH2_G01_FOUND_START,Guest 1,Announce yourself before I die of manners.
CH2_G01_FOUND_REPLY,Guest 1,You may record whatever prevents further surprises.
CH2_G01_MEAL_PLINK,Guest 1,"The fresh monte genellion de plink. If one must face horrors, one should do it properly fed."
CH2_G01_MEAL_THYME,Guest 1,"Thyme with Lillums. It sounds disciplined, and discipline is wanted tonight."
CH2_G01_SMOKE_CIGAR,Guest 1,A cigar. Something with authority.
CH2_G01_SMOKE_PIPE,Guest 1,A pipe. Slower nerves make better decisions.
CH2_G01_SMOKE_NONE,Guest 1,No smoke. I should like my lungs available for any further screaming.
CH2_G01_SPIRITS_REPLY,Guest 1,See that it is not shy.
CH2_G01_EXIT_TO_DINING,Guest 1,Very good. I shall present myself in the Dining Room and recover what dignity remains to us.
CH2_G01_CLOCK_REACTION,Guest 1,Seven o’clock. At least the clock is still obedient.
CH2_G01_DINING_REVEAL,Guest 1,Civilization survives another minute.
CH2_G02_FOUND_START,Guest 2,Please tell me you are real before you come any closer.
CH2_G02_FOUND_REPLY,Guest 2,"At seven? After that thing? Yes. Yes, ordinary questions may save us."
CH2_G02_MEAL_PLINK,Guest 2,"The fresh monte genellion de plink. I cannot explain why, but the longer name feels safer."
CH2_G02_MEAL_THYME,Guest 2,"Thyme with Lillums, please. Something gentle. Something with leaves."
CH2_G02_SMOKE_CIGAR,Guest 2,"A cigar, though I may only hold it for courage."
CH2_G02_SMOKE_PIPE,Guest 2,"A pipe, if it can be made to smell like a normal evening."
CH2_G02_SMOKE_NONE,Guest 2,No smoke at all. I have inhaled enough terror for one night.
CH2_G02_SPIRITS_REPLY,Guest 2,Thank you. I may ask it several questions.
CH2_G02_EXIT_TO_DINING,Guest 2,Very good. I shall present myself in the Dining Room and recover what dignity remains to us.
CH2_G02_CLOCK_REACTION,Guest 2,Please tell me dinner has windows. No—doors. I meant doors.
CH2_G02_DINING_REVEAL,Guest 2,I have never been so grateful for a chair.
CH2_G03_FOUND_START,Guest 3,"If this is a party game, I withdraw my admiration."
CH2_G03_FOUND_REPLY,Guest 3,Splendid. Nothing steadies the soul like being menued after a monster.
CH2_G03_MEAL_PLINK,Guest 3,"Fresh monte genellion de plink. It sounds impossible, and I am in an impossible mood."
CH2_G03_MEAL_THYME,Guest 3,"Thyme with Lillums. Pretty, mysterious, and likely to stain. I accept."
CH2_G03_SMOKE_CIGAR,Guest 3,A cigar. I intend to look magnificent while recovering.
CH2_G03_SMOKE_PIPE,Guest 3,A pipe. It gives one the illusion of wisdom.
CH2_G03_SMOKE_NONE,Guest 3,No smoke. The monster already supplied quite enough atmosphere.
CH2_G03_SPIRITS_REPLY,Guest 3,Make it visible. I may need to toast survival several times.
CH2_G03_EXIT_TO_DINING,Guest 3,Very good. I shall present myself in the Dining Room and recover what dignity remains to us.
CH2_G03_CLOCK_REACTION,Guest 3,"If anyone asks, I was never frightened. I was arranging my face."
CH2_G03_DINING_REVEAL,Guest 3,"Look at us. Pale, terrified, and still punctual."
CH2_G04_FOUND_START,Guest 4,"If you are here to say dinner is canceled, lie more elegantly."
CH2_G04_FOUND_REPLY,Guest 4,"Good. A schedule is a flimsy shield, but it is a shield."
CH2_G04_MEAL_PLINK,Guest 4,"Fresh monte genellion de plink. If the name is a trap, I expect you to spring it first."
CH2_G04_MEAL_THYME,Guest 4,Thyme with Lillums. Quiet food. Sensible food. Food unlikely to chase me.
CH2_G04_SMOKE_CIGAR,Guest 4,"A cigar. If I am to be hunted by architecture, I shall smell expensive."
CH2_G04_SMOKE_PIPE,Guest 4,A pipe. It gives the hands something to do besides tremble.
CH2_G04_SMOKE_NONE,Guest 4,No smoke. I prefer to see what is coming.
CH2_G04_SPIRITS_REPLY,Guest 4,Good. I distrust a dinner table without witnesses.
CH2_G04_EXIT_TO_DINING,Guest 4,Very good. I shall present myself in the Dining Room and recover what dignity remains to us.
CH2_G04_CLOCK_REACTION,Guest 4,The clock sounds pleased with itself. I resent that.
CH2_G04_DINING_REVEAL,Guest 4,"If the soup screams, I am leaving."
CH2_G05_FOUND_START,Guest 5,I was not hiding. I was choosing a defensible position.
CH2_G05_FOUND_REPLY,Guest 5,"Proceed. The more ordinary the ritual, the less power we give the extraordinary."
CH2_G05_MEAL_PLINK,Guest 5,Fresh monte genellion de plink. Something substantial. I dislike fleeing on an empty stomach.
CH2_G05_MEAL_THYME,Guest 5,"Thyme with Lillums. Light enough to run after, should running remain necessary."
CH2_G05_SMOKE_CIGAR,Guest 5,"A cigar. For victory, or for pretending."
CH2_G05_SMOKE_PIPE,Guest 5,A pipe. Slow smoke for a slower pulse.
CH2_G05_SMOKE_NONE,Guest 5,No smoke. Keep the air clear and the exits clearer.
CH2_G05_SPIRITS_REPLY,Guest 5,Place it where I can reach it without turning my back.
CH2_G05_EXIT_TO_DINING,Guest 5,Very good. I shall present myself in the Dining Room and recover what dignity remains to us.
CH2_G05_CLOCK_REACTION,Guest 5,"Dining Room, then. Stay together. Walk, do not scatter."
CH2_G05_DINING_REVEAL,Guest 5,Sit where you can see the doors.
CH2_G06_FOUND_START,Guest 6,"Is it gone, or has it merely become quiet?"
CH2_G06_FOUND_REPLY,Guest 6,Yes. Please. Ask me anything that has only two answers.
CH2_G06_MEAL_PLINK,Guest 6,Fresh monte genellion de plink. I refuse to fear a meal with a comic name.
CH2_G06_MEAL_THYME,Guest 6,Thyme with Lillums. That sounds almost medicinal. I accept.
CH2_G06_SMOKE_CIGAR,Guest 6,A cigar. I may need to prove I still possess hands.
CH2_G06_SMOKE_PIPE,Guest 6,A pipe. Something domestic against the screaming violin.
CH2_G06_SMOKE_NONE,Guest 6,No smoke. The room has already burned itself into my memory.
CH2_G06_SPIRITS_REPLY,Guest 6,Good. Tell it I am counting on its courage.
CH2_G06_EXIT_TO_DINING,Guest 6,Very good. I shall present myself in the Dining Room and recover what dignity remains to us.
CH2_G06_CLOCK_REACTION,Guest 6,I would like the next room to contain fewer instruments.
CH2_G06_DINING_REVEAL,Guest 6,I can hear the violin even when it is not playing.
CH2_G07_FOUND_START,Guest 7,I knew the house was awake. I did not know it had pets.
CH2_G07_FOUND_REPLY,Guest 7,Record quickly. The walls have begun pretending not to listen.
CH2_G07_MEAL_PLINK,Guest 7,"Fresh monte genellion de plink. It sounds like a spell, and we may need one."
CH2_G07_MEAL_THYME,Guest 7,Thyme with Lillums. Green things know how to survive old stone.
CH2_G07_SMOKE_CIGAR,Guest 7,"A cigar. Let the smoke mark where I have been, in case I vanish."
CH2_G07_SMOKE_PIPE,Guest 7,A pipe. Smoke curls like warnings when the air is honest.
CH2_G07_SMOKE_NONE,Guest 7,No smoke. I want to smell it if that thing returns.
CH2_G07_SPIRITS_REPLY,Guest 7,Then pour generously. The chateau has had enough of my nerves.
CH2_G07_EXIT_TO_DINING,Guest 7,Very good. I shall present myself in the Dining Room and recover what dignity remains to us.
CH2_G07_CLOCK_REACTION,Guest 7,The chateau wanted us separated. Remember that.
CH2_G07_DINING_REVEAL,Guest 7,The house is quieter now. That worries me more.
CH2_G08_FOUND_START,Guest 8,"Speak plainly. Is the room safe, or merely occupied?"
CH2_G08_FOUND_REPLY,Guest 8,You may. I admire a household that continues taking orders after an omen.
CH2_G08_MEAL_PLINK,Guest 8,Fresh monte genellion de plink. Boldly named food for a cowardly evening.
CH2_G08_MEAL_THYME,Guest 8,"Thyme with Lillums. Quiet, green, and unlikely to announce itself on nine legs."
CH2_G08_SMOKE_CIGAR,Guest 8,A cigar. I intend to leave evidence that I remained composed.
CH2_G08_SMOKE_PIPE,Guest 8,A pipe. The old rituals have teeth; let us use them.
CH2_G08_SMOKE_NONE,Guest 8,No smoke. I want nothing between myself and the door.
CH2_G08_SPIRITS_REPLY,Guest 8,Good. It may be the most trustworthy guest here.
CH2_G08_EXIT_TO_DINING,Guest 8,Very good. I shall present myself in the Dining Room and recover what dignity remains to us.
CH2_G08_CLOCK_REACTION,Guest 8,Then let us disappoint it by arriving intact.
CH2_G08_DINING_REVEAL,Guest 8,"Serve quickly, Butler. The night is not finished with us."
"""


@dataclass(frozen=True)
class DialogueLine:
    line_id: str
    speaker: str
    text: str

    @property
    def guest_number(self) -> int:
        match = re.search(r"Guest\s+(\d+)", self.speaker)
        if not match:
            raise ValueError(f"Cannot parse guest number from speaker: {self.speaker}")
        return int(match.group(1))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate local Chatterbox WAVs for Guest 1-8 dialogue.")
    parser.add_argument("--device", default="cuda", help="Torch device, normally cuda.")
    parser.add_argument("--force", action="store_true", help="Regenerate WAVs even when outputs already exist.")
    parser.add_argument("--max-attempts", type=int, default=2, help="Attempts per generated line before recording failure.")
    parser.add_argument("--limit", type=int, default=0, help="Optional development limit; 0 means all lines.")
    return parser.parse_args()


def shell_command_for_report() -> str:
    parts = [sys.executable, *sys.argv]
    prefix = []
    for key in ("HF_HUB_OFFLINE", "TRANSFORMERS_OFFLINE"):
        value = os.environ.get(key)
        if value:
            prefix.append(f"{key}={shlex.quote(value)}")
    return " ".join([*prefix, shlex.join(parts)])


def set_seed(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed(seed)
        torch.cuda.manual_seed_all(seed)


def load_dialogue() -> list[DialogueLine]:
    rows = list(csv.DictReader(io.StringIO(DIALOGUE_CSV)))
    lines = [DialogueLine(row["line_id"], row["speaker"], row["text"]) for row in rows]
    line_ids = [line.line_id for line in lines]
    if len(lines) != 160:
        raise RuntimeError(f"Expected 160 dialogue lines, found {len(lines)}")
    duplicates = sorted({line_id for line_id in line_ids if line_ids.count(line_id) > 1})
    if duplicates:
        raise RuntimeError(f"Duplicate line IDs: {duplicates}")
    for guest in range(1, 9):
        count = sum(1 for line in lines if line.guest_number == guest)
        if count != 20:
            raise RuntimeError(f"Expected 20 lines for Guest {guest}, found {count}")
    return lines


def ensure_directories() -> None:
    for guest in range(1, 9):
        (VOICE_ROOT / f"Guest{guest:02d}").mkdir(parents=True, exist_ok=True)
    REFERENCE_ROOT.mkdir(parents=True, exist_ok=True)
    ANCHOR_ROOT.mkdir(parents=True, exist_ok=True)
    REPORT_ROOT.mkdir(parents=True, exist_ok=True)
    (REFERENCE_ROOT / ".gitkeep").touch(exist_ok=True)


def deterministic_guid(path: Path) -> str:
    rel = path.resolve().relative_to(PROJECT_ROOT).as_posix()
    return hashlib.md5(f"ChateauElsewhere:{rel}".encode("utf-8")).hexdigest()


def write_folder_meta(folder: Path) -> None:
    if not folder.is_relative_to(PROJECT_ROOT / "Assets"):
        return
    meta = folder.with_name(folder.name + ".meta")
    if meta.exists():
        return
    meta.write_text(
        textwrap.dedent(
            f"""\
            fileFormatVersion: 2
            guid: {deterministic_guid(folder)}
            folderAsset: yes
            DefaultImporter:
              externalObjects: {{}}
              userData:
              assetBundleName:
              assetBundleVariant:
            """
        ),
        encoding="utf-8",
    )


def write_wav_meta(wav_path: Path) -> None:
    meta = wav_path.with_name(wav_path.name + ".meta")
    if meta.exists():
        return
    meta.write_text(
        textwrap.dedent(
            f"""\
            fileFormatVersion: 2
            guid: {deterministic_guid(wav_path)}
            AudioImporter:
              externalObjects: {{}}
              serializedVersion: 8
              defaultSettings:
                serializedVersion: 2
                loadType: 0
                sampleRateSetting: 0
                sampleRateOverride: 44100
                compressionFormat: 1
                quality: 1
                conversionMode: 0
                preloadAudioData: 0
              platformSettingOverrides: {{}}
              forceToMono: 0
              normalize: 1
              loadInBackground: 0
              ambisonic: 0
              3D: 1
              userData:
              assetBundleName:
              assetBundleVariant:
            """
        ),
        encoding="utf-8",
    )


def ensure_unity_metas(expected_wavs: list[Path]) -> None:
    for folder in [
        PROJECT_ROOT / "Assets" / "Audio" / "Voice",
        PROJECT_ROOT / "Assets" / "Audio" / "Voice" / "Guests",
        *[VOICE_ROOT / f"Guest{guest:02d}" for guest in range(1, 9)],
    ]:
        write_folder_meta(folder)
    for wav_path in expected_wavs:
        if wav_path.exists():
            write_wav_meta(wav_path)


def preload_torchcodec_cuda_libs() -> None:
    for site_dir in site.getsitepackages():
        npp_dir = Path(site_dir) / "nvidia" / "npp" / "lib"
        if not npp_dir.exists():
            continue
        libs = [npp_dir / "libnppc.so.12"]
        libs.extend(sorted(path for path in npp_dir.glob("libnpp*.so.12") if path.name != "libnppc.so.12"))
        for lib in libs:
            ctypes.CDLL(str(lib), mode=ctypes.RTLD_GLOBAL)
        return


def load_model(device: str):
    if device.startswith("cuda") and not torch.cuda.is_available():
        raise RuntimeError("CUDA was requested, but torch.cuda.is_available() is False.")
    from chatterbox.tts import ChatterboxTTS

    return ChatterboxTTS.from_pretrained(device=device)


def line_category(line_id: str) -> str:
    if line_id.endswith("_PANIC"):
        return "panic"
    if "_FOUND_" in line_id:
        return "found"
    if line_id.endswith("_DINING_REVEAL"):
        return "dining"
    if "_AMBIENT_" in line_id or line_id.endswith("_EMPTY_BELL_REACTION") or line_id.endswith("_CLOCK_REACTION"):
        return "uneasy"
    return "normal"


def supported_generate_kwargs(model) -> set[str]:
    return set(inspect.signature(model.generate).parameters)


def generate_with_supported_kwargs(model, text: str, kwargs: dict[str, object]) -> torch.Tensor:
    supported = supported_generate_kwargs(model)
    filtered = {key: value for key, value in kwargs.items() if key in supported}
    return model.generate(text, **filtered)


def prepare_wav_tensor(wav: torch.Tensor, sample_rate: int) -> torch.Tensor:
    wav = wav.detach().float().cpu()
    if wav.ndim == 1:
        wav = wav.unsqueeze(0)
    if wav.ndim != 2:
        raise ValueError(f"Expected mono/stereo tensor, got shape {tuple(wav.shape)}")
    wav = torch.nan_to_num(wav, nan=0.0, posinf=0.0, neginf=0.0)
    peak = float(wav.abs().max().item()) if wav.numel() else 0.0
    if peak <= 0.00001:
        raise ValueError("Generated audio is silent.")
    wav = wav * min(TARGET_PEAK / peak, 100.0)
    wav = wav.clamp(-0.999, 0.999)
    leading = torch.zeros((wav.shape[0], int(sample_rate * LEADING_PAUSE_SECONDS)), dtype=wav.dtype)
    trailing = torch.zeros((wav.shape[0], int(sample_rate * TRAILING_PAUSE_SECONDS)), dtype=wav.dtype)
    return torch.cat([leading, wav, trailing], dim=1)


def audio_sanity(wav: torch.Tensor, sample_rate: int, text: str) -> tuple[bool, str]:
    duration = wav.shape[-1] / float(sample_rate)
    word_count = max(1, len(re.findall(r"[A-Za-z]+(?:['’][A-Za-z]+)?", text)))
    min_duration = max(0.55, word_count * 0.11)
    max_duration = max(8.0, word_count * 0.95)
    peak = float(wav.abs().max().item()) if wav.numel() else 0.0
    if peak < 0.01:
        return False, f"peak too low ({peak:.4f})"
    if duration < min_duration:
        return False, f"duration too short ({duration:.2f}s < {min_duration:.2f}s)"
    if duration > max_duration:
        return False, f"duration too long ({duration:.2f}s > {max_duration:.2f}s)"
    return True, f"{duration:.2f}s peak {peak:.3f}"


def save_generated_wav(model, text: str, out_path: Path, sample_seed: int, kwargs: dict[str, object]) -> tuple[str, int]:
    last_reason = ""
    for attempt in range(1, int(kwargs.pop("_max_attempts", 2)) + 1):
        seed = sample_seed if attempt == 1 else sample_seed + (attempt * 10000)
        set_seed(seed)
        wav = generate_with_supported_kwargs(model, text, kwargs)
        wav = prepare_wav_tensor(wav, model.sr)
        ok, reason = audio_sanity(wav, model.sr, text)
        last_reason = reason
        if ok:
            out_path.parent.mkdir(parents=True, exist_ok=True)
            preload_torchcodec_cuda_libs()
            torchaudio.save(str(out_path), wav, model.sr)
            return reason, attempt
    raise RuntimeError(f"Failed audio sanity checks after attempts: {last_reason}")


def reference_for_guest(guest: int) -> tuple[Path, str]:
    ref = REFERENCE_ROOT / f"guest{guest:02d}_ref.wav"
    if ref.exists():
        return ref, "reference clip"
    return ANCHOR_ROOT / f"guest{guest:02d}_anchor.wav", "generated anchor"


def generate_anchor_if_needed(model, guest: int, force: bool, max_attempts: int) -> tuple[Path, str, bool, str]:
    ref = REFERENCE_ROOT / f"guest{guest:02d}_ref.wav"
    if ref.exists():
        return ref, "reference clip", False, "existing reference clip"

    anchor = ANCHOR_ROOT / f"guest{guest:02d}_anchor.wav"
    if anchor.exists() and not force:
        return anchor, "generated anchor", False, "existing generated anchor"

    kwargs = dict(QUALITY_SETTINGS["normal"])
    kwargs["_max_attempts"] = max_attempts
    reason, attempts = save_generated_wav(
        model,
        ANCHOR_TEXT[guest],
        anchor,
        GUEST_SEEDS[guest],
        kwargs,
    )
    return anchor, "generated anchor", True, f"generated in {attempts} attempt(s), {reason}"


def expected_output_path(line: DialogueLine) -> Path:
    return VOICE_ROOT / f"Guest{line.guest_number:02d}" / f"{line.line_id}.wav"


def validate_outputs(lines: list[DialogueLine]) -> tuple[dict[int, int], list[str]]:
    problems: list[str] = []
    expected_paths = {expected_output_path(line) for line in lines}
    actual_paths = set(VOICE_ROOT.glob("Guest??/*.wav"))
    missing = sorted(path for path in expected_paths if not path.exists())
    extra = sorted(path for path in actual_paths if path not in expected_paths)
    if missing:
        problems.append("Missing WAVs: " + ", ".join(path.relative_to(PROJECT_ROOT).as_posix() for path in missing))
    if extra:
        problems.append("Unexpected WAVs: " + ", ".join(path.relative_to(PROJECT_ROOT).as_posix() for path in extra))
    counts = {}
    for guest in range(1, 9):
        count = len(list((VOICE_ROOT / f"Guest{guest:02d}").glob("*.wav")))
        counts[guest] = count
        if count != 20:
            problems.append(f"Guest {guest:02d} has {count} WAV files, expected 20")
    if len(actual_paths) != 160:
        problems.append(f"Total guest WAV count is {len(actual_paths)}, expected 160")
    return counts, problems


def write_report(
    *,
    lines: list[DialogueLine],
    generated: list[str],
    skipped: list[str],
    failed: list[str],
    regenerated: list[str],
    guest_sources: dict[int, str],
    anchor_notes: dict[int, str],
    counts: dict[int, int],
    validation_problems: list[str],
    command: str,
    elapsed_seconds: float,
) -> None:
    total_guest_wavs = sum(counts.values())
    report_lines = [
        "# Guest Voice Generation Report",
        "",
        f"- Command: `{command}`",
        f"- Device requested: `cuda`",
        f"- Offline mode: HF_HUB_OFFLINE={os.environ.get('HF_HUB_OFFLINE', '')}, TRANSFORMERS_OFFLINE={os.environ.get('TRANSFORMERS_OFFLINE', '')}",
        f"- Total generated count: {len(generated)}",
        f"- Total skipped count: {len(skipped)}",
        f"- Total failed count: {len(failed)}",
        f"- Total guest WAV count: {total_guest_wavs}",
        f"- Elapsed seconds: {elapsed_seconds:.1f}",
        "",
        "## Per-Guest Counts",
        "",
    ]
    for guest in range(1, 9):
        report_lines.append(f"- Guest {guest:02d}: {counts.get(guest, 0)} WAV files; source: {guest_sources.get(guest, 'unknown')}; {anchor_notes.get(guest, '')}")
    report_lines.extend(["", "## Regenerated Lines", ""])
    if regenerated:
        report_lines.extend(f"- {item}" for item in regenerated)
    else:
        report_lines.append("- None")
    report_lines.extend(["", "## Failed Lines", ""])
    if failed:
        report_lines.extend(f"- {item}" for item in failed)
    else:
        report_lines.append("- None")
    report_lines.extend(["", "## Validation", ""])
    if validation_problems:
        report_lines.extend(f"- {problem}" for problem in validation_problems)
    else:
        report_lines.append("- PASS: all 160 expected guest WAVs exist.")
        report_lines.append("- PASS: each Guest01 through Guest08 folder contains exactly 20 WAV files.")
        report_lines.append("- PASS: every expected line_id has a matching WAV filename.")
    report_lines.extend(["", "## Expected Dialogue IDs", ""])
    for line in lines:
        report_lines.append(f"- {line.line_id}: {line.text}")
    REPORT_PATH.write_text("\n".join(report_lines) + "\n", encoding="utf-8")


def main() -> int:
    started = time.monotonic()
    args = parse_args()
    os.chdir(PROJECT_ROOT)
    lines = load_dialogue()
    if args.limit:
        lines = lines[: args.limit]
    ensure_directories()

    model = load_model(args.device)
    expected_paths = [expected_output_path(line) for line in lines]

    generated: list[str] = []
    skipped: list[str] = []
    failed: list[str] = []
    regenerated: list[str] = []
    guest_sources: dict[int, str] = {}
    anchor_notes: dict[int, str] = {}
    prompt_paths: dict[int, Path] = {}

    for guest in range(1, 9):
        prompt_path, source, did_generate, note = generate_anchor_if_needed(model, guest, args.force, args.max_attempts)
        prompt_paths[guest] = prompt_path
        guest_sources[guest] = f"{source} ({prompt_path.relative_to(PROJECT_ROOT).as_posix()})"
        anchor_notes[guest] = note
        if did_generate:
            print(f"generated anchor guest{guest:02d}: {prompt_path}")
        else:
            print(f"using {source} guest{guest:02d}: {prompt_path}")

    for index, line in enumerate(lines, start=1):
        out_path = expected_output_path(line)
        rel = out_path.relative_to(PROJECT_ROOT).as_posix()
        if out_path.exists() and not args.force:
            skipped.append(line.line_id)
            print(f"[{index:03d}/{len(lines):03d}] skip {line.line_id}")
            continue

        category = line_category(line.line_id)
        settings = dict(QUALITY_SETTINGS[category])
        settings["audio_prompt_path"] = str(prompt_paths[line.guest_number])
        settings["_max_attempts"] = args.max_attempts
        try:
            reason, attempts = save_generated_wav(
                model,
                line.text,
                out_path,
                GUEST_SEEDS[line.guest_number],
                settings,
            )
            generated.append(line.line_id)
            if attempts > 1:
                regenerated.append(f"{line.line_id}: regenerated after {attempts - 1} failed sanity attempt(s)")
            print(f"[{index:03d}/{len(lines):03d}] generated {rel} ({category}, {reason})")
        except Exception as exc:
            failed.append(f"{line.line_id}: {exc}")
            print(f"[{index:03d}/{len(lines):03d}] FAILED {line.line_id}: {exc}", file=sys.stderr)

    ensure_unity_metas(expected_paths)
    counts, validation_problems = validate_outputs(load_dialogue())
    if failed:
        validation_problems.append(f"{len(failed)} generation failure(s) recorded")
    write_report(
        lines=load_dialogue(),
        generated=generated,
        skipped=skipped,
        failed=failed,
        regenerated=regenerated,
        guest_sources=guest_sources,
        anchor_notes=anchor_notes,
        counts=counts,
        validation_problems=validation_problems,
        command=shell_command_for_report(),
        elapsed_seconds=time.monotonic() - started,
    )
    if validation_problems or failed:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
