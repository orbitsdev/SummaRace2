# SummaRace — Audio Audit

**Date:** 2026-08-06 · **Branch:** `experiment/endless-override-2` · **Build:** pre-device, editor only

Reviewed as a ship check, not a code read: every interactive moment in the loop
Boot → NameEntry → MainMenu → SessionMap → StorySelect → Reader → Race → Arrange → Summary →
Results → TeacherMenu, against three questions a 9-year-old on a **muted classroom tablet**
would ask — *did my tap do anything?*, *did I get it right?*, *is this thing broken?*

Method: every `PlaySfx` / `PlayMusic` / `PlayNarration` call site in `Assets/_Game/Scripts`,
cross-referenced against every `onClick` listener and every early-`return` on the tap paths;
all 21 `AudioKeys` constants re-derived against the files on disk (not trusted from a previous
pass); clip provenance traced by MD5 back to the raw Kenney packs; the Trash Dash music state
machine traced across a whole session (three races), which is where the largest open defect is.

---

## 1. Coverage — every interactive moment

`press` = `sfx_press` from `UI.ButtonSquash` on pointer-**down** (fires on every object carrying
that component, which is every button in the kit skin). The controller's own sound fires on
pointer-**up**, so a normal button tap is deliberately two layered sounds.

| Screen | Moment | Sound | Key | Visual partner |
|---|---|---|---|---|
| Boot | splash appears | — | — | logo pop-in + progress bar |
| Boot | → NameEntry / MainMenu | — (tip-less fade, chime suppressed on purpose) | — | fade |
| NameEntry | screen opens | **music (added)** | `MusicMenu` | — |
| NameEntry | pick avatar | click | `SfxClick` | selected avatar goes white, others grey |
| NameEntry | LET'S GO | press + click | `SfxPress`+`SfxClick` | scene change |
| MainMenu | screen opens | music (loop) | `MusicMenu` | — |
| MainMenu | TAP TO START | press + click + chime | `SfxClick`+`SfxTransition` | squash + loading overlay |
| MainMenu | teacher corner | press + click + chime | `SfxClick`+`SfxTransition` | squash + overlay |
| SessionMap | screen opens | music (re-asserted, no-ops if running) | `MusicMenu` | — |
| SessionMap | open stop | press + click + chime | `SfxClick` | squash + overlay |
| SessionMap | **locked** stop | press + nudge | `SfxSlotWiggle` | lock punches + hint line re-asserted |
| SessionMap | session-complete cheer | star + coin per star | `SfxStar`,`SfxCoin` | stop punch, stars light, cheer line |
| SessionMap | BACK | press + click + chime | `SfxClick` | squash + overlay |
| StorySelect | screen opens | music (re-asserted) | `MusicMenu` | — |
| StorySelect | playable card | press + click | `SfxClick` | squash + overlay |
| StorySelect | **locked** card | press + nudge | `SfxSlotWiggle` | lock (or title) punches |
| StorySelect | BACK | press + click | `SfxClick` | squash + overlay |
| Reader | scene opens | **music STOPS** (narration owns the scene) | — | — |
| Reader | page shown | narration auto-plays | (page's own clip) | page text + progress bar sweep |
| Reader | page 2..5 arrives | page turn | `SfxPageTurn` | text swap + bar sweep |
| Reader | NEXT | press + click (+ page turn) | `SfxClick` | squash, question or next page |
| Reader | VOICE ON/OFF | press + click | `SfxClick` | label flips, button greys |
| Reader | **HEAR AGAIN** | press + click + narration | `SfxClick` | **label punch (added)** |
| Reader | answer correct | press + chime | `SfxCorrect` | correct pill greens + punch, praise line pops |
| Reader | answer wrong | press + soft down | `SfxNotQuite` | correct pill still greens + punch, warm-orange line |
| Reader | BACK (arm) | press + nudge | `SfxSlotWiggle` | label → confirm, pill navy→gold |
| Reader | BACK (confirm) | press + click | `SfxClick` | overlay |
| Race | briefing START | press + click | `SfxClick` | briefing hides |
| Race | 3 · 2 · 1 | pop each | `SfxPop` | giant gold numeral |
| Race | GO! | rising boost | `SfxBoost` | giant GO! + camera swoop |
| Race | correct card | collect + boost | `SfxCollect`+`SfxBoost` | sparkle, word flies to tracker slot, praise pill, jump |
| Race | wrong card | soft down | `SfxNotQuite` | feedback pill, danger vignette, cop surges into frame |
| Race | pause / resume / leave | click | `SfxClick` | pause card, armed LEAVE chip |
| Race | FINISH | star, **their music stops** | `SfxStar` | FINISH banner, 2.2 s victory beat |
| Arrange | pick a piece | press + click | `SfxClick` | piece turns blue |
| Arrange | place in slot | press + click | `SfxClick` | slot fills, piece leaves pool |
| Arrange | tap **filled** slot | press + click | `SfxClick` | piece returns to pool |
| Arrange | tap **locked** slot | press + lock | `SfxSlotLock` | label punch |
| Arrange | tap empty slot, **nothing held** | press + click | `SfxClick` | **status line + label punch (added)** |
| Arrange | UNDO with nothing to undo | press + click | `SfxClick` | **status line (added)** |
| Arrange | VERIFY, board not full | press + click | `SfxClick` | status "Put all 5 parts in first!" |
| Arrange | slot verified right | lock | `SfxSlotLock` | slot goes green |
| Arrange | slot verified wrong | nudge | `SfxSlotWiggle` | slot goes amber, piece returns |
| Arrange | all five right | chime | `SfxCorrect` | praise line |
| Arrange | assist finishes it | lock ×n + chime | `SfxSlotLock`,`SfxCorrect` | slots fill one by one, status line |
| Summary | typing | — | — | caret / text |
| Summary | **DONE TYPING** | press + **click (added)** | `SfxClick` | keyboard closes, chip hides, hint widens |
| Summary | SUBMIT nudged | press + click + soft down | `SfxClick`+`SfxNotQuite` | nudge line |
| Summary | SUBMIT accepted | press + click + chime | `SfxClick`+`SfxCorrect` | overlay |
| Results | star lights (×1-3) | star | `SfxStar` | star colours + punch |
| Results | treasure gem (×5) | coin tick | `SfxCoin` | gem pops in, dim = missed |
| Results | praise + main idea | **victory sting (1.3 s, then silence)** | `MusicVictory` | praise line, main-idea card |
| Results | continue | press + click + chime | `SfxClick` | overlay |
| TeacherMenu | any button | press + click | `SfxClick` | squash |
| TeacherMenu | **PIN accepted** | **chime (added)** | `SfxCorrect` | gate hides, actions appear |
| TeacherMenu | wrong PIN / mismatch / cooldown / too short | **nudge (added)** | `SfxSlotWiggle` | status line |
| TeacherMenu | unlock next session | star | `SfxStar` | status names the session |
| TeacherMenu | export succeeded | **star (added)** | `SfxStar` | status shows the full path |
| TeacherMenu | nothing to export | **nudge (added)** | `SfxSlotWiggle` | status line |
| TeacherMenu | arm delete / arm reset / last chance | **nudge (added)** | `SfxSlotWiggle` | label → confirm, warning text |
| any | scene change (with overlay) | chime | `SfxTransition` | loading overlay + progress bar |

**No silent interactions remain** on any tap path in the eleven screens. Nine were found; all
nine are fixed (marked *added* above). Two more had been closed earlier this session (locked
Arrange slot, locked StorySelect card) and are verified still correct.

---

## 2. Defects, ranked

### D1 — Races 2 and 3 of every session have NO MUSIC ⚠️ open, not mine to fix
`Features/Race/*` is owned by another agent this pass; this is the handover.

Trash Dash's `MusicPlayer` is a `DontDestroyOnLoad` **static singleton**, and the director stops
its `AudioSource`s in two places so the music does not follow the learner out
(`EndlessRaceDirector.FinishRoutine` ~line 1196, `LeaveRace` ~line 1699). Nothing ever starts
them again:

* `MusicPlayer.RestartAllStems()` runs from `MusicPlayer.Start()` — **once per app launch**, not
  per scene load; the second scene copy destroys itself in `Awake` (`s_Instance != null`).
* Their only other restart is `GameState.Enter` (`Assets/Scripts/GameManager/GameState.cs:98`),
  guarded by `if (MusicPlayer.instance.GetStem(0) != gameTheme)`. After race 1 stem 0 **is**
  `gameTheme`, so the guard is false and the restart is skipped for the rest of the app's life.

Net: the first race of a launch has music; every race after it is silent, while
`TrackManager.Update` keeps calling `UpdateVolumes()` on stopped sources so nothing looks wrong
in the console. A session is three stories — the learner hears race music once out of three.

**Fix (one line, in our code, their scripts untouched):** in the director's run-release path
(beside `SilenceOurMenuMusic()` / after the countdown), call
`CoroutineHandler.StartStaticCoroutine(MusicPlayer.instance.RestartAllStems())` guarded on
`MusicPlayer.instance != null`. Prefer that over nulling stem 0, which would make their
`GameState.Enter` guard fire and re-enter their state machine's assumptions.

### D2 — `AppSettings.narrationVolume` was never applied ✅ fixed
`AudioManager.PlayNarration` hard-coded `_voiceSource.volume = 1f`. The field was declared,
persisted to `settings.json` and read by nobody, so the one channel the study's accessibility
support depends on was the one channel with no level control. Now stored in `SetVolumes` and
applied both to new narration and to a page already speaking.

### D3 — HEAR AGAIN was pure audio ✅ fixed
The replay chip's entire result is a sound. On a muted tablet — most classroom tablets — it
answered a tap with nothing on screen at all, which reads as a broken button on the one control
that exists to help a struggling reader. Now punches its label (label, not the chip root: the
root carries `ButtonSquash`, and two tweens on one transform fight — the same rule Arrange and
StorySelect already follow).

### D4 — Every teacher-screen rejection sounded exactly like success ✅ fixed
`Submit()` clicks *before* it knows the answer, so a wrong PIN, a mismatched setup pair, a
cooldown, an empty export and a successful unlock all produced one identical cheerful click plus
one line of small status text. "Exported" and "there was nothing to export" are one line apart
and only one of them means the study data is on the tablet. Rejections now carry the warm nudge
(`sfx_slot_wiggle`, never a harsh buzzer — the adult screen gets the same restraint as the
learner's), a successful PIN gets `sfx_correct`, a successful export gets `sfx_star`, and arming
the wipe or the tablet reset sounds like a warning rather than like progress.

### D5 — Three silent no-op taps ✅ fixed
* **Arrange**, empty slot tapped with no piece in hand: click, then nothing. Now re-states the
  instruction line the learner arrived on and punches the slot label.
* **Arrange**, UNDO with an empty stack (or everything locked): click, then nothing. Same line.
* **Summary**, DONE TYPING: no sound of its own at all (only the generic press). Now clicks.

Each is the same failure the locked Arrange slot had before it was fixed: on the screens the
story cannot leave until it is finished, a tap that seems to do nothing is exactly where a
9-year-old decides the game is broken.

### D6 — NameEntry, the first screen on a fresh device, was silent ✅ fixed
Every other menu screen starts `music_menu`; NameEntry did not, so on a fresh tablet the app
opened into silence and stayed there until the Main Menu. Now starts the loop (`PlayMusic`
no-ops when it is already running, so arriving from the teacher's "+ New learner" costs nothing).

### D7 — `sfx_coin` and `sfx_collect` are byte-identical ⬜ open, needs an owner decision
Both are `kenney_rpg-audio/handleCoins.ogg`, MD5 `6718a29b…`, shipped twice. So "you picked the
right SWBST card" (the study's actual measure) and "a gem popped in on the results board" make
exactly the same sound. Not urgent on this branch — coin lines are suppressed in the endless
race — but it costs the correct-answer sound its distinctiveness on Results and the session map.
Fix is one file copy, no code: `kenney_rpg-audio/Audio/handleCoins2.ogg` → `sfx_coin.ogg`.
Deliberately **not** done here: it silently changes a playtested sound, and audio import settings
are being reworked by another pass this session (a new file lands on Unity defaults).

### D8 — `music_menu.mp3` is a 320 kbps stereo MP3 set to DecompressOnLoad ⬜ open, other owner
2 m 41 s, 6.3 MB on disk, **≈27 MB of resident PCM**, and `AudioManager._cache` holds a hard
reference for the app's lifetime once the Main Menu has been seen, so it never unloads. It is
also the only **looping** asset in MP3 format — MP3 carries encoder delay/padding, so the loop
point is audibly gapped on every repeat where an Ogg would be clean. Both are import/asset
issues, and `Assets/_Game/Editor/DeviceBudgetTools.cs` owns them this session (156 MB of resident
PCM is already a known finding); recorded here so the loop-gap reason is not lost — it wants a
re-encode to Ogg, not only a load-type change.

### D9 — Two AudioListeners coexist for the first ~0.5 s of a race ⬜ open, not mine
`EndlessRaceDirector.EnsureSingleAudioListener()` runs after a `yield return new
WaitForSeconds(0.5f)`, so `MainSummaRace`'s own camera listener and the persistent `[Core]` one
are both live until then (Unity logs a warning and the mix is undefined). Masked in practice by
the briefing scrim and their ~1.25 s boot. Worth moving to the director's `Awake`. Verified: the
disable-not-destroy approach and "keep the DontDestroyOnLoad one" choice are both correct, and
**none of the eleven `_Game` scenes contains an `AudioListener`** (grepped) — only the Trash Dash
scene does, so this is the only duplication path in the project.

### D10 — Race SFX and race music are on two different volume systems ⬜ open, design note
In the race our Kenney SFX go through `AudioManager` (no mixer group, `sfxVolume` from
`AppSettings`) while the music is Trash Dash's `MusicPlayer` → their `AudioMixer` at
`maxVolume 0.1`, with levels read from **their** `PlayerData` save, not ours. So
`AppSettings.musicVolume` does nothing during the race, and our SFX sit much higher above the bed
there than they do in the menus. Not a bug the learner can hit, but the mix cannot be balanced
from one place until the race music is routed through `AudioManager` (or our SFX through their
mixer). Worth one listening pass on the device before the study.

---

## 3. The mute case — anything that is audio-only

The defect class that matters most here: a classroom tablet is often muted or at 10% volume, so
any feedback that exists only as a sound does not exist.

**Clean.** After D3, every sound in the game has a visual partner: press → squash, correct →
green + punch + praise line, wrong → coloured feedback line (+ vignette and chaser in the race),
locked → punch + hint line, lock → colour change, star → colour + punch, gem → pop-in, coin →
fly-up, transition → overlay, countdown → giant numeral, finish → banner, teacher actions →
status line.

Three things are audio-only **by nature** and are correctly not treated as feedback:

* **Narration** — its visual partner is the page text it reads, which is on screen throughout.
* **Music** — ambience, carries no information.
* **Race footsteps** (legacy scene only) — texture, not signal.

One thing deliberately left as-is: **`sfx_slot_lock` is used both for "this slot just locked in
correctly" and for "that slot is already done, stop tapping it"**. It reads as a success sound in
a nudge's place, which is the right call under GDD D7 (a tap on finished work must not sound like
a mistake) and the punch carries the actual meaning. Recorded so it is not re-flagged later.

---

## 4. Music state machine, traced end to end

| Scene | On entry | On exit | Survives the load? |
|---|---|---|---|
| Boot | nothing | — | `AudioManager` is `DontDestroyOnLoad` |
| NameEntry | `PlayMusic(MusicMenu)` **(added)** | — | yes, keeps playing |
| MainMenu | `PlayMusic(MusicMenu)` | — | yes |
| SessionMap | `PlayMusic(MusicMenu)` (no-op if running) | — | yes |
| StorySelect | `PlayMusic(MusicMenu)` (no-op if running) | — | yes |
| Reader | **`StopMusic()`** — nothing under the voice | — | silent by design |
| Race | their `MusicPlayer` (ours already stopped, re-asserted by `SilenceOurMenuMusic`) | `FinishRoutine`/`LeaveRace` stop their sources | see **D1** |
| Arrange | nothing | — | **silent** |
| Summary | nothing | — | **silent** |
| Results | `MusicVictory` (1.32 s, `loop:false`) | — | silent after the sting |
| TeacherMenu | nothing (inherits whatever is playing) | — | menu loop continues |

**No scene plays two tracks at once** and **nothing competes with the narration** — verified:
narration only ever plays in the Reader, and the Reader is the one scene that stops the music
outright. `PlayMusic` also no-ops when the same clip is already running, so re-entering a menu
never restarts or layers the loop. Their `MusicPlayer` is stopped before Arrange, so the race
music cannot bleed into the back half.

**The back half of the loop is 90+ seconds of near-total silence** — Arrange, Summary, and
Results after its 1.3 s sting. This is a deliberate-looking gap (they are the "thinking" screens)
but it is also exactly what a muted-tablet learner cannot distinguish from broken sound, and it
is the longest silence in the app. **Owner design call, not fixed here.** If wanted, it is one
line per scene — `AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu)` in
`ArrangeController.Start` / `SummaryController.Start` — with the caveat that `music_menu` is a
bright menu loop, not a work-under bed, and D8 applies to it. A quieter bed would need a new
asset (see §6).

---

## 5. Volumes and mix

* `AppSettings.musicVolume` (0.8) — applied, to `AudioManager`'s music source only (not the race,
  D10).
* `AppSettings.sfxVolume` (1.0) — applied as the `PlayOneShot` volume scale.
* `AppSettings.narrationVolume` (1.0) — **was dead, now applied** (D2).
* `AppSettings.haptics` — no reader anywhere in the project. Not audio; noted so it is not
  mistaken for a wired setting.
* There is **no in-game volume UI** (the Settings scene was cut). Volumes come from
  `settings.json` at boot and never change afterwards, so the device volume keys are the only
  control a teacher has. That is defensible for a study instrument; it does mean the *relative*
  mix has to be right on the file, because nothing can correct it at runtime.
* Levels as authored: narration 1.0 over music 0.8 in the menus; in the Reader the music is
  stopped outright, so the voice is never fighting anything. SFX at 1.0 are the loudest thing in
  the game — 0.1-0.9 s Kenney one-shots, so this is peaky rather than sustained, but on a tablet
  speaker `sfx_correct`/`sfx_star` will sit clearly above the narration. Worth one device listen;
  if it is harsh, `sfxVolume ≈ 0.8` in `settings.json` fixes it with no code change.

---

## 6. Assets

**All 21 `AudioKeys` constants resolve — re-derived, not trusted.** 21 constants, 21 files in
`Assets/_Game/Resources/Audio`, exact 1:1, no orphan files and no missing clips.

| Key | File | s | ch | Kenney source (MD5-traced) |
|---|---|---|---|---|
| `SfxClick` | `sfx_click.mp3` | 0.4 | 2 | (not from the Kenney packs) |
| `SfxPress` | `sfx_press.ogg` | 0.29 | 2 | interface `drop_004` |
| `SfxPop` | `sfx_pop.ogg` | 0.10 | 2 | interface `pluck_001` |
| `SfxTransition` | `sfx_transition.ogg` | 0.55 | 1 | digital `highUp` |
| `SfxCorrect` | `sfx_correct.ogg` | 0.54 | 1 | interface `confirmation_002` |
| `SfxNotQuite` | `sfx_not_quite.ogg` | 0.78 | 1 | digital `lowDown` |
| `SfxPageTurn` | `sfx_page_turn.ogg` | 0.43 | 2 | rpg `bookFlip2` |
| `SfxStar` | `sfx_star.ogg` | 0.13 | 1 | interface `glass_002` |
| `SfxCollect` | `sfx_collect.ogg` | 0.85 | 2 | rpg `handleCoins` |
| `SfxCoin` | `sfx_coin.ogg` | 0.85 | 2 | rpg `handleCoins` — **identical to above (D7)** |
| `SfxBoost` | `sfx_boost.ogg` | 0.47 | 1 | digital `powerUp2` |
| `SfxWhoosh` | `sfx_whoosh.ogg` | 0.42 | 2 | rpg `cloth2` |
| `SfxCaught` | `sfx_caught.ogg` | 0.12 | 2 | impact `impactSoft_medium_000` |
| `SfxFootstepA/B/C` | `sfx_footstep_a/b/c.ogg` | ~0.7 | 2 | impact `footstep_grass_000/001/002` |
| `SfxSlotLock` | `sfx_slot_lock.ogg` | 0.26 | 2 | rpg `metalLatch` |
| `SfxSlotWiggle` | `sfx_slot_wiggle.ogg` | 0.55 | 1 | digital `lowRandom` |
| `MusicMenu` | `music_menu.mp3` | 161 | 2 | (not Kenney) — **D8** |
| `MusicRace` | `music_race.ogg` | 240 | 1 | = `Assets/Sounds/Stems/STEMSMainTrackMono.ogg`, byte-identical, both ship |
| `MusicVictory` | `music_victory.ogg` | 1.32 | 2 | jingles `Pizzicato/jingles_PIZZI07` |

**Dead on the shipping path — six keys, one asset, all for the same reason.** `SfxWhoosh`,
`SfxCaught`, `SfxFootstepA/B/C` and `MusicRace` have call sites **only** in the legacy
`Features/Race/RaceController.cs` / `PlayerRunner.cs` / `CoinPickup.cs`, i.e. only reachable
through `Race.unity` — build index 6 with nothing routing to it (already on the finalization
list). Dropping that scene also retires these six keys and the 6.9 MB duplicate `music_race.ogg`.
Do **not** delete the keys before the scene: they are live code today.

`SfxCaught` additionally can never fire by design — the patrol never catches (GDD D7,
`timesCaught` is hard-wired to 0). It is a 0.12 s soft impact kept as a safety net.

`SfxPop` is alive only through the two race countdowns: `UI.PanelIntro` has
`playSound = false` and nothing sets it true (the owner removed the blanket panel pop).

### Shopping list — sounds a moment wants that do not exist

Nothing invented; sources checked against the raw packs in `Assets/Audio/Kenney/` first.

| Want | Constant | File to create | Sounds like | Source |
|---|---|---|---|---|
| Teacher opens a session | `SfxUnlock` | `sfx_unlock.ogg` | short rising two-note "opened" | **already on disk**: `kenney_interface-sounds/Audio/confirmation_001.ogg` (or `maximize_004`). Currently substituted by `SfxStar` **on purpose** — this is a nicety, not a defect. |
| Session (whole day) complete | `MusicSessionComplete` | `music_session_complete.ogg` | 2-3 s fanfare, bigger than the per-story sting, same instrument family | **already on disk**: `kenney_music-jingles/Audio/Pizzicato jingles/` — pick a longer sibling of `jingles_PIZZI07`, which is `music_victory`, so the day-cheer and the story-cheer stay one voice. Today the map's celebration reuses `SfxStar` + `SfxCoin`. |
| Calm bed for Arrange / Summary | `MusicThink` | `music_think.ogg` | 60-90 s seamless loop, sparse, no melody to sing along to, ~-12 dB under `music_menu` | **not in Kenney** — the packs hold jingles (≤3 s), not loops. Same gap the race music had before F24. Sourcing needed, or reuse `music_menu` at reduced volume (see §4). |
| Distinct coin tick | (reuse `SfxCoin`) | replace `sfx_coin.ogg` | lighter than the collect sound | **already on disk**: `kenney_rpg-audio/Audio/handleCoins2.ogg` — see D7. |

Everything else the loop asks for already exists.

---

## 7. What changed in this pass

| File | Change |
|---|---|
| `Core/AudioManager.cs` | apply `narrationVolume` (D2); honour a changed `loop` flag when the same track is already playing; cache clip **misses** as well as hits (a missing key was a `Resources.Load` + console line per call — the race asks for footsteps several times a second); null-guard `SetVolumes` |
| `Features/Reader/ReaderController.cs` | HEAR AGAIN punches its label (D3) |
| `Features/Arrange/ArrangeController.cs` | empty-slot-with-nothing-held and empty-UNDO now answer with the instruction line (+ label punch) instead of a click into nothing (D5) |
| `Features/Summary/SummaryController.cs` | DONE TYPING gets its own click via `OnDoneTyping` — routed through a wrapper so `Accept()`, which also closes the keyboard, does not double up on its own `sfx_correct` (D5) |
| `Features/NameEntry/NameEntryController.cs` | starts the menu loop (D6) |
| `Features/TeacherMenu/TeacherMenuController.cs` | new `Reject(message)` helper — status line **plus** nudge — on wrong PIN, cooldown, mismatch, too-short, save-failed, nothing-to-export, all-unlocked, picker-unavailable, recovery-warning, recovery-last-chance and recovery-failed; `sfx_correct` when the PIN is accepted; `sfx_star` on a successful export; nudge when the wipe is armed (D4) |

No audio **import settings**, no `.unity` files, no `Features/Race/*`, no `GameText`/`GameRules`
touched. All six files validate clean.

## 8. Still open

1. **D1 — race music dies after race 1 of each launch.** Highest value; needs the race owner.
2. D9 — move `EnsureSingleAudioListener()` to `Awake` (race owner).
3. D7 — one-file swap so the coin tick stops being the collect sound (owner call).
4. D8 — `music_menu` re-encode to Ogg (loop gap) + load type (device-budget pass).
5. D10 — decide whether the race music routes through `AudioManager`, so one volume governs the
   whole app.
6. §4 — decide whether Arrange/Summary get a bed or stay silent.
7. Nothing here has been heard in Play mode: `PanelIntro`, `UIFloat` and every PrimeTween punch
   only run in Play, and no device build exists yet. This audit is call-site-complete, not
   ear-complete — the mix in §5 needs one pass on the real tablet.
