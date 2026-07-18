# Natural Delivery Fish Audio S2 Repair Report

Targeted quality repair for the Butler address and the live Guest 5/7 order-taking lines.

- Started: `20260718_092858`
- Elapsed seconds: `100.8`
- Model: `/home/hamzak/ai-tts/fish-speech-s2/fish-speech-src/checkpoints/s2-pro`
- Device: `NVIDIA GeForce RTX 5090`
- Candidate policy: varied sampling; reject long-pause or flat takes; pitch-preserving speed correction capped at 1.08x.
- Guest post-roll: `0.25s`; Butler pre-stinger post-roll: `0.15s`.
- Staging and backups: `/tmp/chateau_natural_delivery_fish_s2_20260718_092858`
- Unity `.meta` files changed: `False`

## Installed lines

- `CH2_G07_MEAL_PLINK` — Lord Ambrose Veil
  - Text: Fresh monte genellion de plink. It sounds like a spell, and we may need one.
  - Candidates: #1 seed=1677712 rate=2.84w/s pause=0.36s pitch=6.0st score=0.728 pass=False, #2 seed=1678721 rate=2.89w/s pause=0.39s pitch=5.6st score=0.628 pass=True, #3 seed=1679730 rate=2.86w/s pause=0.30s pitch=5.7st score=0.354 pass=True, #4 seed=1680739 rate=2.81w/s pause=0.44s pitch=5.5st score=0.877 pass=False, #5 seed=1681748 rate=3.08w/s pause=0.26s pitch=7.0st score=0.557 pass=True
  - Selected: #3; spoken=5.241s; rate=2.862w/s; internal silence=0.570s; longest pause=0.300s; pitch range=5.69st; tempo=1.000x
  - Final duration=5.491s; tail=0.25s; peak=0.7079
  - Asset: `/home/hamzak/Desktop/ChataeuChantilly/Assets/Audio/Voice/Guests/Guest07/CH2_G07_MEAL_PLINK.wav`
