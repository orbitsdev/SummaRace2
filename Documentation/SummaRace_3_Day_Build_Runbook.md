# SummaRace — The 3-Day Build Runbook

**Written 2026-08-20 · branch `experiment/endless-override-2` · HEAD `bbbc72d`**

Three days to the study. No APK has ever been built. This is the shortest path from here to
40 tablets, with the failures you should expect at each step and what to do about each.

**The rule for the next three days:** if a task does not move an APK onto a tablet, it does not
happen. That includes the mission-select redesign, the legacy `Race.unity` cleanup, and the unused
packages. All of it can wait until after the study.

---

## Day 0 — right now (already started)

**Android Build Support is installing.** Started via Unity Hub CLI:

```bash
"C:/Program Files/Unity Hub/Unity Hub.exe" -- --headless \
  install-modules --version 6000.4.1f1 --module android --childModules
```

`--childModules` pulls the OpenJDK, SDK and NDK along with it. It is roughly 2 GB.

**Verify it landed** — this folder must exist and be non-empty:

```
C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/
```

Before it: only `windowsstandalonesupport`. That is the blocker.

> **If it fails with access denied**, the Hub needs elevation. Close Unity, right-click Unity Hub →
> Run as administrator, then Installs → 6000.4.1f1 → gear → Add modules → Android Build Support
> (tick the child modules too).

---

## Day 1 — get a build to come out at all

### 1.1 Close Unity, confirm the module

Editor closed. Confirm `AndroidPlayer/` exists. Reopen Unity.

### 1.2 Switch platform to Android

`File ▸ Build Profiles ▸ Android ▸ Switch Platform`.

> **Expect 30–90 minutes.** The first switch re-imports every texture in a 945 MB `Assets/` folder
> for a different compression target. It looks like a hang. It is not. Start it and walk away.
>
> Do this **once, today** — never on a study day.

### 1.3 Run the preflight

`SummaRace ▸ Build Preflight`.

Today it reports **FAIL 2 · WARN 2**. After the module install, one FAIL should clear:

| Result | Meaning |
|---|---|
| ✖ Android Build Support not installed | **should now be gone** |
| ✖ No Addressables content for Android | still there until 1.4 |
| ▲ Adaptive icon slots empty | cosmetic, ignore for now |
| ▲ Google EDM present in Assets/ | editor-only, ignore |

**Do not proceed past a FAIL you don't understand.** The tool is read-only and never throws.

### 1.4 Build Addressables for Android

`Window ▸ Asset Management ▸ Addressables ▸ Groups ▸ Build ▸ New Build ▸ Default Build Script`.

This creates `Library/com.unity.addressables/aa/Android/`. Right now that folder only exists for
**Windows**, which is why the preflight fails.

> **Why this is not optional.** The race scene loads its road segments, themes, zones, scenery and
> the runner prefab through `Addressables.InstantiateAsync`. In the Editor those resolve straight
> off the Asset Database, so it looks perfect. In an APK with no Android content,
> `TrackManager.cs:194` returns null → `yield break` at `:197` → the `SetActive(true)` at `:228`
> never runs → **`TrackManager.instance` stays null forever and the race never starts.**
>
> `m_BuildAddressablesWithPlayerBuild` is set to `1` (BuildWithPlayer), so a player build *should*
> produce content automatically — but run it manually once so you can see it succeed on its own.

### 1.5 The Addressables group-path warning — SETTLED, no action needed

The preflight WARNs that 13 of 15 bundled groups have empty build/load path IDs on disk, inherited
from the Endless Runner import. **This has now been proven harmless — do not spend day-1 time on it.**

Evidence: a Windows Addressables content build already succeeded on this machine and produced
**15 bundles, one per group**, including all six the race needs:

```
themes_assets_all_*.bundle          default-zones_assets_all_*.bundle
night-zones_assets_all_*.bundle     default-sky_assets_all_*.bundle
night-sky_assets_all_*.bundle       duplicateassetisolation_assets_all_*.bundle
```

A group with genuinely unusable paths cannot produce a bundle. `BundledAssetGroupSchema.OnEnable`
re-seeds them to Local* at load, exactly as suspected. The Android build will behave the same way.

**If the Android build produces fewer than 15 bundles, then revisit this.** Otherwise ignore the WARN.

### 1.6 Build APK #1

`File ▸ Build Profiles ▸ Build`. Output somewhere outside the repo.

> **Expect this to fail.** First builds always do. The likely causes, in order:

| Symptom | Cause | Fix |
|---|---|---|
| `error CS0246 ... UnityEditor` | An unguarded `using UnityEditor;` in a runtime script | Preflight catches this — it currently reports **0** across 253 player scripts, and the two remaining hits are in Editor-only asmdefs |
| Gradle / JDK / SDK path error | Child modules not installed | Preferences ▸ External Tools — all three boxes ticked to use the Hub-installed versions |
| IL2CPP link errors | Stripping | `Link.xml` already preserves `System.Security.Cryptography.*` (the PIN, session unlock and **Export** all reflect through `SHA256.Create()`) |
| Build succeeds, huge | Expected | ~190–230 MB estimated against a 300 MB cap |

**Budget the whole of day 1 for 1.2 → 1.6.** If you get an APK by end of day 1, you are on schedule.

---

## Day 2 — prove it works on a real tablet

This is the day that finds the real problems. Everything until now was the Editor lying to you
politely.

### 2.1 Sideload to ONE tablet

Enable Developer Options → USB debugging, then:

```bash
adb install -r SummaRace.apk
```

### 2.2 Play the whole loop, on the tablet, on a NON-s01 story

**27 of the 30 stories have never been run through the loop even once.** Pick something from
session 5 or later so you exercise a different world, a different theme and different content.

Walk the full path: Boot → Name Entry → Main Menu → Session Map → Story Select → Read → **Race** →
Arrange → Summary → Results.

What to watch for specifically:

- **Does the race actually start?** If you sit on "Getting ready…" forever, Addressables content
  did not ship. This is the #1 predicted failure
- **Does the road have scenery, and is there a runner?** Same cause
- **Does tapping a lane work?** Tap a third of the screen. If the runner never moves, check
  `activeInputHandler` is still `2` (Both) — the preflight fails the build if it is `1`
- **Frame rate in the race.** Target is 30fps on a 2GB device. **Never measured.** If it misses,
  the first lever is `GameRules.RaceMaxSceneryPerSegment` (currently **14**) — halve it
- **Does narration play?** It is the accessibility support the study depends on
- **Android BACK** — should do nothing, not quit. `BackButtonGuard` swallows it

### 2.3 THE IMPORTANT ONE — prove you can get the data off

Do this on day 2. Not day 10. **There is no second chance to collect the dataset.**

Play one complete story so there is a row to export. Then: Main Menu → teacher corner → PIN →
**Export**. The screen shows the full path. It writes to:

```
/storage/emulated/0/Android/data/com.orbitsdev.summarace/files/export_<stamp>.jsonl
```

Now try to actually retrieve it:

```bash
adb pull /sdcard/Android/data/com.orbitsdev.summarace/files/
```

> ⚠️ **On Android 11+, `Android/data/` is hidden from USB/MTP.** Plug the tablet into a PC and
> open the file browser and you will not see that folder. `adb pull` still works — which is why
> **USB debugging must be part of the install checklist for all 40 tablets.**
>
> If `adb pull` also fails, you need a code change to write somewhere public, and you need to know
> that on **day 2**, not after the study.

### 2.4 Open the exported file and look at it

One row per play-through. Confirm:

- `participantCode` is present and correct
- `raceFirstPickCorrect` matches the stars you saw
- `readingSeconds` is not zero
- `schemaVersion` is **6**

Field-by-field meaning: `SummaRace_Data_Dictionary.md`.

---

## Day 3 — 40 tablets

### 3.1 Build the final APK — on the SAME machine as every future build

> ⚠️ `androidUseCustomKeystore: 0` — the APK is **debug-signed**, and Unity's debug keystore is
> **per-machine**. An APK built on a different PC **will not install over** the deployed one. The
> only fix is uninstall-first, which **erases every learner's progress and every unexported log on
> that tablet.**
>
> If there is any chance of a mid-study rebuild, either always use this machine, or set a custom
> keystore now and keep the file.

### 3.2 Per tablet

1. `adb install -r SummaRace.apk`
2. Leave **USB debugging on** (you need it to collect data)
3. Launch once — it routes to Name Entry
4. **Set the teacher PIN** (Main Menu → teacher corner)
5. **Set the learner's participant code** — the id from that child's paper booklet, behind the PIN
6. Have the child enter their name and pick an avatar
7. Unlock session 1

> **Both teacher steps matter.** The participant code is the join key between the app's process
> data and the paper pretest/posttest. Export warns about missing or duplicate codes, but **a run
> logged without one cannot be joined afterwards.**

### 3.3 If the PIN is ever lost

Teacher corner → **Enter PIN** step → leave the box **empty** → **press and hold OK for 6 seconds**
→ "Reset this tablet?" → ERASE TABLET → confirm.

**Cost: every profile, all progress, and every log on that tablet, permanently.** Export before the
study ends, not after a lockout.

---

## Known limitations that will still be there on day 1

Recorded so they are reported as limitations rather than discovered as surprises.

1. **The memoriser residual is 25/150 pairs, all of them the `SOMEBODY` slot** — every one a
   character's name. You cannot reword a name, and remembering who a story was about *is* the
   recall being tested. Excluding names, the race scores **49.2%** against a 33.3% chance floor.
   That is the honest number to report
2. **Three "worlds" are dressed streets, not what they are named** — `bright_park`,
   `golden_fields` and `autumn_lane`. Neither available theme contains a park, a field or a
   country lane
3. **`s05_hard` and `s06_hard` share the title "In Grandfather's Day"** with the same characters,
   and their `SOMEBODY` answers **disagree** (s05: *Grandfather*; s06: *Sharr and Kaze*). Different
   passages, so not a duplicate — but a learner meets what reads as one story twice and is taught
   two different correct answers. **Researcher content call**
4. **Frame rate on the floor device is unmeasured.** The project renders on Trash Dash's pipeline
   asset, not the `Mobile_RPAsset` tuned for this — `renderScale` 1.0, 2048 shadow map, 4 cascades
5. **`s01_easy` questions are AI-authored** (the researcher's source gives Day 1 EASY only a
   passage). Sign-off formality — **do not rewrite them**

---

## The one-page version

| Day | Must be true by end of day |
|---|---|
| **0** | `AndroidPlayer/` exists |
| **1** | An APK file exists on disk |
| **2** | The full loop plays on a real tablet **and** you have pulled an export off it |
| **3** | 40 tablets installed, PINs set, participant codes set, session 1 unlocked |

If day 2 ends without a successful `adb pull`, **stop and fix that before anything else.** A study
that runs perfectly and yields no retrievable data has failed completely; a study that runs on
slightly ugly menus has not.
