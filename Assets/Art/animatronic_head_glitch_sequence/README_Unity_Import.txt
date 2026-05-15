Animatronic Head Glitch Sequence
==================================

This pack contains 16 transparent PNG frames of the head glitching.

Important:
- Every PNG in cropped_same_frame/ is exactly 540 x 603 px.
- Every PNG in aligned_full_canvas/ is exactly 956 x 1645 px.
- The alpha mask is identical across frames, so the silhouette/shape does not pop or shift.
- The background is true alpha transparency. There is no checkerboard baked into the image.
- Effects are applied inside the head silhouette: static, scanlines, RGB split, blur, eye flicker, and horizontal glitch bands.

Unity import suggestion:
1. Drag the PNG frames into your project.
2. Set Texture Type = Sprite (2D and UI).
3. Set Sprite Mode = Single for individual files.
4. Disable compression or use high quality compression to avoid alpha artifacts.
5. Use the cropped_same_frame frames for a head-only animation, or aligned_full_canvas if layering over the full-body sprite from the previous pack.
6. Suggested frame rate: 10-14 FPS. Try 12 FPS first.

If using the spritesheet:
- cropped spritesheet cell size: 540 x 603 px.
- aligned spritesheet cell size: 956 x 1645 px.
- Slice as a 4 columns x 4 rows grid.
