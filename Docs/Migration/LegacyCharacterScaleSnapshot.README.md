# Legacy character-scale snapshot

`LegacyCharacterScaleSnapshot.json` is a reference-only export of the values removed during the Phase 1 character-presentation cleanup. It is deliberately outside `Assets` and must never be loaded by runtime code.

The snapshot uses plain JSON primitives. Vectors and colors are named component objects; room and character collections are arrays keyed by `roomId` or `characterId`; curve keys preserve Unity's serialized key data. `front` means the near/lower-foot-Y endpoint and `back` means the far/higher-foot-Y endpoint. `sceneSerializedScale` records the transform value present at the starting commit, while `capturedBaseScale` or `authoredScale` records the legacy system's saved reference value.

The replacement architecture used this file once to seed the saved room definitions in `Assets/Resources/CharacterScaleCatalog.asset`. That ScriptableObject asset is now the sole runtime authority for Front/Back Y and scale values. Runtime does not read this snapshot or scene calibration handles.

The `Character Scale/Front/Back` scene objects are editor-only drafts. They may be loaded from the catalog and moved for calibration, but changing them or saving the scene does not update runtime data. Only the Character Scale Tool's explicit `Save Handles To Asset` and `Save All Loaded Handles To Asset` actions persist draft values to the Resources catalog.

The replacement may own one explicit shared linear Y function and one room-stage zoom conversion; it must not preserve, reproduce, or invoke legacy components, formulas, guest multipliers, per-character fine tunes, profiles, projection, tint, shadow, or competing runtime scale writers. The snapshot must not become an automatic fallback or a second way to rebuild runtime calibration.
