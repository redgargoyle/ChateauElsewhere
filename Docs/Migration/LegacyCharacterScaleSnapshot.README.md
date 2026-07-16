# Legacy character-scale snapshot

`LegacyCharacterScaleSnapshot.json` is a reference-only export of the values removed during the Phase 1 character-presentation cleanup. It is deliberately outside `Assets` and must never be loaded by runtime code.

The snapshot uses plain JSON primitives. Vectors and colors are named component objects; room and character collections are arrays keyed by `roomId` or `characterId`; curve keys preserve Unity's serialized key data. `front` means the near/lower-foot-Y endpoint and `back` means the far/higher-foot-Y endpoint. `sceneSerializedScale` records the transform value present at the starting commit, while `capturedBaseScale` or `authoredScale` records the legacy system's saved reference value.

The replacement architecture used this file once to seed direct Front/Back room objects. Runtime does not read it. The replacement may own one explicit shared linear Y function and one room-stage zoom conversion; it must not preserve, reproduce, or invoke legacy components, formulas, guest multipliers, per-character fine tunes, profiles, projection, tint, shadow, or competing runtime scale writers.
