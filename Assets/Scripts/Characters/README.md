# Character display architecture

The Butler and all managed Guests share one display-scale pipeline under `DisplayScale/`.

`CharacterDisplayScaleSubject` identifies a character and its dedicated `AnimationDisplay` child. It reads room, stable floor position, and seated state without changing gameplay state. `CharacterDisplayScaleController` is the sole production writer to that visual root's `localScale`; it evaluates `CharacterDisplayScaleCatalog` from authored room-local Front/Back Y values on every update. The calculation is absolute and never multiplies the previous frame.

Movement, room assignment, seating, animation selection, sorting, and story systems may expose read-only context, but must not resize character bodies. The only state-specific scale values allowed are the Drawing Room seated and Dining Room seated overrides stored in the catalog.

The canonical asset is `Assets/_Chateau/Data/Resources/CharacterDisplayScaleCatalog.asset`. Edit it through `Tools/Chateau/Universal Character Display Scale`; previews are temporary and restored automatically.

`CharacterAnimationDisplay` is only a descriptor for the animation child. It does not calculate or apply display scale.
