# Demo V1 Scale Calibration Guide

Goal: every room should have a saved Butler front/back scale curve, and guests should normally follow that Butler depth curve through Guest Size Master. Guests should get larger closer to camera and smaller farther away, following the same room-local Y depth law as the Butler, with a room multiplier for small visual corrections.

## Rules

- Calibrate in Edit Mode.
- Butler first, guests second.
- Use front/closest for the largest visible character size.
- Use back/furthest for the smallest visible character size.
- Do not manually edit guest or Butler Transform scale in the Inspector as the saved calibration.
- Prefer `Follows Butler Depth`. `Fixed Size / Seated Exception` is only an advanced fallback for a static one-off problem.
- Save the scene after each room that looks correct.

## Room Loop

For each room in the demo route:

1. Open `Tools > Butler > Room Scale Calibration`.
2. Select the room.
3. Put the Butler at the closest/front useful floor point for that room.
4. Adjust `Preview Final Butler Local Scale` until the Butler looks correct.
5. Click `SAVE FRONT: Current Position + Current Visible Size`.
6. Put the Butler at the furthest/back useful floor point for that room.
7. Adjust `Preview Final Butler Local Scale` until the Butler looks correct.
8. Click `SAVE BACK: Current Position + Current Visible Size`.
9. Open `Tools > Characters > Guest Size Master`.
10. Select the same room.
11. Set `Guest Scale Mode` to `Follows Butler Depth`.
12. Adjust `Guest Size Multiplier`.
13. Click `PREVIEW SELECTED ROOM`.
14. If the guests look correct, click `SAVE SELECTED ROOM`, then `SAVE SCENE`.
15. If the room needs custom front/back guest sizes, use the manual guest steps below.

## Manual Guest Tweaks

Use this only after `Follows Butler Depth` cannot match the room well enough.

1. Select a guest near the closest/front point in the Scene or Hierarchy.
2. Set `Guest Scale Mode` to `Custom Front/Back Curve`.
3. Adjust `Closest Guest Scale`.
4. Click `PREVIEW SELECTED GUEST SIZE`.
5. Click `SAVE CLOSEST POINT FROM SELECTED GUEST`.
6. Select a guest near the furthest/back point.
7. Adjust `Furthest Guest Scale`.
8. Click `PREVIEW SELECTED GUEST SIZE`.
9. Click `SAVE FURTHEST POINT FROM SELECTED GUEST`.
10. Click `PREVIEW SELECTED ROOM`.
11. Click `SAVE SELECTED ROOM`, then `SAVE SCENE`.

## Quick Validation

After each room:

- Move a guest from the back of the room toward the camera.
- The guest should grow smoothly.
- Move the guest away from the camera.
- The guest should shrink smoothly.
- Zoom the camera.
- The guest and Butler should stay visually stable relative to the room-stage zoom behavior.
- If guests do not change size with depth, check that Guest Size Master says the room is using a depth curve and that both closest and furthest points are saved.

## What To Avoid

- Do not use fixed room size for demo rooms unless a guest never moves in depth.
- Do not use all-room/all-guest bulk scaling while calibrating the demo route.
- Do not clear or reset a room after saving unless you intend to recalibrate it.
- Do not commit or ship a room where only one guest depth point is saved.

## Recommended Demo Order

1. Grand Entrance Hall.
2. Drawing Room.
3. Dining Room.
4. Any room reachable in the demo route after those.

This order starts with the room where guest walking depth already appears closest to correct, then moves through the rooms most visible in the demo.
