# Decision record — the race clock, the multiplier, and fail/retry

**2026-08-22.** Raised by the owner from the web prototype (`tdrx44.csb.app`) and his 21 Aug note:

> *"Timer is about the next options appear? … Clock countdown maybe the logic behind it is that you
> need to finish the collection within it; a correct item [raises] the timer/multiplier and the
> speed until you finish; but if [the time] ends it's failed and retry again the race — I think
> that would be the possible client want? … our current is good but it seems illogical to have a
> timer for the next-appearing collection."*

The reading was right, and the confusion was real: **the build's countdown and the prototype's
countdown are two different mechanics.** Ours counts to *the next item arriving*. The prototype's
counts to *the race ending*.

---

## 1. What the prototypes actually do — established, not assumed

**The prototype clock is a single global race clock.** Proven by arithmetic: the screenshot
filenames carry real timestamps, so elapsed wall-time can be checked against the timer delta.

| frames | real seconds elapsed | timer | note |
|---|---|---|---|
| `c.png` → `080056` | **5.4 s** | 89s → 83s | an answer is collected across this pair (question changes, multiplier x1 → x2) and the clock **keeps falling** |
| `080121` → `080141` | **19.9 s** | 87s → 68s | five items, four collections, still monotonic |
| canva mockups 15 → 19 | one run | 90 → 88 → 84 → 75 → 60 | never returns to 90 |

It never resets mid-run. The one apparent reset — 65s → 87s — has a **second race briefing screen
between the two frames**: a restart, not time being added.

> ⚠️ This **corrects `SummaRace_Test_Checklist.md` §3**, which concluded that correct picks *add*
> time, from two frames 11s apart reading 65s → 84s. Those frames are `080113` and `080124`, and
> the restart at `080116` sits between them. Struck through in that document.

**The multiplier is a progress counter, not a score.** Measured across every race frame it is
exactly `1 + parts collected` — x1 with none banked, x6 at "Goal!" — it does **not** reset on a
wrong pick, and **there is no score anywhere on the race HUD for it to multiply.** It also appears
in only **one** of the two prototypes: the canva deck has no multiplier on any of its five race
screens, and the GDD never mentions one.

**Neither prototype ever shows the clock reaching zero.** No fail state is asserted anywhere in
either deck. The "failed and retry" reading is an inference, not something the client's own screens
show.

---

## 2. The ruling

### 2a. No global race clock — **decided, and this one is reversible by the owner alone**

D7 permits a timer that cannot fail, so a purely cosmetic clock *is* within the owner's gift. It is
still the wrong call, for three reasons that are about the instrument rather than taste:

1. **It scores reading speed.** The measured decoding budget is ~11.6s per gate at 100 wpm and
   ~16.5s at the 70 wpm a struggling Grade-4 reader manages. The build now delivers **16.8–23.4s**
   per gate. A 90-second clock across five gates gives 18s each with no slack — it would take back
   the entire D2 reading-time decision, and take it back from exactly the children the study
   exists to help.
2. **A countdown that cannot fail is a lie the child finds.** Ten sessions is enough for any
   nine-year-old to notice that the number reaching zero does nothing. At that point it is not
   urgency, it is noise — and it has cost the reading time in the meantime.
3. **We already ship its honest half.** Results shows *"Your race: m:ss!"* after the run, where it
   cannot pressure anyone. That was built for exactly this reason.

### 2b. No multiplier — **decided**

It multiplies nothing, it exists in one deck of two, the GDD is silent on it, and GDD §8.4 forbids
the direction it points: *"the app never displays scores that could be confused with rubric
grades."* Its actual function — *how many parts do I have* — is already carried, better, by the
five-plaque tracker, which also says **which** parts and **what is next**.

What the multiplier was reaching for is real: the race is the same interaction five times with no
rising curve. The answer to that is on record and is **escalate the reward, never the threat** — a
music layer per part, the tracker brightening, a bigger collect burst, so part five lands like a
finale.

### 2c. No fail/retry — **not the owner's decision to take, and not mine**

This is the half that cannot simply be chosen:

- **D7 is LOCKED**: *"Soft pressure only. Timers and the Patrol create urgency but can never fail,
  block, or punish a learner."* Per GDD §10.2, changing a locked decision *"requires all three
  researchers + developer sign-off and an edit to this document."*
- **A retry breaks the headline measure.** `raceFirstPickCorrect` is the star count *and* the
  logged variable the study reports. A learner who re-runs a story answers the same five gates
  twice; the second attempt is memory, not comprehension, and nothing in the export could separate
  them.

If the client asks for it, the route is a researcher sign-off and a GDD edit — not a code change.

---

## 3. What was built instead, from the same review

The sweep that produced this ruling also found seven things that were genuinely wrong, all now
fixed: the reading window opened ~1.5s late at every gate; the five SWBST words appeared nowhere in
the race; the last gate announced nothing; the Reader's narration toggle was unreachable during
every question of all 30 stories; the Reader's answer pills were separated from their card by hue
alone; Arrange erased the SWBST word exactly when the learner reviews the order; and Results never
said the learner had cleared anything. See `CLAUDE.md`, row **F60**.

## 4. If this is revisited

The cheapest option that gives the client's 90-second look without touching D7 is to point the
countdown at **the finish line** rather than at the next item: the same draining number, honest
(it is distance ÷ speed, which `SecondsToCover` already computes), and reaching zero means the
learner has arrived rather than failed. It is roughly half a day. It is **not** recommended — it
reintroduces a clock's pressure for a clock's look — but it is the option to reach for first if the
client will not be moved.
