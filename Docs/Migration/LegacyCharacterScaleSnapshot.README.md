# Legacy character-scale snapshot

`LegacyCharacterScaleSnapshot.json` is a reference-only export of values discovered during the legacy cleanup. It lives outside `Assets` and runtime code must never load it.

The snapshot was used once to seed the 19 room defaults in `Assets/_Chateau/Data/Resources/CharacterDisplayScaleCatalog.asset`. Runtime has no fallback to the snapshot, no scene calibration handles, and no second source of scale values.

For current tuning, use `Tools/Chateau/Universal Character Display Scale`. Front/Back positions are editor authoring data; runtime consumes their room-local Y values, endpoint scales, and curve. Only an explicit catalog save publishes changes.
