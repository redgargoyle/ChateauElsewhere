# F5-TTS Guest 6 and 7 Game-Dialogue Anchor Report

These anchors are for local generation only. Unity gameplay, subtitles, and playback hooks were not modified.

- Started: 20260619_195638
- Elapsed seconds: 3.8
- CUDA device: NVIDIA GeForce RTX 5090
- `nvidia-smi`: NVIDIA GeForce RTX 5090, 610.43.02, 32607 MiB
- Final format: 48000 Hz, mono, PCM_16 WAV, peak-normalized near -3 dBFS.
- Purpose: convert approved RP samples into game-dialogue reference anchors so the full 160-line pass uses game-dialogue-only ref_text.

## Anchors

- Guest 06 Lady Sabine Marrow
  - output: `/home/hamzak/Desktop/ChataeuChantilly/Tools/Voice/reference_clips/game_dialogue_anchors/guest06_game_anchor.wav`
  - final_game_ref_text: `Thank you. I nearly mistook the bell pull for a funeral cord.`
  - seed: 46606
  - speed: 0.84
  - peak: 0.7079
  - duration: 3.720s
- Guest 07 Lord Ambrose Veil
  - output: `/home/hamzak/Desktop/ChataeuChantilly/Tools/Voice/reference_clips/game_dialogue_anchors/guest07_game_anchor.wav`
  - final_game_ref_text: `Lovely to see you. The chateau looks almost awake tonight.`
  - seed: 46707
  - speed: 0.88
  - peak: 0.7079
  - duration: 6.024s
