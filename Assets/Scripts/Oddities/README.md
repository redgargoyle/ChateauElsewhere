# Room Oddities

Oddities are authored as real scene objects under a room's `Oddities` child, the same way room lights live under `Lighting`.

For animated picture-frame oddities, use `OdditySpriteAnimator` on a UI `Image`. Put it inside a small `RectMask2D` window that matches the inside of the painted frame, then resize the image child until the motion feels embedded in the background instead of pasted on top.

Current example:

- `Gameplay.unity > Canvas_Background > Rooms > Room_Grand_Entrance_Hall_Rear_view > Oddities > Oddity_GEH_Rear_RightPortraitWatcher`

Useful tweaks:

- Move/resize the oddity root to fit the frame.
- Move/resize `PortraitImage_LookingLady` inside the mask to control the crop.
- Adjust `Tint`, `Alpha Flicker`, and `Scale Pulse` on `OdditySpriteAnimator` for a warmer, older, 90s-style composite.
