# Chateau Elsewhere Character Animation Pipeline

This project needs character acting, not procedural wobble. The production rule is:

1. Existing in-game/source art is reference.
2. AI or hand-painted candidates land in intake.
3. Only reviewed, cropped, alpha-clean PNGs move to approved runtime art.

The active library root is:

```text
Assets/Art/Library/AnimationLibrary/
```

## Why This Pipeline Exists

The failed prototype deformed whole sprites and drew minimal arm accents. That cannot produce believable panic acting because the costume mass, hands, sleeves, legs, and silhouette never receive real pose art.

Professional 2D game animation for these characters should use two asset lanes:

1. Full-body pose sequences for strong acting beats.
2. Separated part sheets for rigged or sprite-swap animation.

Use full-body frames for moments like shrieking, cowering, skidding, and sprinting. Use separated parts for reusable motion: head turns, hand variants, arm raises, torso bends, coat tails, skirts, legs, and facial expressions.

## Folder States

Each character gets this structure:

```text
Assets/Art/Library/AnimationLibrary/<Character>/
  reference/
    source_sheet.png
    full_body/<existing_clip_frames>/
  intake/
    full_body/<requested_action>/
    parts/<requested_part_sheet>/
  approved/
    full_body/<requested_action>/frames/
    parts/<requested_part_sheet>/
  qa/reference_contact_sheet.png
  requests/generation_prompts.jsonl
  requests/README.md
  manifest.json
```

`reference` is trusted art copied from the current project.

`intake` is where generated sprite sheets or manually painted sheets go first.

`approved` is the only place new art should be consumed by gameplay builders.

## AI Generation Strategy

Do not generate every possible body-part combination. That becomes a combinatorial mess and still needs animation direction.

Generate controlled source material:

1. Full-body action sheets: panic reaction, shriek, panic run left/right, turnarounds, cower.
2. Rig part sheets: front, left, right, heads, hands, arms, legs, coat/skirt overlays, props.
3. Expression and hand libraries: small reusable swaps.

Prompts are written per character in:

```text
Assets/Art/Library/AnimationLibrary/<Character>/requests/generation_prompts.jsonl
```

Every prompt tells the image model to match the current character art and explicitly forbids stick limbs, tilted idle bodies, pasted stickers, modern clothing, shadows, labels, and text.

## Approval Checklist

Before moving any generated PNG into `approved`:

1. The character is recognizably the exact guest or butler.
2. The sprite matches the painterly Victorian style and costume colors.
3. Hands, arms, legs, sleeves, coats, skirts, and props are real painted shapes.
4. The sprite crops to `166 x 297` with bottom-center pivot.
5. Alpha is clean with no background fringe.
6. Feet are consistent enough for motion without sliding.
7. The contact sheet reads as acting from game camera distance.

## Unity Integration

The project already has:

```text
com.unity.2d.animation
com.unity.2d.psdimporter
```

That means the approved assets can feed either:

1. Standard sprite-frame AnimationClips.
2. Unity 2D Animation rigs and Sprite Library swaps.

For panic, prefer full-body clips first. Add rigged part animation when repeated gestures need reuse across many beats.

## Commands

Build or refresh the animation library:

```bash
python Tools/chateau_animation_pipeline.py
```

This does not overwrite original character art. It copies reference frames, writes manifests, and creates empty intake/approved folders for production work.
