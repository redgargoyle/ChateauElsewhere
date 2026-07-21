# Universal character display-scale legacy removal audit

## Result

The Butler and all managed Guests now have one display-size authority: `CharacterDisplayScaleController`. It writes only each subject's dedicated animation/visual child. Movement, room assignment, seating, projection, panic, animation, and sorting systems do not own body display scale.

The interim `CharacterScaleCatalog`, `CharacterScaleFunction`, `CharacterScaleRoom`, `CharacterScaleTool`, their Resources asset, their scene components/handles, and architecture tests were removed rather than retained as compatibility shims.

## Files inspected

Runtime inspection covered:

- `Assets/Scripts/PointClickPlayerMovement.cs`
- `Assets/Scripts/Story/ActorRoomState.cs`
- `Assets/Scripts/Characters/RoomPersonWalker2D.cs`
- `Assets/Scripts/Characters/RoomProjectedEntity.cs`
- `Assets/Scripts/Characters/RoomPerspectiveProfile.cs`
- `Assets/Scripts/Characters/CharacterVisualProfile.cs`
- `Assets/Scripts/Characters/GuestRoomScaleApplier.cs`
- `Assets/Scripts/Characters/GuestRoomScaleCalibration.cs`
- `Assets/Scripts/Characters/GuestScaleParticipant.cs`
- `Assets/Scripts/Characters/GuestRoomStageScaleUtility.cs`
- `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs`
- `Assets/Scripts/Characters/CharacterAnimationDisplay.cs`
- `Assets/Scripts/Characters/CharacterFloorReference.cs`
- `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`
- all C# `localScale` assignments and all AnimationClip scale bindings under `Assets`

Editor/test inspection covered:

- `Assets/Editor/ButlerRoomScaleCalibrationWindow.cs`
- `Assets/Editor/GuestRoomScaleMasterWindow.cs`
- `Assets/Editor/GuestScaleAudit.cs`
- `Assets/Editor/GuestButlerScaleRegressionTests.cs`
- `Assets/Editor/RoomProjectionCalibrationWindow.cs`
- `Assets/Editor/RoomPerspectiveProfileEditor.cs`
- `Assets/Editor/RoomProjectedEntityEditor.cs`
- `Assets/Editor/RoomProjectionRegressionTests.cs`
- `Assets/Editor/StoryActorRoomStageLockingTests.cs`
- `Assets/Editor/PointClickPlayerMovementEditor.cs`
- current ownership, animation, room-stage, Chapter 1, Chapter 2, sorting, and collision regression fixtures

The named guest/projection legacy files were already absent at the starting SHA. Their absence and lack of serialized references were rechecked. Environment/non-character visual scale assignments remain outside this ownership rule.

## Ownership removed or stripped

- `PointClickPlayerMovement` retains movement and exposes a read-only current floor point; it no longer owns Butler display size or mutates navigation state for scale evaluation.
- `ActorRoomState` retains room/seated/visibility data; body-scale binding and room-stage scale compensation were removed.
- `CharacterAnimationDisplay` is now a passive animation-root descriptor.
- Chapter 1 arrival logic no longer settles or restores scale. It only ensures the correct Guest display identity after creating a fallback actor.
- Chapter 2 panic, room walking, projection, and sorting code have no character body-scale assignment.
- The dedicated controller contains the sole production assignment to a managed character visual root's `localScale`.

## Deleted files

- `Assets/Scripts/Characters/CharacterScaleCatalog.cs` and meta
- `Assets/Scripts/Characters/CharacterScaleFunction.cs` and meta
- `Assets/Scripts/Characters/CharacterScaleRoom.cs` and meta
- `Assets/Editor/CharacterScaleTool.cs` and meta
- `Assets/Editor/CharacterScaleArchitectureTests.cs` and meta
- `Assets/Resources/CharacterScaleCatalog.asset` and meta

Obsolete serialized fields and inspector UI disappeared with those types. `Assets/Scenes/New Scene.unity` also had an unrelated dangling Input System UI component removed during the cleanup.

## Serialized migration

`Player.prefab` now carries a `CharacterDisplayScaleSubject` targeting its `AnimationDisplay` child. Gameplay's eight Guest prefab instances override the identity to `Guest1` through `Guest8`; the Butler remains `Butler`. Chapter 1 assigns the same identity when it creates or clones a fallback Guest.

Gameplay's 19 `CharacterScaleRoom` components and all 19 EditorOnly `Character Scale/Front/Back` marker hierarchies were removed before deleting the old script. The new catalog contains 19 migrated room defaults. There are no old component GUID references, marker names, or missing-script placeholders from the removed stack.

## Final authority confirmation

The final scale is recomputed from catalog data, room-local floor Y, character id, and one of exactly three states: Normal, Drawing Room seated, or Dining Room seated. It never multiplies prior scale, stage zoom, a hidden Guest multiplier, or a historical Butler basis. X/Y facing signs and authored Z are preserved while the catalog magnitude remains authoritative.
