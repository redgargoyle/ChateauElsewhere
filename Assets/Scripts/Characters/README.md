# Room People

People are authored as real scene objects under each room's `People` child. They move with the room stage, so panning and zooming keep them locked to the painted background.

`RoomPersonWalker2D` uses a transparent sprite atlas on a UI `RawImage`. The path points are room-local coordinates, and the walker scales/tints itself from `Near Y` to `Far Y` so it feels embedded in the room instead of pasted over it.

Foreground occlusion uses a simple 90s prerendered trick: `RoomForegroundOccluder` objects are editable `RawImage` crops of the room painting placed above the `People` layer. Put them over railings, table edges, and other foreground furniture so walkers can pass behind those objects without a 3D setup.

Current examples:

- `Gameplay.unity > Canvas_Background > Rooms > Room_Grand_Entrance_Hall > People > Walker_GEH_GreenGentleman`
- `Gameplay.unity > Canvas_Background > Rooms > Room_Grand_Entrance_Hall > People > Walker_GEH_GreenLady`
- `Gameplay.unity > Canvas_Background > Rooms > Room_Grand_Entrance_Hall > ForegroundOccluders`

Useful tweaks:

- Move path points on `RoomPersonWalker2D` to change where a person walks.
- Resize the `RawImage` RectTransform to change character height.
- Adjust `Near Y`, `Far Y`, `Near Scale`, and `Far Scale` for perspective.
- Add foreground occluder cards whenever a walker should disappear behind painted furniture.
