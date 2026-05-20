# Ideas

Ideas are the quest layer. The player does not go on a quest; they explore an Idea, and the house starts to answer from that frame of reference.

## Runtime Pieces

- `IdeaManager` owns the current Idea. If none exists, `IdeaDimension`, `IdeaWorldTint`, or `IdeaEntryPoint` can create one at runtime.
- `IdeaDimension` belongs on an interactable root. Assign one neutral child root and one child root per Idea that should look or behave differently.
- `IdeaEntryPoint` can be placed on a clickable object or called from a UnityEvent to start an Idea, enter Elsewhere, or clear the current Idea.
- `IdeaWorldTint` belongs on a full-screen UI `Image` above the room art with raycasts disabled. It uses the current Idea color as a light filter.

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
