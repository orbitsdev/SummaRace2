# Expert Game-Dev Critique — 2026-08-22 (post fix-pass)

Verdict up front: **this is a shippable, honest piece of educational game design.** The loop is
complete with zero dead ends, the support-removal ladder is real game design (not worksheet-
with-graphics), wrong answers teach without punishing while the race keeps its juice, and the
data layer is more rigorous than most commercial games'. The findings below are ranked by
what they cost the player or the study.

## Findings and dispositions

1. **Passive play is invisible in the data** (proven live: a no-input run collects the centre
   card at every gate; scores ≈ chance, statistically sound, but "engaged & wrong" and "never
   engaged" are indistinguishable). → **FIXED THIS PASS**: `raceSteerCount` logged per run
   (schema 7, additive; 0 on a finished run = passive run). Dictionary updated.

2. **Badge identity undersold** (owner's own observation: the pill/teacher badge was a plain
   tinted square, not the heart/star/gem/lightning the child picked). → **FIXED THIS PASS**:
   real icon sprites in `Resources/UI/Badges/` shown everywhere; tinted-quad kept as fallback.
   Name Entry's own tiles were always real icons; its lack of an exit is CORRECT (one-time
   setup, never a trap — empty name → "Runner").

3. **Legacy `Race.unity` ships ~12–20MB of dead APK weight** (zero call sites). → Remove from
   Build Settings **after** the current APK is verified on device (one click; don't change
   build variables mid-flight). Loading is name-based, so index shift is safe.

4. **Code hygiene**: `GameRules` orphaned constants (RaceSecondsPerGate, GateTime*, Patrol
   screen-space block, Danger*, RaceLeaveConfirmSeconds) + a stale `MaxRacePicks` comment
   describing the deleted re-present mechanic. → Post-APK cleanup task; tests now run, so a
   cleanup can be verified. Not done mid-build-window by choice.

5. **Post-study polish list** (none affect the study): Results echo-card geometry tuned for
   the old 200-char cap (ellipsis covers it) · `music_menu.mp3` has an audible loop gap ·
   three "park/field" worlds are dressed streets (theme asset limitation, documented) ·
   `music_race.ogg` byte-duplicates a Trash Dash stem (~7MB) · uncompressed UI textures ~9MB.

## What was deliberately NOT critiqued into change
No timers, no fail state, no score/multiplier, no hint button, no settings screen, forward-
only Arrange/Summary — each traces to the thesis text, a locked decision (D7/D8), or a
measured validity threat. The prototypes are direction, not a quality bar: both contain
mechanics the study never asked for and contrast failures the build already avoids.

## Post-study art direction (owner references, 2026-08-23)

The owner supplied two reference kits that name the target look precisely: "Jungle forest
lianas game interface" and "Bamboo game interface wooden sign boards" (both Pro Vector,
PAID — the previews are watermarked and must never be used directly; buy a license first).
What they codify: wood-plank buttons and title boards, leaf/vine framing around panels,
gold stars on wood, lighter cards held inside darker wood frames. Tonight's wood pass
(wood-plaque banners, parchment cards, bronze trim) is the same family built from owned
assets — the study build ships that. The kit purchase + full re-skin is the store-version
art pass: every button to wood planks, leafy corners on the mission board and story cards,
bamboo-frame variants for the teacher screen.
