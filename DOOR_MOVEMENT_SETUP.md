# Door Movement Setup

## What Hamza Still Does

Yes: Hamza manually places the door click rectangles, because only he can see exactly where each door is in the final room art.

The code now does everything else:

- loads `Assets/Resources/Navigation/doors.txt`
- tracks the current room
- moves rooms when a door is clicked
- disables non-current-room doors
- toggles the map with `M`
- prevents map buttons from moving the player

## Fast Setup

1. Open the gameplay scene in Unity.
2. Run `ChataeuChatilly > Navigation > Create Room Visual Catalog From Map Buttons`.
3. Run `ChataeuChatilly > Navigation > Create Door Button Placeholders From Door Data`.
4. In the Hierarchy, open:

```txt
Canvas_Background
  DoorButtons
    Room_Kitchen
      Door_K1
      Door_K2
```

5. For each yellow `Door_*` rectangle, drag/resize it over the actual visible door in that room image.
6. Press Play and click a door.
7. Run `ChataeuChatilly > Navigation > Validate Door Data` whenever something feels wrong.

## Replacing Door Data

Replace `Assets/Resources/Navigation/doors.txt` with Hamza's final data:

```txt
Kitchen:
K1: Ballroom
K2: Hallway

Study:
S1: Ballroom
```

Room names, door IDs, and visual catalog room names must match exactly.

## Important

Door movement is directional. If `Kitchen -> Ballroom` exists, that does not automatically create `Ballroom -> Kitchen`.

Add both:

```txt
Kitchen:
K1: Ballroom

Ballroom:
B1: Kitchen
```
