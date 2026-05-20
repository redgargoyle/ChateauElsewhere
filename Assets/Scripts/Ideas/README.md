# Ideas

Ideas are the quest layer. The player does not go on a quest; they explore an Idea, and the house starts to answer from that frame of reference.

## Runtime Pieces

- `IdeaManager` owns the current Idea. If none exists, `IdeaDimension`, `IdeaWorldTint`, or `IdeaEntryPoint` can create one at runtime.
- `IdeaDimension` belongs on an interactable root. Assign one neutral child root and one child root per Idea that should look or behave differently.
- `IdeaEntryPoint` can be placed on a clickable object or called from a UnityEvent to start an Idea, enter Elsewhere, or clear the current Idea.
- `IdeaWorldTint` belongs on a full-screen UI `Image` above the room art with raycasts disabled. It uses the current Idea color as a light filter.
- `IdeaGameplayUI` creates the in-game Ideas overlay in Gameplay: active Idea, Ideas menu, selection readout, simple world-note placement, and the new-game tutorial.
- `IdeaWorldObject` belongs on clickable world objects that should appear in the selection readout.

## Built-In Idea IDs

- `inheritance` - The Inherited Shape
- `appetite` - The Patient Appetite
- `witness` - The Witness in the Glass
- `elsewhere` - Elsewhere, The Odd Place

## Authoring Pattern

Create interactable children like this:

```text
Portrait_Frame
  Neutral
  Idea_Inheritance
  Idea_Appetite
  Idea_Witness
  Idea_Elsewhere
```

Put `IdeaDimension` on `Portrait_Frame`. Assign `Neutral` as the neutral root, then add variants with the Idea IDs above and assign each matching child root. Any Idea without a variant can fall back to the neutral root.

## Gameplay UI

New Game marks the Ideas tutorial as pending. When Gameplay loads, `IdeaGameplayUIBootstrap` creates the overlay only if the scene has both `CameraManager` and `Canvas_Background`.

The overlay is intentionally small:

- the top-left Ideas button opens the Ideas menu
- the top-left status shows the active Idea or Elsewhere
- the bottom readout shows the hovered door or selected `IdeaWorldObject`
- Place Marker lets you click the current room image and leave a small selectable marker
