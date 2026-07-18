# Chateau Chantilly / Chateau Elsewhere Game Script Source of Truth

Chapters 1, 2, and the handoff into Chapter 3.

Generated from the current local project state on 2026-06-20.

## Scope

This document is the working production script for testing and debugging Chapter 1, Chapter 2, and the beginning/pending state of Chapter 3. It replaces older compiled scripts where those scripts disagree with the current subtitle bank, voice catalog, or chapter controllers.

Every quoted dialogue line below is copied from the current Unity subtitle/voice data. Do not change dialogue text in gameplay, subtitles, or audio generation without updating this document at the same time.

## Primary Sources

- `Assets/Resources/UI/SubtitleLineBank.asset` for exact subtitle text.
- `Assets/Resources/Audio/GuestVoiceLineCatalog.asset` for voice-backed line IDs.
- `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs` for Chapter 1 timing, guest flow, and completion gate.
- `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs` for Chapter 2 phases, objectives, and chapter handoff.
- `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs` for guest search, preference recording, and dining order.
- `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2MonsterStingerController.cs` and `Chapter2GuestPanicController.cs` for monster and panic beats.
- `Assets/Scripts/Story/ChapterManager.cs` for chapter IDs, fade transitions, and skip behavior.

## Runtime Status Key

- Current gameplay path: current code calls this line or an alias that resolves to this voice line during normal play.
- Voice/subtitle asset-backed: the line exists in the subtitle bank and voice catalog, but it may still need a gameplay hook in the relevant beat.
- Chapter 3 pending: Chapter 3 has no real controller yet; the current game reaches a pending/requested state only.

## Dialogue Coverage Check

- Voice catalog entries: 186.
- Guest voice/subtitle lines: 168.
- Butler voice/subtitle lines: 18.
- Every speech WAV under `Assets/Audio/Voice` should have a subtitle entry with the same line ID.
- Active speech should be cut only by chapter skip, teleport, or leaving the room where the speaker is present.
- If a speech line is cut by leaving the room, returning to that room should resume after the cut line, not replay the cut line.

## Cast

| Guest ID | Character name |
| --- | --- |
| Guest 1 | Miss Isolde Wren |
| Guest 2 | Professor Lucien Vale |
| Guest 3 | Mister Florian Knell |
| Guest 4 | Countess Elowen Dusk |
| Guest 5 | Baron Hector Glass |
| Guest 6 | Lady Sabine Marrow |
| Guest 7 | Lord Ambrose Veil |
| Guest 8 | Madame Coralie Thread |
| Butler | Player character / service voice |
| Count and Countess of Chantilly | Addressed in the Chapter 2 opening speech |
| Monster | Long-haired, arm-swinging beast on nine spider legs |

# Chapter 1 - Arrivals

## Chapter 1 Runtime Setup

| Field | Value |
| --- | --- |
| Chapter ID | `chapter_01_arrivals` |
| Title card | `Chapter 1` |
| Starting room | `Grand Entrance Hall` |
| Completion room | `Drawing Room` |
| Clock start | `5:59 PM` |
| First arrival | `6:00 PM` |
| Empty doorbell | `6:04 PM` |
| Required guests | `8` |
| Guests per arrival group | `2` |
| Next request | `chapter_02_pending` |

## Chapter 1 Beat Script

### Beat 1 - Black Screen and Title

Action: reset the chapter clock, clear previous chapter events, disable player input, show black, then show `Chapter 1`.

Action: fade from black into the Grand Entrance Hall. Enable player input. Arm the arrival timeline at 5:59 PM.

### Beat 2 - Doorbell Arrival Schedule

| Time | Event | Guests |
| --- | --- | --- |
| 6:00 PM | Guest group 1 queues outside | Guest 1 Miss Isolde Wren; Guest 2 Professor Lucien Vale |
| 6:01 PM | Guest group 2 queues outside | Guest 3 Mister Florian Knell; Guest 4 Countess Elowen Dusk |
| 6:02 PM | Guest group 3 queues outside | Guest 5 Baron Hector Glass; Guest 6 Lady Sabine Marrow |
| 6:03 PM | Guest group 4 queues outside | Guest 7 Lord Ambrose Veil; Guest 8 Madame Coralie Thread |
| 6:04 PM | Empty doorbell event | No new guests |

Action: when guests are waiting outside, the doorbell rings. If guests wait long enough, the ring escalates from normal to urgent to aggressive. If the Butler answers while several groups are pending, all pending guest groups are admitted in order.

### Beat 3 - Standard Guest Arrival Loop

Action: player clicks the front door. If needed, the Butler walks to the door-answer spot first. The doorbell stops. Pending guests enter the Grand Entrance Hall.

Dialogue: each arriving guest speaks their `ENTRY` line. If delayed, they additionally speak their `DELAYED` line.

Action: each guest offers a coat. The Butler can carry only one coat. The player takes a coat, carries it to the wardrobe/coat closet, and stores it. After the coat is stored, the guest can move to the Drawing Room and become seated/handled.

Action: if the Butler tries to take another coat while already carrying one, play `SUB_CH01_BUTLER_ONE_COAT_001`. If the wardrobe is clicked while empty-handed, play `SUB_CH01_BUTLER_NO_COAT_001`.

### Beat 4 - Drawing Room Ambient Lines

Action: once a guest is seated/handled in the Drawing Room, the first ambient line can be shown. The voice catalog also contains the second ambient line for each guest.

### Beat 5 - 6:04 Empty Doorbell

Action: at 6:04 PM, the empty doorbell event fires. No guest group is attached. The player answers the front door.

Dialogue - Butler (`SUB_CH01_BUTLER_EMPTY_DOOR_001`): "No one is there."

Action: `finalEmptyDoorbellOccurred` becomes true. The chapter completion gate can now pass once all guest coats are stored, all required guests are seated/handled in the Drawing Room, no guest groups are pending outside, and the Butler is not carrying a coat.

### Beat 6 - Chapter 1 Completion

Action: if completion is ready but the Butler is not in the Drawing Room, Chapter 1 waits. Once the Butler enters the Drawing Room, all required guests are staged for Chapter 2, guest coats are hidden/disabled, player input is disabled, and the Chapter Manager fades to black after the Chapter 1 completion delay.

Transition: request `chapter_02_pending`, which the Chapter Manager resolves into `chapter_02_guest_search` and starts Chapter 2.

## Chapter 1 Butler Service Lines

| Line ID | Status | Speaker | Dialogue |
| --- | --- | --- | --- |
| SUB_CH01_BUTLER_WELCOME_001 | Voice/subtitle asset-backed | Butler | "Good evening. Welcome to Chateau Chantilly." |
| SUB_CH01_BUTLER_TAKE_COAT_001 | Voice/subtitle asset-backed | Butler | "May I take your coat?" |
| SUB_CH01_BUTLER_THIS_WAY_001 | Voice/subtitle asset-backed | Butler | "This way, please. The Drawing Room is prepared." |
| SUB_CH01_BUTLER_ONE_COAT_001 | Current gameplay path | Butler | "One coat at a time, if you please." |
| SUB_CH01_BUTLER_NO_COAT_001 | Current gameplay path | Butler | "I have no coat to hang." |
| SUB_CH01_BUTLER_EMPTY_DOOR_001 | Current gameplay path | Butler | "No one is there." |

## Chapter 1 Guest Dialogue

### Guest 1 - Miss Isolde Wren

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH1_G01_ENTRY | Current gameplay path | Door answered / arrival greeting | "Good evening. I trust the house remembers its manners better than the weather does." |
| CH1_G01_DELAYED | Current gameplay path | Delayed outside | "We were beginning to wonder if anyone was home." |
| CH1_G01_COAT_HANDOFF | Voice/subtitle asset-backed | Coat handoff | "Careful with the collar, if you please. It has survived worse evenings than this one." |
| CH1_G01_TO_DRAWING_ROOM | Voice/subtitle asset-backed | Sent toward Drawing Room | "A proper house is judged by its wardrobe first. So far, Chateau Chantilly remains under review." |
| CH1_G01_AMBIENT_01 | Current gameplay path | Drawing Room ambient 1 | "This house is colder than I expected." |
| CH1_G01_AMBIENT_02 | Voice/subtitle asset-backed | Drawing Room ambient 2 | "The fire looks arranged rather than lit." |
| CH1_G01_EMPTY_BELL_REACTION | Voice/subtitle asset-backed | 6:04 empty doorbell reaction | "Then who, precisely, rang?" |

### Guest 2 - Professor Lucien Vale

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH1_G02_ENTRY | Current gameplay path | Door answered / arrival greeting | "Thank you. The drive was longer in the dark than I care to admit." |
| CH1_G02_DELAYED | Current gameplay path | Delayed outside | "It is rather cold out there." |
| CH1_G02_COAT_HANDOFF | Voice/subtitle asset-backed | Coat handoff | "Thank you. The damp seems to cling to everything tonight." |
| CH1_G02_TO_DRAWING_ROOM | Voice/subtitle asset-backed | Sent toward Drawing Room | "The Drawing Room sounds heavenly. I would settle for any room with a pulse of warmth." |
| CH1_G02_AMBIENT_01 | Current gameplay path | Drawing Room ambient 1 | "The host is late, isn't he?" |
| CH1_G02_AMBIENT_02 | Voice/subtitle asset-backed | Drawing Room ambient 2 | "I keep thinking someone is standing just behind the curtains." |
| CH1_G02_EMPTY_BELL_REACTION | Voice/subtitle asset-backed | 6:04 empty doorbell reaction | "Please do not say the wind. The wind has better manners." |

### Guest 3 - Mister Florian Knell

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH1_G03_ENTRY | Current gameplay path | Door answered / arrival greeting | "Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?" |
| CH1_G03_DELAYED | Current gameplay path | Delayed outside | "We have been waiting at the door for some time." |
| CH1_G03_COAT_HANDOFF | Voice/subtitle asset-backed | Coat handoff | "With gratitude. Do hang it somewhere it can be admired." |
| CH1_G03_TO_DRAWING_ROOM | Voice/subtitle asset-backed | Sent toward Drawing Room | "Prepared? How promising. I adore a room that knows guests are coming." |
| CH1_G03_AMBIENT_01 | Current gameplay path | Drawing Room ambient 1 | "Did you hear something upstairs?" |
| CH1_G03_AMBIENT_02 | Voice/subtitle asset-backed | Drawing Room ambient 2 | "If the house is settling, it is doing so with theatrical timing." |
| CH1_G03_EMPTY_BELL_REACTION | Voice/subtitle asset-backed | 6:04 empty doorbell reaction | "A phantom caller? How rude to arrive without a coat." |

### Guest 4 - Countess Elowen Dusk

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH1_G04_ENTRY | Current gameplay path | Door answered / arrival greeting | "Good evening, Butler. The road up here has the cheerful shape of a warning." |
| CH1_G04_DELAYED | Current gameplay path | Delayed outside | "At last. I had begun composing my obituary in the frost." |
| CH1_G04_COAT_HANDOFF | Voice/subtitle asset-backed | Coat handoff | "Take it before it decides to stay here without me." |
| CH1_G04_TO_DRAWING_ROOM | Voice/subtitle asset-backed | Sent toward Drawing Room | "Prepared rooms and prepared excuses often look alike. Lead on." |
| CH1_G04_AMBIENT_01 | Current gameplay path | Drawing Room ambient 1 | "The drawing room should be warmer." |
| CH1_G04_AMBIENT_02 | Voice/subtitle asset-backed | Drawing Room ambient 2 | "Old houses groan. This one seems to choose its words." |
| CH1_G04_EMPTY_BELL_REACTION | Voice/subtitle asset-backed | 6:04 empty doorbell reaction | "Doors do not summon themselves. Not respectable doors, anyway." |

### Guest 5 - Baron Hector Glass

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH1_G05_ENTRY | Current gameplay path | Door answered / arrival greeting | "Good evening. I hope the evening has not started without us." |
| CH1_G05_DELAYED | Current gameplay path | Delayed outside | "We were beginning to wonder if anyone was home." |
| CH1_G05_COAT_HANDOFF | Voice/subtitle asset-backed | Coat handoff | "Of course. There is nothing in the pockets but travel dust and bad omens." |
| CH1_G05_TO_DRAWING_ROOM | Voice/subtitle asset-backed | Sent toward Drawing Room | "Then let us not keep the Drawing Room from its purpose." |
| CH1_G05_AMBIENT_01 | Current gameplay path | Drawing Room ambient 1 | "This house is colder than I expected." |
| CH1_G05_AMBIENT_02 | Voice/subtitle asset-backed | Drawing Room ambient 2 | "The portraits look recently offended." |
| CH1_G05_EMPTY_BELL_REACTION | Voice/subtitle asset-backed | 6:04 empty doorbell reaction | "Let us pretend it was a mistake. Pretending is useful in old houses." |

### Guest 6 - Lady Sabine Marrow

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH1_G06_ENTRY | Current gameplay path | Door answered / arrival greeting | "Thank you. I nearly mistook the bell pull for a funeral cord." |
| CH1_G06_DELAYED | Current gameplay path | Delayed outside | "It is rather cold out there, and colder still when one is expected." |
| CH1_G06_COAT_HANDOFF | Voice/subtitle asset-backed | Coat handoff | "Yes, please. I have been wearing half the road since the lower gate." |
| CH1_G06_TO_DRAWING_ROOM | Voice/subtitle asset-backed | Sent toward Drawing Room | "If the fire is real, I may forgive the road." |
| CH1_G06_AMBIENT_01 | Current gameplay path | Drawing Room ambient 1 | "The host is late, isn't he?" |
| CH1_G06_AMBIENT_02 | Voice/subtitle asset-backed | Drawing Room ambient 2 | "I dislike a clock that seems to be waiting for me personally." |
| CH1_G06_EMPTY_BELL_REACTION | Voice/subtitle asset-backed | 6:04 empty doorbell reaction | "I should very much like that to be the last surprise before dinner." |

### Guest 7 - Lord Ambrose Veil

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH1_G07_ENTRY | Current gameplay path | Door answered / arrival greeting | "Lovely to see you. The chateau looks almost awake tonight." |
| CH1_G07_DELAYED | Current gameplay path | Delayed outside | "We have been waiting at the door for some time. The house was listening with us." |
| CH1_G07_COAT_HANDOFF | Voice/subtitle asset-backed | Coat handoff | "Yes. And if it whispers, do not answer it." |
| CH1_G07_TO_DRAWING_ROOM | Voice/subtitle asset-backed | Sent toward Drawing Room | "Prepared is good. Protected would be better." |
| CH1_G07_AMBIENT_01 | Current gameplay path | Drawing Room ambient 1 | "Did you hear something upstairs?" |
| CH1_G07_AMBIENT_02 | Voice/subtitle asset-backed | Drawing Room ambient 2 | "The ceiling has footsteps in it, and not all of them are human." |
| CH1_G07_EMPTY_BELL_REACTION | Voice/subtitle asset-backed | 6:04 empty doorbell reaction | "It wanted us all in here. That is what I think." |

### Guest 8 - Madame Coralie Thread

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH1_G08_ENTRY | Current gameplay path | Door answered / arrival greeting | "Good evening, Butler. I see the house has chosen its most severe face." |
| CH1_G08_DELAYED | Current gameplay path | Delayed outside | "At last. A closed door should not feel so pleased with itself." |
| CH1_G08_COAT_HANDOFF | Voice/subtitle asset-backed | Coat handoff | "Take it. The night has left fingerprints on the sleeves." |
| CH1_G08_TO_DRAWING_ROOM | Voice/subtitle asset-backed | Sent toward Drawing Room | "Very well. Let us see what sort of welcome the room has rehearsed." |
| CH1_G08_AMBIENT_01 | Current gameplay path | Drawing Room ambient 1 | "The drawing room should be warmer." |
| CH1_G08_AMBIENT_02 | Voice/subtitle asset-backed | Drawing Room ambient 2 | "There is a draft here that does not come from any door." |
| CH1_G08_EMPTY_BELL_REACTION | Voice/subtitle asset-backed | 6:04 empty doorbell reaction | "Then we are here. I hope it is satisfied." |

# Chapter 2 - Guest Search

## Chapter 2 Runtime Setup

| Field | Value |
| --- | --- |
| Chapter ID | `chapter_02_guest_search` |
| Title card | `Chapter 2` |
| Opening room | `Drawing Room` |
| Dinner room | `Dining Room` |
| Chapter clock | `6:05 PM` at start; stopped until dinner transition |
| Dinner clock target | `7:00 PM` |
| Initial phase | `FadeInDrawingRoom` |
| Final request | `chapter_03_dinner_pending` |

## Chapter 2 Beat Script

### Beat 1 - Start Black After Chapter 1

Action: Chapter 2 begins from the black screen created by the Chapter 1 completion transition. The Chapter Manager resolves `chapter_02_pending` into `chapter_02_guest_search`.

### Beat 2 - Fade In On Drawing Room

Action: set the current room to the Drawing Room, move/stage the Butler and guests into Chapter 2 state, set the Chapter 2 clock, initialize the Chapter 2 HUD, and fade in.

UI objective: `Address the guests.`

UI primary action: `Address Guests`

### Beat 3 - Optional Pre-Speech Guest Barks

These eight lines are voice/subtitle asset-backed and belong before the Butler speech if that beat is enabled.

| Line ID | Speaker | Dialogue |
| --- | --- | --- |
| CH2_G01_PRESPEECH_BARK | Miss Isolde Wren | "Do begin, Butler. Formality is all that stands between dinner and nonsense." |
| CH2_G02_PRESPEECH_BARK | Professor Lucien Vale | "Are we all meant to be waiting like this?" |
| CH2_G03_PRESPEECH_BARK | Mister Florian Knell | "This is deliciously awkward. I approve, with reservations." |
| CH2_G04_PRESPEECH_BARK | Countess Elowen Dusk | "It is never a good sign when the servants make speeches." |
| CH2_G05_PRESPEECH_BARK | Baron Hector Glass | "Let him speak. The hour has turned strange." |
| CH2_G06_PRESPEECH_BARK | Lady Sabine Marrow | "I dislike a room that listens back." |
| CH2_G07_PRESPEECH_BARK | Lord Ambrose Veil | "That sound in the walls—did anyone else hear it before the bell?" |
| CH2_G08_PRESPEECH_BARK | Madame Coralie Thread | "Say what you came to say, Butler. The room is holding its breath." |

### Beat 4 - Butler Speech

Dialogue - Butler (`SUB_CH02_BUTLER_ADDRESS_GUESTS_001`): "Welcome, friends and honored guests, to Chateau Chantilly. On behalf of the Count and Countess—"

Action: hold the line long enough for the voice clip, then interrupt it. The objective/status changes to `A terrible sound cuts through the room...`.

### Beat 5 - Monster / Violin Stinger

SFX: the high-pitched violin begins abruptly.

Action: the monster appears from `Ch2_MonsterRunStart`, runs rightward for 1-2 seconds, freezes for 1-2 seconds, and repeats for exactly three run/freeze cycles. The monster uses the `Ch2_Monster` object when present, falls back to a placeholder if configured, and uses `Ch2_MonsterFreezeTarget` / forward run targeting to continue forward rather than reset.

SFX: violin audio stops when the stinger completes or is stopped by room/state cleanup.

### Beat 6 - Guest Panic Lines

These eight spoken panic lines are voice/subtitle asset-backed. The current panic controller also plays panic scream SFX and animates/runs guests toward Drawing Room exits.

| Line ID | Speaker | Dialogue |
| --- | --- | --- |
| CH2_G01_PANIC | Miss Isolde Wren | "Do not run! Do not—oh Lord, run!" |
| CH2_G02_PANIC | Professor Lucien Vale | "It has too many legs!" |
| CH2_G03_PANIC | Mister Florian Knell | "That is not a dog. Someone tell me that is not a dog." |
| CH2_G04_PANIC | Countess Elowen Dusk | "Down! Get down!" |
| CH2_G05_PANIC | Baron Hector Glass | "Away from the windows!" |
| CH2_G06_PANIC | Lady Sabine Marrow | "The violin—make it stop!" |
| CH2_G07_PANIC | Lord Ambrose Veil | "I saw its hair move before it moved!" |
| CH2_G08_PANIC | Madame Coralie Thread | "No one touch it! No one breathe at it!" |

### Beat 7 - Guests Scatter

Action: after the monster stinger, guest panic exits resolve, the guests are placed at `Ch2_Hide_` anchors, made visible/interactable according to room state, and assigned click targets if needed.

UI objective: `Find the guests. Tell them dinner will be served at 7:00 PM sharp.`

### Beat 8 - Main Guest Search Conversation Loop

Current gameplay path: clicking a visible hidden guest starts a conversation locked to that guest. The Butler found line, fixed meal and spirits questions, optional fixed smoking question, and unique guest exit are shown with voice/subtitle playback. If the player leaves the room mid-conversation, the current audio/subtitle is cut; returning to that room resumes after the interrupted line rather than replaying it.

Current implemented conversation order:

1. Butler found announcement and the guest's found reply.
2. The player may ask the remaining question categories in any order; the guest never chooses from a preference UI.
3. Butler meal question, followed by that guest's fixed meal answer.
4. Butler spirits question, followed by that guest's fixed yes/no answer.
5. For Guests 2, 3, 5, and 7 only: Butler smoking question, followed by that guest's fixed answer.
6. `Comfort and send to Dining Room`, followed by that guest's unique exit line.
7. Guest is marked found, hidden for Dining Room transfer, and assigned found/favor order.

Recorded data: fixed meal preference by identity; spirits bottle or `none`; fixed smoking preference for Guests 2, 3, 5, and 7, with `not asked` recorded for Guests 1, 4, 6, and 8.

### Beat 9 - Butler Search Conversation Lines

| Line ID | Status | Applies to | Dialogue |
| --- | --- | --- | --- |
| SUB_CH02_BUTLER_FOUND_G01 | Current gameplay path | Miss Isolde Wren | "I have found you, Miss Isolde Wren. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?" |
| SUB_CH02_BUTLER_FOUND_G02 | Current gameplay path | Professor Lucien Vale | "I have found you, Professor Lucien Vale. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?" |
| SUB_CH02_BUTLER_FOUND_G03 | Current gameplay path | Mister Florian Knell | "I have found you, Mister Florian Knell. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?" |
| SUB_CH02_BUTLER_FOUND_G04 | Current gameplay path | Countess Elowen Dusk | "I have found you, Countess Elowen Dusk. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?" |
| SUB_CH02_BUTLER_FOUND_G05 | Current gameplay path | Baron Hector Glass | "I have found you, Baron Hector Glass. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?" |
| SUB_CH02_BUTLER_FOUND_G06 | Current gameplay path | Lady Sabine Marrow | "I have found you, Lady Sabine Marrow. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?" |
| SUB_CH02_BUTLER_FOUND_G07 | Current gameplay path | Lord Ambrose Veil | "I have found you, Lord Ambrose Veil. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?" |
| SUB_CH02_BUTLER_FOUND_G08 | Current gameplay path | Madame Coralie Thread | "I have found you, Madame Coralie Thread. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?" |
| SUB_CH02_BUTLER_MEAL_ASK_001 | Current gameplay path | All guests | "For supper, shall I put you down for the fresh monte genellion de plink, or thyme with Lillums?" |
| SUB_CH02_BUTLER_SMOKE_ASK_001 | Current gameplay path | Guests 2, 3, 5, and 7 only | "After dinner, shall I prepare a cigar, a pipe, or no smoke at all?" |
| SUB_CH02_BUTLER_SPIRITS_ASK_001 | Current gameplay path | All guests | "And shall I see that your bottle of spirits is waiting at the table?" |

### Beat 10 - Full Authored Guest Response Lines For Search/Preference Flow

The lines below are the complete voice/subtitle-backed guest response set. Use them when expanding the current implemented conversation so guest replies, preference acknowledgements, spirits replies, clock reactions, and Dining Room reveal barks do not drift from the approved audio.

#### Guest 1 - Miss Isolde Wren

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH2_G01_PRESPEECH_BARK | Voice/subtitle asset-backed | Before Butler speech | "Do begin, Butler. Formality is all that stands between dinner and nonsense." |
| CH2_G01_PANIC | Voice/subtitle asset-backed | Monster/violin panic | "Do not run! Do not—oh Lord, run!" |
| CH2_G01_FOUND_START | Voice/subtitle asset-backed | Found in hiding / opening | "Announce yourself before I die of manners." |
| CH2_G01_FOUND_REPLY | Voice/subtitle asset-backed | Reply to Butler taking orders | "You may record whatever prevents further surprises." |
| CH2_G01_MEAL_PLINK | Voice/subtitle asset-backed | Meal choice: fresh monte genellion de plink | "The fresh monte genellion de plink. If one must face horrors, one should do it properly fed." |
| CH2_G01_MEAL_THYME | Voice/subtitle asset-backed | Meal choice: thyme with Lillums | "Thyme with Lillums. It sounds disciplined, and discipline is wanted tonight." |
| CH2_G01_SMOKE_CIGAR | Voice/subtitle asset-backed | Smoke choice: cigar | "A cigar. Something with authority." |
| CH2_G01_SMOKE_PIPE | Voice/subtitle asset-backed | Smoke choice: pipe | "A pipe. Slower nerves make better decisions." |
| CH2_G01_SMOKE_NONE | Voice/subtitle asset-backed | Smoke choice: no smoke | "No smoke. I should like my lungs available for any further screaming." |
| CH2_G01_SPIRITS_REPLY | Voice/subtitle asset-backed | Spirits response | "See that it is not shy." |
| CH2_G01_EXIT_TO_DINING | Current gameplay path | Leaves for Dining Room | "Then I shall proceed to the Dining Room. Perhaps punctuality can restore what panic has misplaced." |
| CH2_G01_CLOCK_REACTION | Voice/subtitle asset-backed | Clock strikes seven / Dining Room objective | "Seven o’clock. At least the clock is still obedient." |
| CH2_G01_DINING_REVEAL | Voice/subtitle asset-backed | Dining Room reveal | "Civilization survives another minute." |

#### Guest 2 - Professor Lucien Vale

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH2_G02_PRESPEECH_BARK | Voice/subtitle asset-backed | Before Butler speech | "Are we all meant to be waiting like this?" |
| CH2_G02_PANIC | Voice/subtitle asset-backed | Monster/violin panic | "It has too many legs!" |
| CH2_G02_FOUND_START | Voice/subtitle asset-backed | Found in hiding / opening | "Please tell me you are real before you come any closer." |
| CH2_G02_FOUND_REPLY | Voice/subtitle asset-backed | Reply to Butler taking orders | "At seven? After that thing? Yes. Yes, ordinary questions may save us." |
| CH2_G02_MEAL_PLINK | Voice/subtitle asset-backed | Meal choice: fresh monte genellion de plink | "The fresh monte genellion de plink. I cannot explain why, but the longer name feels safer." |
| CH2_G02_MEAL_THYME | Voice/subtitle asset-backed | Meal choice: thyme with Lillums | "Thyme with Lillums, please. Something gentle. Something with leaves." |
| CH2_G02_SMOKE_CIGAR | Voice/subtitle asset-backed | Smoke choice: cigar | "A cigar, though I may only hold it for courage." |
| CH2_G02_SMOKE_PIPE | Voice/subtitle asset-backed | Smoke choice: pipe | "A pipe, if it can be made to smell like a normal evening." |
| CH2_G02_SMOKE_NONE | Voice/subtitle asset-backed | Smoke choice: no smoke | "No smoke at all. I have inhaled enough terror for one night." |
| CH2_G02_SPIRITS_REPLY | Voice/subtitle asset-backed | Declines spirits | "No, thank you. I may need every faculty I possess." |
| CH2_G02_EXIT_TO_DINING | Current gameplay path | Leaves for Dining Room | "Right. The Dining Room at seven. A chair and an ordinary meal sound remarkably reassuring." |
| CH2_G02_CLOCK_REACTION | Voice/subtitle asset-backed | Clock strikes seven / Dining Room objective | "Please tell me dinner has windows. No—doors. I meant doors." |
| CH2_G02_DINING_REVEAL | Voice/subtitle asset-backed | Dining Room reveal | "I have never been so grateful for a chair." |

#### Guest 3 - Mister Florian Knell

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH2_G03_PRESPEECH_BARK | Voice/subtitle asset-backed | Before Butler speech | "This is deliciously awkward. I approve, with reservations." |
| CH2_G03_PANIC | Voice/subtitle asset-backed | Monster/violin panic | "That is not a dog. Someone tell me that is not a dog." |
| CH2_G03_FOUND_START | Voice/subtitle asset-backed | Found in hiding / opening | "If this is a party game, I withdraw my admiration." |
| CH2_G03_FOUND_REPLY | Voice/subtitle asset-backed | Reply to Butler taking orders | "Splendid. Nothing steadies the soul like being menued after a monster." |
| CH2_G03_MEAL_PLINK | Voice/subtitle asset-backed | Meal choice: fresh monte genellion de plink | "Fresh monte genellion de plink. It sounds impossible, and I am in an impossible mood." |
| CH2_G03_MEAL_THYME | Voice/subtitle asset-backed | Meal choice: thyme with Lillums | "Thyme with Lillums. Pretty, mysterious, and likely to stain. I accept." |
| CH2_G03_SMOKE_CIGAR | Voice/subtitle asset-backed | Smoke choice: cigar | "A cigar. I intend to look magnificent while recovering." |
| CH2_G03_SMOKE_PIPE | Voice/subtitle asset-backed | Smoke choice: pipe | "A pipe. It gives one the illusion of wisdom." |
| CH2_G03_SMOKE_NONE | Voice/subtitle asset-backed | Smoke choice: no smoke | "No smoke. The monster already supplied quite enough atmosphere." |
| CH2_G03_SPIRITS_REPLY | Voice/subtitle asset-backed | Spirits response | "Make it visible. I may need to toast survival several times." |
| CH2_G03_EXIT_TO_DINING | Current gameplay path | Leaves for Dining Room | "To the Dining Room, then. I shall arrive composed, even if I must rehearse it on the way." |
| CH2_G03_CLOCK_REACTION | Voice/subtitle asset-backed | Clock strikes seven / Dining Room objective | "If anyone asks, I was never frightened. I was arranging my face." |
| CH2_G03_DINING_REVEAL | Voice/subtitle asset-backed | Dining Room reveal | "Look at us. Pale, terrified, and still punctual." |

#### Guest 4 - Countess Elowen Dusk

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH2_G04_PRESPEECH_BARK | Voice/subtitle asset-backed | Before Butler speech | "It is never a good sign when the servants make speeches." |
| CH2_G04_PANIC | Voice/subtitle asset-backed | Monster/violin panic | "Down! Get down!" |
| CH2_G04_FOUND_START | Voice/subtitle asset-backed | Found in hiding / opening | "If you are here to say dinner is canceled, lie more elegantly." |
| CH2_G04_FOUND_REPLY | Voice/subtitle asset-backed | Reply to Butler taking orders | "Good. A schedule is a flimsy shield, but it is a shield." |
| CH2_G04_MEAL_PLINK | Voice/subtitle asset-backed | Meal choice: fresh monte genellion de plink | "Fresh monte genellion de plink. If the name is a trap, I expect you to spring it first." |
| CH2_G04_MEAL_THYME | Voice/subtitle asset-backed | Meal choice: thyme with Lillums | "Thyme with Lillums. Quiet food. Sensible food. Food unlikely to chase me." |
| CH2_G04_SMOKE_CIGAR | Voice/subtitle asset-backed | Smoke choice: cigar | "A cigar. If I am to be hunted by architecture, I shall smell expensive." |
| CH2_G04_SMOKE_PIPE | Voice/subtitle asset-backed | Smoke choice: pipe | "A pipe. It gives the hands something to do besides tremble." |
| CH2_G04_SMOKE_NONE | Voice/subtitle asset-backed | Smoke choice: no smoke | "No smoke. I prefer to see what is coming." |
| CH2_G04_SPIRITS_REPLY | Voice/subtitle asset-backed | Spirits response | "Good. I distrust a dinner table without witnesses." |
| CH2_G04_EXIT_TO_DINING | Current gameplay path | Leaves for Dining Room | "I will be in the Dining Room at seven—assuming the house still permits a civilized schedule." |
| CH2_G04_CLOCK_REACTION | Voice/subtitle asset-backed | Clock strikes seven / Dining Room objective | "The clock sounds pleased with itself. I resent that." |
| CH2_G04_DINING_REVEAL | Voice/subtitle asset-backed | Dining Room reveal | "If the soup screams, I am leaving." |

#### Guest 5 - Baron Hector Glass

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH2_G05_PRESPEECH_BARK | Voice/subtitle asset-backed | Before Butler speech | "Let him speak. The hour has turned strange." |
| CH2_G05_PANIC | Voice/subtitle asset-backed | Monster/violin panic | "Away from the windows!" |
| CH2_G05_FOUND_START | Voice/subtitle asset-backed | Found in hiding / opening | "I was not hiding. I was choosing a defensible position." |
| CH2_G05_FOUND_REPLY | Voice/subtitle asset-backed | Reply to Butler taking orders | "Proceed. The more ordinary the ritual, the less power we give the extraordinary." |
| CH2_G05_MEAL_PLINK | Voice/subtitle asset-backed | Meal choice: fresh monte genellion de plink | "Fresh monte genellion de plink. Something substantial. I dislike fleeing on an empty stomach." |
| CH2_G05_MEAL_THYME | Voice/subtitle asset-backed | Meal choice: thyme with Lillums | "Thyme with Lillums. Light enough to run after, should running remain necessary." |
| CH2_G05_SMOKE_CIGAR | Voice/subtitle asset-backed | Smoke choice: cigar | "A cigar. For victory, or for pretending." |
| CH2_G05_SMOKE_PIPE | Voice/subtitle asset-backed | Smoke choice: pipe | "A pipe. Slow smoke for a slower pulse." |
| CH2_G05_SMOKE_NONE | Voice/subtitle asset-backed | Smoke choice: no smoke | "No smoke. Keep the air clear and the exits clearer." |
| CH2_G05_SPIRITS_REPLY | Voice/subtitle asset-backed | Spirits response | "Place it where I can reach it without turning my back." |
| CH2_G05_EXIT_TO_DINING | Current gameplay path | Leaves for Dining Room | "Understood. I shall take my place in the Dining Room and keep watch on the doors." |
| CH2_G05_CLOCK_REACTION | Voice/subtitle asset-backed | Clock strikes seven / Dining Room objective | "Dining Room, then. Stay together. Walk, do not scatter." |
| CH2_G05_DINING_REVEAL | Voice/subtitle asset-backed | Dining Room reveal | "Sit where you can see the doors." |

#### Guest 6 - Lady Sabine Marrow

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH2_G06_PRESPEECH_BARK | Voice/subtitle asset-backed | Before Butler speech | "I dislike a room that listens back." |
| CH2_G06_PANIC | Voice/subtitle asset-backed | Monster/violin panic | "The violin—make it stop!" |
| CH2_G06_FOUND_START | Voice/subtitle asset-backed | Found in hiding / opening | "Is it gone, or has it merely become quiet?" |
| CH2_G06_FOUND_REPLY | Voice/subtitle asset-backed | Reply to Butler taking orders | "Yes. Please. Ask me anything that has only two answers." |
| CH2_G06_MEAL_PLINK | Voice/subtitle asset-backed | Meal choice: fresh monte genellion de plink | "Fresh monte genellion de plink. I refuse to fear a meal with a comic name." |
| CH2_G06_MEAL_THYME | Voice/subtitle asset-backed | Meal choice: thyme with Lillums | "Thyme with Lillums. That sounds almost medicinal. I accept." |
| CH2_G06_SMOKE_CIGAR | Voice/subtitle asset-backed | Smoke choice: cigar | "A cigar. I may need to prove I still possess hands." |
| CH2_G06_SMOKE_PIPE | Voice/subtitle asset-backed | Smoke choice: pipe | "A pipe. Something domestic against the screaming violin." |
| CH2_G06_SMOKE_NONE | Voice/subtitle asset-backed | Smoke choice: no smoke | "No smoke. The room has already burned itself into my memory." |
| CH2_G06_SPIRITS_REPLY | Voice/subtitle asset-backed | Declines spirits | "Please leave my bottle put away. I need to know whether that violin starts again." |
| CH2_G06_EXIT_TO_DINING | Current gameplay path | Leaves for Dining Room | "Thank you. I will make my way to the Dining Room. Please warn me if anything starts playing again." |
| CH2_G06_CLOCK_REACTION | Voice/subtitle asset-backed | Clock strikes seven / Dining Room objective | "I would like the next room to contain fewer instruments." |
| CH2_G06_DINING_REVEAL | Voice/subtitle asset-backed | Dining Room reveal | "I can hear the violin even when it is not playing." |

#### Guest 7 - Lord Ambrose Veil

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH2_G07_PRESPEECH_BARK | Voice/subtitle asset-backed | Before Butler speech | "That sound in the walls—did anyone else hear it before the bell?" |
| CH2_G07_PANIC | Voice/subtitle asset-backed | Monster/violin panic | "I saw its hair move before it moved!" |
| CH2_G07_FOUND_START | Voice/subtitle asset-backed | Found in hiding / opening | "I knew the house was awake. I did not know it had pets." |
| CH2_G07_FOUND_REPLY | Voice/subtitle asset-backed | Reply to Butler taking orders | "Record quickly. The walls have begun pretending not to listen." |
| CH2_G07_MEAL_PLINK | Voice/subtitle asset-backed | Meal choice: fresh monte genellion de plink | "Fresh monte genellion de plink. It sounds like a spell, and we may need one." |
| CH2_G07_MEAL_THYME | Voice/subtitle asset-backed | Meal choice: thyme with Lillums | "Thyme with Lillums. Green things know how to survive old stone." |
| CH2_G07_SMOKE_CIGAR | Voice/subtitle asset-backed | Smoke choice: cigar | "A cigar. Let the smoke mark where I have been, in case I vanish." |
| CH2_G07_SMOKE_PIPE | Voice/subtitle asset-backed | Smoke choice: pipe | "A pipe. Smoke curls like warnings when the air is honest." |
| CH2_G07_SMOKE_NONE | Voice/subtitle asset-backed | Smoke choice: no smoke | "No smoke. I want to smell it if that thing returns." |
| CH2_G07_SPIRITS_REPLY | Voice/subtitle asset-backed | Spirits response | "Then pour generously. The chateau has had enough of my nerves." |
| CH2_G07_EXIT_TO_DINING | Current gameplay path | Leaves for Dining Room | "I shall meet the others in the Dining Room. Better that none of us make the journey alone." |
| CH2_G07_CLOCK_REACTION | Voice/subtitle asset-backed | Clock strikes seven / Dining Room objective | "The chateau wanted us separated. Remember that." |
| CH2_G07_DINING_REVEAL | Voice/subtitle asset-backed | Dining Room reveal | "The house is quieter now. That worries me more." |

#### Guest 8 - Madame Coralie Thread

| Line ID | Status | Moment | Dialogue |
| --- | --- | --- | --- |
| CH2_G08_PRESPEECH_BARK | Voice/subtitle asset-backed | Before Butler speech | "Say what you came to say, Butler. The room is holding its breath." |
| CH2_G08_PANIC | Voice/subtitle asset-backed | Monster/violin panic | "No one touch it! No one breathe at it!" |
| CH2_G08_FOUND_START | Voice/subtitle asset-backed | Found in hiding / opening | "Speak plainly. Is the room safe, or merely occupied?" |
| CH2_G08_FOUND_REPLY | Voice/subtitle asset-backed | Reply to Butler taking orders | "You may. I admire a household that continues taking orders after an omen." |
| CH2_G08_MEAL_PLINK | Voice/subtitle asset-backed | Meal choice: fresh monte genellion de plink | "Fresh monte genellion de plink. Boldly named food for a cowardly evening." |
| CH2_G08_MEAL_THYME | Voice/subtitle asset-backed | Meal choice: thyme with Lillums | "Thyme with Lillums. Quiet, green, and unlikely to announce itself on nine legs." |
| CH2_G08_SMOKE_CIGAR | Voice/subtitle asset-backed | Smoke choice: cigar | "A cigar. I intend to leave evidence that I remained composed." |
| CH2_G08_SMOKE_PIPE | Voice/subtitle asset-backed | Smoke choice: pipe | "A pipe. The old rituals have teeth; let us use them." |
| CH2_G08_SMOKE_NONE | Voice/subtitle asset-backed | Smoke choice: no smoke | "No smoke. I want nothing between myself and the door." |
| CH2_G08_SPIRITS_REPLY | Voice/subtitle asset-backed | Declines spirits | "No spirits tonight. I intend to remain the most trustworthy guest at the table." |
| CH2_G08_EXIT_TO_DINING | Current gameplay path | Leaves for Dining Room | "Then the Dining Room it is. I intend to arrive before the house invents another interruption." |
| CH2_G08_CLOCK_REACTION | Voice/subtitle asset-backed | Clock strikes seven / Dining Room objective | "Then let us disappoint it by arriving intact." |
| CH2_G08_DINING_REVEAL | Voice/subtitle asset-backed | Dining Room reveal | "Serve quickly, Butler. The night is not finished with us." |

### Beat 11 - All Guests Found / Clock Strikes Seven

Trigger: all eight guests are marked found.

Action: disable player input, clear conversation UI, prepare guests for Dining Room transfer, stop the clock, set the clock to 7:00 PM, and show the clock strike close-up for 2.25 seconds.

UI: clear objective/status during the clock close-up.

SFX: play the clock strike audio if a clip/source is available.

### Beat 12 - Go To Dining Room

Action: after the clock close-up, phase becomes `DiningRoomObjective`, player input returns, and the HUD objective becomes:

`The clock strikes 7:00. Go to the Dining Room.`

Trigger: when the current room is `Dining Room`, start the Dining Room completion routine.

### Beat 13 - Dining Room Reveal

Action: phase becomes `DiningRoomReveal`. Seat guests in found/favor order at `Ch2_DiningSeat_01` through `Ch2_DiningSeat_08`. Clear status and primary action.

UI objective: `Dinner is served.`

Hold: `diningRoomRevealSeconds` defaults to 5 seconds.

# Beginning of Chapter 3

## Normal Handoff

After the Dining Room reveal hold, Chapter 2 sets phase `Complete` and calls:

`chapterManager.CompleteChapterAndTriggerNextChapter("chapter_03_dinner_pending")`

Current implementation status: `chapter_03_dinner_pending` is a requested/pending chapter ID only. There is no Chapter 3 controller yet. The normal completion routine fades to black, stops the clock, logs the next chapter request, and does not currently start a Chapter 3 gameplay controller.

## Debug Skip To Chapter 3

The debug skip path explicitly sets:

| Field | Value |
| --- | --- |
| Current chapter ID | `chapter_03_dinner_pending` |
| Displayed title | `Chapter 3` |
| Chapter phase | `Complete` |
| Room | `Dining Room` |
| Clock | `7:00 PM`, stopped |
| Objective | `Dinner is served.` |
| Guest state | All guests found, default preferences filled, seated in Dining Room order |

# Current Gameplay Subtitle Aliases

These aliases matter while debugging because gameplay often calls a `SUB_...` subtitle ID, and `GuestVoiceLinePlayback` resolves that to the imported direct voice line ID.

| Gameplay subtitle ID | Resolves to voice line ID | Moment |
| --- | --- | --- |
| SUB_CH01_G01_GREETING_001 | CH1_G01_ENTRY | Chapter 1 guest arrival greeting |
| SUB_CH01_G01_ANNOYED_001 | CH1_G01_DELAYED | Chapter 1 delayed guest line |
| SUB_CH01_G01_AMBIENT_001 | CH1_G01_AMBIENT_01 by default; can resolve to AMBIENT_02 by exact text | Chapter 1 Drawing Room ambient |
| SUB_CH02_G01_FINAL_ACK_001 | CH2_G01_EXIT_TO_DINING | Chapter 2 guest final acknowledgement |
| SUB_CH01_G02_GREETING_001 | CH1_G02_ENTRY | Chapter 1 guest arrival greeting |
| SUB_CH01_G02_ANNOYED_001 | CH1_G02_DELAYED | Chapter 1 delayed guest line |
| SUB_CH01_G02_AMBIENT_001 | CH1_G02_AMBIENT_01 by default; can resolve to AMBIENT_02 by exact text | Chapter 1 Drawing Room ambient |
| SUB_CH02_G02_FINAL_ACK_001 | CH2_G02_EXIT_TO_DINING | Chapter 2 guest final acknowledgement |
| SUB_CH01_G03_GREETING_001 | CH1_G03_ENTRY | Chapter 1 guest arrival greeting |
| SUB_CH01_G03_ANNOYED_001 | CH1_G03_DELAYED | Chapter 1 delayed guest line |
| SUB_CH01_G03_AMBIENT_001 | CH1_G03_AMBIENT_01 by default; can resolve to AMBIENT_02 by exact text | Chapter 1 Drawing Room ambient |
| SUB_CH02_G03_FINAL_ACK_001 | CH2_G03_EXIT_TO_DINING | Chapter 2 guest final acknowledgement |
| SUB_CH01_G04_GREETING_001 | CH1_G04_ENTRY | Chapter 1 guest arrival greeting |
| SUB_CH01_G04_ANNOYED_001 | CH1_G04_DELAYED | Chapter 1 delayed guest line |
| SUB_CH01_G04_AMBIENT_001 | CH1_G04_AMBIENT_01 by default; can resolve to AMBIENT_02 by exact text | Chapter 1 Drawing Room ambient |
| SUB_CH02_G04_FINAL_ACK_001 | CH2_G04_EXIT_TO_DINING | Chapter 2 guest final acknowledgement |
| SUB_CH01_G05_GREETING_001 | CH1_G05_ENTRY | Chapter 1 guest arrival greeting |
| SUB_CH01_G05_ANNOYED_001 | CH1_G05_DELAYED | Chapter 1 delayed guest line |
| SUB_CH01_G05_AMBIENT_001 | CH1_G05_AMBIENT_01 by default; can resolve to AMBIENT_02 by exact text | Chapter 1 Drawing Room ambient |
| SUB_CH02_G05_FINAL_ACK_001 | CH2_G05_EXIT_TO_DINING | Chapter 2 guest final acknowledgement |
| SUB_CH01_G06_GREETING_001 | CH1_G06_ENTRY | Chapter 1 guest arrival greeting |
| SUB_CH01_G06_ANNOYED_001 | CH1_G06_DELAYED | Chapter 1 delayed guest line |
| SUB_CH01_G06_AMBIENT_001 | CH1_G06_AMBIENT_01 by default; can resolve to AMBIENT_02 by exact text | Chapter 1 Drawing Room ambient |
| SUB_CH02_G06_FINAL_ACK_001 | CH2_G06_EXIT_TO_DINING | Chapter 2 guest final acknowledgement |
| SUB_CH01_G07_GREETING_001 | CH1_G07_ENTRY | Chapter 1 guest arrival greeting |
| SUB_CH01_G07_ANNOYED_001 | CH1_G07_DELAYED | Chapter 1 delayed guest line |
| SUB_CH01_G07_AMBIENT_001 | CH1_G07_AMBIENT_01 by default; can resolve to AMBIENT_02 by exact text | Chapter 1 Drawing Room ambient |
| SUB_CH02_G07_FINAL_ACK_001 | CH2_G07_EXIT_TO_DINING | Chapter 2 guest final acknowledgement |
| SUB_CH01_G08_GREETING_001 | CH1_G08_ENTRY | Chapter 1 guest arrival greeting |
| SUB_CH01_G08_ANNOYED_001 | CH1_G08_DELAYED | Chapter 1 delayed guest line |
| SUB_CH01_G08_AMBIENT_001 | CH1_G08_AMBIENT_01 by default; can resolve to AMBIENT_02 by exact text | Chapter 1 Drawing Room ambient |
| SUB_CH02_G08_FINAL_ACK_001 | CH2_G08_EXIT_TO_DINING | Chapter 2 guest final acknowledgement |


# Testing / Debug Checklist

- Chapter 1 starts at 5:59 PM in the Grand Entrance Hall and shows `Chapter 1`.
- Guest groups arrive at 6:00, 6:01, 6:02, and 6:03 PM, two guests per group.
- Each arriving guest uses the exact `CH1_Gxx_ENTRY` line listed here.
- Delayed guests use the exact `CH1_Gxx_DELAYED` line listed here.
- No guest can complete Chapter 1 until their coat is stored and they are seated/handled in the Drawing Room.
- The 6:04 doorbell has no guest and plays Butler line `SUB_CH01_BUTLER_EMPTY_DOOR_001`.
- Chapter 1 completes only after the empty doorbell, all eight guests are handled, the Butler is empty-handed, and the Butler enters the Drawing Room.
- Chapter 2 starts in the Drawing Room with objective `Address the guests.` and action `Address Guests`.
- Butler speech line `SUB_CH02_BUTLER_ADDRESS_GUESTS_001` is interrupted by the monster/violin stinger.
- Guest search objective is exactly `Find the guests. Tell them dinner will be served at 7:00 PM sharp.`
- Every found guest uses the guest-specific `SUB_CH02_BUTLER_FOUND_Gxx` line; no `[Guest Name]` placeholder should appear.
- Meal answers are fixed by guest identity: odd-numbered guests receive `fresh monte genellion de plink`; even-numbered guests receive `thyme with Lillums`.
- Guests 2, 6, and 8 decline spirits; the remaining guests request their bottle.
- Smoking is asked only of Guests 2, 3, 5, and 7, with fixed answers of cigar, no smoke, cigar, and pipe respectively.
- After all guests are found, the clock is set to 7:00 PM, the objective becomes `The clock strikes 7:00. Go to the Dining Room.`, then `Dinner is served.` in the Dining Room reveal.
- Chapter 2 normal completion requests `chapter_03_dinner_pending`; Chapter 3 gameplay is not implemented yet.
- Any audible speech must have an on-screen subtitle with the same text at that moment.
