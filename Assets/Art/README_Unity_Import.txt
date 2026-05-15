Animatronic Unity Proportional Sprite Parts — Corrected
======================================================

This redo uses ONLY the major component groups you asked for:

- 01_head.png — head + both ears
- 02_left_arm_viewer_left.png — full arm from shoulder to fingers
- 03_right_arm_viewer_right.png — full arm from shoulder to fingers
- 04_left_leg_viewer_left.png — full leg from hip to foot
- 05_right_leg_viewer_right.png — full leg from hip to foot
- 06_torso.png — chest, bowtie, abdomen, waist/pelvis

No random upper-arm/forearm/shin/foot fragments. Cuts are placed at natural neck, shoulder, and hip boundaries.

Folders:
- aligned_full_canvas/: all sprites keep the original 956x1645 canvas. Put them at the same Unity transform position and they line up.
- cropped_parts/: trimmed versions for rigging/animation. Use Sprite Editor to set pivots.
- preview/: region and stack checks.
- 00_full_character_clean_transparent.png: complete cleaned transparent character.
- manifest.json: crop boxes and pivot notes.

Unity import settings:
- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Alpha Is Transparency: ON
- Compression: None or High Quality
- Mesh Type: Full Rect for aligned sprites; Tight can work for cropped sprites.

Suggested sorting order, back to front:
left_leg, right_leg, torso, head, left_arm, right_arm.

All PNGs are true-alpha transparent. The checker pattern from the generated reference was removed.
