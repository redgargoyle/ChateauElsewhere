# Guest Scaling Writers Audit

These are the scale writers found before implementing Butler-derived guest scaling.

- `PointClickPlayerMovement` writes the Butler `Transform.localScale` from manually saved per-room front/back Butler calibration, or from legacy room perspective/fallback scale when no complete Butler calibration exists.
- `RoomProjectedEntity` writes projected guest visual-root scale in `ApplyProjectedScale`. It multiplies room/profile scale, `CharacterVisualProfile.HeightScaleMultiplier`, room-stage scale, and authored or per-room visual-root overrides.
- `RoomProjectedEntity` also normalizes its logical root scale when `normalizeLogicalRootScale` is enabled.
- `RoomPersonWalker2D` writes walker `RectTransform.localScale` in `ApplyVisuals` from `GetDepthScale`, with direct X mirroring through `facingSign`.
- `ActorRoomState` writes bound world-space actor `Transform.localScale` when following room-stage motion, using the actor's bound local scale, room-stage scale ratio, and bound `RoomPerspectiveProfile`.
- `Chapter1ArrivalController` preserves/restores guest root scale, disables inherited player scale components on guests, configures projected guests, and can write coat/sprite visual scale for carried coat visuals.
- `CharacterVisualProfile.HeightScaleMultiplier` intentionally changes guest identity height and remains part of projected guest scale.
- `RoomProjectedEntity.roomVisualScaleOverrides` and `useRoomVisualScaleOverrides` can apply old per-room manual guest visual scale values.
- Scene and prefab YAML contain authored `m_LocalScale`, `m_SizeDelta`, `roomVisualScaleOverrides`, `nearScale`, and `farScale` values that can affect the visible size of UI/Image-based and projected guests.
- `PlayModeLayoutCaptureWindow` can persist play-mode `localScale` and `sizeDelta` changes back into scenes.

`GuestButlerScaleHarmonizer` now runs late and calls `ApplyButlerCharacterScaleNow` on `RoomProjectedEntity`, `RoomPersonWalker2D`, and world-space `ActorRoomState` guests so these older scale writers cannot silently hide the Butler-derived guest scale.
