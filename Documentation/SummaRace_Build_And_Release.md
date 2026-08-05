# SummaRace — Build & Release

**From this repo to 40 tablets.** Written 2026-08-05, against the working tree of
`experiment/endless-override-2`. Every number below was read out of the actual file named
beside it, not from memory.

**Scope.** This is the engineering side: install the toolchain, prove the project is
buildable, produce the APK, get it onto tablets, and measure the two acceptance numbers.
The classroom side — teacher PIN, session unlocking, running a session, exporting data —
lives in `SummaRace_Study_Operations_Runbook.md`, and the remaining work list lives in
`SummaRace_Finalization_Plan.md`. Neither is restated here.

> **No APK has ever been built from this project.** That is not a warning about quality; it
> is a statement about what is *measured*. APK size and race frame rate are currently
> estimates derived from disk. Step 0 unblocks the first real build.

---

## 0. The state of play in one screen

| Thing | Value | Where it was read |
|---|---|---|
| Unity | `6000.4.1f1` | `ProjectSettings/ProjectVersion.txt` |
| Android module | **NOT INSTALLED** | `Editor/Data/PlaybackEngines/` contains only `windowsstandalonesupport` |
| Build scenes | 12, `Boot` at index 0 | `ProjectSettings/EditorBuildSettings.asset` |
| Company / product | `Orbitsdev` / `SummaRace` | `ProjectSettings.asset` |
| Package name | `com.orbitsdev.summarace` | `ProjectSettings.asset` |
| Version / code | `1.0` / `1` | `ProjectSettings.asset` |
| Orientation | Portrait (`defaultScreenOrientation: 0`) | `ProjectSettings.asset` |
| Scripting backend | IL2CPP (`scriptingBackend.Android: 1`) | `ProjectSettings.asset` |
| Architectures | ARM64 only (`AndroidTargetArchitectures: 2`) | `ProjectSettings.asset` |
| Min / target SDK | `26` / `0` = Automatic | `ProjectSettings.asset` |
| Signing | debug (`androidUseCustomKeystore: 0`) | `ProjectSettings.asset` |
| Icon | `Assets/_Game/Art/UI/app_icon.png`, all 6 densities | `ProjectSettings.asset` ▸ `m_BuildTargetIcons` |
| Input handling | **Both** (`activeInputHandler: 2`) | `ProjectSettings.asset` |
| Banned packages | none | `Packages/manifest.json` |
| Unity services | all `m_Enabled: 0` | `ProjectSettings/UnityConnectSettings.asset` |
| Addressables → player | **DoNotBuildWithPlayer** (`m_BuildAddressablesWithPlayerBuild: 2`) | `Assets/AddressableAssetsData/AddressableAssetSettings.asset` |
| Addressables content for Android | **never built** | `Library/com.unity.addressables/` has no `aa/Android` |

Two of those rows are build-stoppers, and one is silent. See §1 and §4.

---

## 1. Step 0 — install Android Build Support (the hard blocker)

Nothing else in this document can happen first.

1. **Unity Hub ▸ Installs**.
2. Find **6000.4.1f1** (match the project exactly — a different patch version re-imports the
   whole project and re-serialises assets, which is not something to do two days before a
   study). Click its **gear ▸ Add modules**.
3. Tick:
   - ☑ **Android Build Support**
   - ☑ **OpenJDK** (nested under it)
   - ☑ **Android SDK & NDK Tools** (nested under it)
   - Leave everything else alone. Do **not** add iOS/WebGL/etc. — they add nothing and cost
     disk.
4. Install (several GB, expect 10–30 minutes). **Close the Unity Editor first** — installing a
   module into a running editor's install directory is how you get a half-written toolchain.
5. Reopen the project. Verify with `SummaRace ▸ Build Preflight` (§2): the first line must read
   *"Android Build Support is installed"*.

**Then switch platform once**: `File ▸ Build Profiles` (Unity 6's replacement for Build
Settings) ▸ **Android** ▸ **Switch Platform**. The first switch re-imports every texture in a
945MB `Assets/` folder and takes a long time. **Do this the day before, not on build day.**

If Unity later complains it cannot find the SDK/NDK/JDK: `Edit ▸ Preferences ▸ External Tools`
and tick the three "Android SDK / NDK / JDK Tools Installed with Unity" boxes. They will be
empty until the module is installed, which is exactly the current state.

---

## 2. Step 1 — run the preflight

`Assets/_Game/Editor/BuildPreflight.cs` → menu **SummaRace ▸ Build Preflight**.

It is read-only (it changes nothing) and it never throws — a broken check reports itself and
the rest of the report still prints. Output goes to the Console, to a summary dialog, and to
the clipboard.

What it checks, and why each one is there:

| Check | The failure it is guarding against |
|---|---|
| Android build target support | The blocker in §1, reported with the exact Hub steps. |
| Build Settings list — count, order, index 0, dead paths, disabled entries, duplicates | An earlier import overwrote this file and dropped **every** SummaRace scene. Index 0 decides what the APK launches into; it was once Trash Dash's own scene. |
| Trash Dash scenes not in the list | `Main`/`Start`/`Shop` were in the list once. |
| Every `SceneNames` constant is an enabled build scene | A scene the code can name but the build lacks is a runtime dead end **in the player only** — it works perfectly in the Editor. |
| Player settings: portrait, IL2CPP, ARM64, min SDK 26, ids, version code | The study's stated device requirements. |
| Package name is not a placeholder | Changing the id after deployment makes the next APK a *different app* and orphans every learner's progress and logs. |
| Input handling = Both | Trash Dash's `CharacterInputController` reads legacy `Input.touchCount` for the race swipe. Under "New only" that throws and **touch steering dies on the tablet** — invisible on a keyboard-driven desktop test. |
| App icon is ours, all densities | The build shipped Trash Dash's cat icon through six playtests. Nothing in the game window shows the launcher icon. |
| Addressables build mode + content present | §4. The one that would produce an APK that boots and then has no race. |
| 30 stories load through the real `StoryLoader`; hero art, narration and every `AudioKeys` constant resolve | Counts are re-derived from `GameRules.SessionCount` × `StoryIds.Difficulties`, so the report cannot drift from the game. A missing narration clip is *silent*, not broken. |
| Banned packages, Unity services, `BillingMode.json`, External Dependency Manager | Ads/analytics/purchasing have re-entered `manifest.json` twice. `com.unity.purchasing` auto-initialises at runtime even with its scripts compiled out, so `#if`-ing the code is not enough. |
| `Application.OpenURL` / `UnityWebRequest` reachable from a build scene | 100% offline is a non-negotiable. It resolves the script's GUID against the actual scene files, so a script that merely *exists* on disk is reported separately from one a shipping scene references. |
| Unguarded `using UnityEditor;` in player-compiled code | **This made the project unbuildable and nothing in the Editor revealed it** — the Editor compiles `UnityEditor` happily; only the player build fails. Three instances have been found and fixed. The scan is preprocessor-aware and `.asmdef`-aware, so `#if UNITY_EDITOR` blocks and Editor-only assemblies are correctly excluded. |

**Fix every ✖ before building.** ▲ warnings are judgement calls; each carries its reasoning.

---

## 3. Step 2 — the Addressables content build (do not skip this)

**This is the least obvious way to ship a broken APK, and the project is currently configured
to do it.**

The shipping race scene is Trash Dash's `MainSummaRace`. Its track segments, themes, zones and
the runner prefab are all loaded through `Addressables.InstantiateAsync` /
`LoadAssetsAsync` (14 scripts under `Assets/Scripts` call Addressables). **In the Editor these
resolve straight off the Asset Database**, so everything looks perfect. In a player they come
from built content bundles — and:

- `m_BuildAddressablesWithPlayerBuild: 2` = **DoNotBuildWithPlayer**, and
- `Library/com.unity.addressables/aa/Android` does not exist — content has **never** been built
  for Android.

Ship as-is and the learner gets a Boot screen, a menu, a Reader… and then a race with no road,
no scenery and no runner.

**Two ways to fix it. Pick one and write it on the build checklist.**

*Option A — make it automatic (recommended for a study build):*
`Window ▸ Asset Management ▸ Addressables ▸ Settings` ▸ **Build Addressables on Player Build** =
**Build Addressables content on Player Build**. Every APK then carries current content with no
human step. Costs a few minutes per build.

*Option B — build it by hand, every time:*
`Window ▸ Asset Management ▸ Addressables ▸ Groups` ▸ **Build ▸ New Build ▸ Default Build
Script**, with the platform **already switched to Android** (content is per-platform). Repeat
whenever an addressable asset changes. Forgetting once produces the failure above.

Avoid the third state — `PlayerBuildOption.PreferencesValue` — because it reads a per-machine
Unity *Preferences* value that lives outside the repo, so nobody reviewing the project can see
what a given machine will do.

Re-run the preflight afterwards: it reports the bundle count and the timestamp of the built
content, so "did I remember?" is answerable in two seconds.

---

## 4. Step 3 — build the APK

`File ▸ Build Profiles` ▸ **Android**.

**Check before pressing Build:**

- **Build App Bundle (Google Play)** — **OFF**. An `.aab` cannot be sideloaded; you want an
  `.apk`. (This lives in the build window, not in ProjectSettings, so it is per-machine and the
  preflight cannot see it.)
- **Development Build** — **OFF for the study build.** It adds the debug overlay, disables some
  optimisation, inflates the APK, and prints a "development build" watermark a Grade-4 learner
  will ask about. Turn it **ON only** for the profiling pass in §7 (it is what enables
  *Autoconnect Profiler* and *Script Debugging*), then rebuild with it off. Never hand a
  development build to a learner — the two builds are not the same instrument.
- **Compression Method** — LZ4HC gives the smallest APK at the cost of build time. Reasonable
  here; measure §7 with whatever you ship.
- IL2CPP / ARM64 / min SDK 26 are **not** in this window. They live in
  `Project Settings ▸ Player ▸ Android ▸ Other Settings` (*Scripting Backend*, *Target
  Architectures*, *Minimum API Level*). They are already set correctly — the preflight
  re-verifies them so nobody has to remember.

Press **Build** (not *Build And Run*, unless a tablet is already plugged in). Output to a
folder **outside** the repo — `C:\SummaRaceBuilds\` — so a 200MB APK never lands in git.

**Name the file so it identifies itself on a tablet six weeks later:**
`SummaRace_v1.0_vc1_YYYYMMDD.apk`.

**Build ONE APK and install that same file on all 40 tablets.** Do not rebuild per tablet.
Every learner must run byte-identical content and code, or the study is comparing two
instruments.

The first IL2CPP build is slow (20–45 minutes is normal). Later builds reuse the IL2CPP cache.

---

## 5. Signing — what debug-signed actually means here

`androidUseCustomKeystore: 0`, so Unity signs the APK with its **debug keystore**
(`~/.android/debug.keystore`, auto-generated per machine).

**For this study that is fine, and here is precisely why:**

| | Debug-signed |
|---|---|
| Sideloading onto the 40 tablets | ✅ Works exactly like any other APK. Android only asks you to allow "unknown sources" — a property of *sideloading*, not of the signature. |
| Google Play | ❌ Rejected outright. Not applicable — this app is never published. |
| Updating in place (install a v2 over v1) | ⚠️ Only if v2 is signed with the **same key**. |

That last row is the one that can hurt.

> **Build every study APK on the same machine.** The debug keystore is per-machine and
> per-user profile. If the first build comes from the office PC and a mid-study fix is built on
> a laptop, the new APK will refuse to install over the deployed one ("App not installed" /
> signature mismatch). The only way through is to **uninstall first — which erases that
> tablet's learner profiles, progress and every log that has not been exported.** Mid-study,
> that is lost data, not an inconvenience.

Also: the debug keystore can be regenerated by Android tooling if it expires or is deleted, and
a regenerated one is a *different key* with the same consequence. Back up
`C:\Users\<you>\.android\debug.keystore` alongside the APK.

**If you would rather not depend on that**, switch to a custom keystore *before the first
tablet is flashed* — `Project Settings ▸ Player ▸ Android ▸ Publishing Settings ▸ Custom
Keystore`, create one, and store the `.keystore` file plus both passwords **outside this repo**
(never commit a keystore). Doing it later means the same uninstall-and-lose-data problem, so it
is a now-or-never decision. The preflight reports which mode is active and whether the keystore
file actually exists.

---

## 6. `AndroidTargetSdkVersion: 0` (Automatic) — leave it, with one caveat

`0` = `AndroidApiLevelAuto`: Unity targets the **highest Android SDK platform installed on the
build machine**.

- The Play Store's "must target API N" policy **does not apply** — nothing here is submitted to
  Play.
- Android's per-target-API behaviour changes (scoped storage, background limits, notification
  permission) do not affect this app: it is offline, single-activity, writes only to its own
  `persistentDataPath`, and posts no notifications.

The caveat is **reproducibility**: "Automatic" resolves against whatever the build machine
happens to have, so the same commit can produce two different APKs on two machines. If a
rebuild has to be provably identical later, pin it: `Project Settings ▸ Player ▸ Android ▸ Other
Settings ▸ Target API Level` → a concrete level (33/34 is a safe, well-tested pair with
min 26). Otherwise leave it and record the resolved value from the build log in the study notes.

Min SDK **26** is a real constraint on hardware, not a formality: an Android 7 tablet cannot
install this APK at all. Confirm every one of the 40 tablets is **Android 8.0 or newer** before
build day, not on it.

---

## 7. Getting it onto the tablets

Same APK file, 40 times. Two routes — use whichever the person doing it can actually do.

### Route A — `adb` (fast, scriptable, needs a PC)

1. On the tablet: `Settings ▸ About tablet ▸ tap Build number 7×` → Developer options appear →
   enable **USB debugging**.
2. Plug in over USB; the tablet shows *"Allow USB debugging?"* → tick **Always allow from this
   computer** → Allow.
3. On the PC (`adb` ships with the Android SDK the Unity module just installed —
   `<Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe`):

   ```
   adb devices                       # the tablet must appear as "device", not "unauthorized"
   adb install -r SummaRace_v1.0_vc1_20260805.apk
   ```

   `-r` reinstalls over an existing copy **keeping its data** — but only if the signature
   matches (§5). If you see `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, the signature differs; stop
   and read §5 before uninstalling anything.
4. Repeat for each tablet. With a powered USB hub you can do several in a row without
   re-enabling anything.

Useful during the smoke test:

```
adb logcat -s Unity            # Unity's runtime log, including any exception on device
adb shell dumpsys meminfo com.orbitsdev.summarace | head -20
adb shell ls -l /storage/emulated/0/Android/data/com.orbitsdev.summarace/files/
```

That last path is `Application.persistentDataPath` on Android — where `SaveManager` writes
profiles, `logs/<learnerId>.jsonl`, and the `export_<stamp>.jsonl` +
`export_<stamp>_learners.json` pair that `ExportLogs()` produces. Pulling the export over USB:

```
adb pull /storage/emulated/0/Android/data/com.orbitsdev.summarace/files/export_20260901_1430.jsonl
```

### Route B — no adb (a teacher can do this)

1. Copy the `.apk` to the tablet: USB "File transfer / MTP" mode and drag it to `Download`, or a
   USB-C flash drive with an OTG adapter, or a microSD card. **Do not use a cloud drive or
   email** — the study tablets are meant to be offline, and it is one more thing that can fail
   in a classroom.
2. On the tablet, open **Files ▸ Downloads** and tap the APK.
3. Android blocks it: *"For your security, your tablet isn't allowed to install unknown apps
   from this source."* → **Settings** → toggle **Allow from this source** for the Files app →
   Back → tap the APK again → **Install**.
4. Play Protect may offer to scan an unrecognised app. **Don't send** / **Install anyway** is
   safe — it is unrecognised because it was never published, and "send for scanning" would put
   the app on the network.
5. **Turn the "allow unknown apps" toggle back off** afterwards on tablets children will use.

Either route: the app appears as **SummaRace** with our crown/SUMMA·RACE! icon. **If you see a
cat, you installed the wrong build** (see §8).

---

## 8. On-device verification — the smoke test that has to pass

Do this on **at least one tablet of every model in the study**, and on the weakest one for sure.
It should take under 10 minutes.

1. **Identity.** Launcher shows **SummaRace** with our icon, not a cat and not the Unity logo.
   `Settings ▸ Apps ▸ SummaRace` shows version `1.0`.
2. **Launch scene.** The app opens into the **Boot** splash (crown + SUMMA / RACE! on the sky
   backdrop) → Name Entry on a fresh device, or Main Menu afterwards. **If it opens into a
   runner game with a cat, coins and a shop button, the build list's index 0 is wrong** — stop,
   do not deploy, re-run the preflight.
3. **Portrait.** Rotate the tablet through all four orientations. The game must not rotate.
   Then check split-screen if your tablets expose it (`androidResizeableActivity` is currently
   `1`, so they may) — if the layout breaks in split-screen, either turn that setting off or
   write "no split-screen" on the tablet setup sheet.
4. **Offline, properly.** Put the tablet in **Airplane mode** *and* switch Wi-Fi off, then play
   a whole story end to end. Nothing may stall, retry, or show a network error. Do this
   deliberately — a build that quietly depends on the network passes every test in a connected
   room and fails in the classroom.
5. **The full loop, on a non-`s01` story.** Boot → Main Menu → Session Map → Story Select →
   Reader (5 pages, narration audible, VOICE toggle works) → race briefing → 3-2-1-GO! → five
   gates, deliberately getting some wrong → FINISH → Arrange → Summary → Results (stars and
   treasure gems must match the picks you actually made) → back to the map.
   *27 of the 30 stories have never been played through even once.* Pick one of those.
6. **Audio.** Narration, correct/wrong stings, race music. Music must stop at FINISH so the
   victory sting owns Results.
7. **The race is playable by touch.** Swipe left/right to change lanes. This is the path that
   breaks if Active Input Handling is ever set to "New only" (§2).
8. **Interruption.** Press Home mid-race, wait 10 seconds, come back. The race must resume, not
   freeze; audio must return. Then check the log file still grows — backgrounding once
   discarded the rest of a run's data.
9. **Teacher gate.** Set a PIN (Runbook §1.2), unlock a session, run an export, and confirm the
   file exists at the path the screen prints.
10. **Storage headroom.** `Settings ▸ Storage` — leave the tablet with room to spare; a device
    that fills up mid-study loses logs.

---

## 9. The two numbers the study still owes, and how to measure them

Both are currently **estimated from disk** and become real only after the first build.

### APK size vs the 300MB cap

Estimate on record: ~190–230MB (≈143MB shipped asset payload + 55–90MB engine/IL2CPP).

Measure: the size of the `.apk` on disk, and confirm it on the tablet under
`Settings ▸ Apps ▸ SummaRace ▸ Storage` (installed size exceeds APK size).

If it comes in over budget, these are known-free reductions, in order of return:

- Drop the legacy `Assets/_Game/Scenes/Race.unity` from Build Settings — **~23MB**, nothing
  routes to it (removing `SceneNames.Race` too, or the preflight will flag the constant).
- `music_race.ogg` is byte-identical to Trash Dash's `STEMSMainTrackMono.ogg` and **both ship**
  — ~6.9MB.
- Three textures import uncompressed, ~9MB (`UISpritesheet.png` alone is 6MB).
- `TextMesh Pro/Examples & Extras` ships through its own `Resources/` folder — 2.7MB.
- Deleting unused Addressables groups saves **≈0** — their meshes and textures are shared with
  what does ship. Measured; don't spend time on it.

Use `Window ▸ Analysis ▸ Build Report` (or the `Editor.log` build summary) for the real
per-category breakdown rather than guessing again.

### 30fps floor in the race on the 2GB / Android 8 device

Measure with a **Development Build + Autoconnect Profiler** over USB, then **rebuild without
it** for the study.

Watch: race frame time, GC allocations per frame, and total memory. **RAM is the real pressure
here, not polygons** — the whole Daytime theme is ~80k verts, while ~36MB of Vorbis music is
imported `DecompressOnLoad` and expands to raw PCM in memory.

If the race misses 30fps, the first lever is documented and deliberate: every quality level has
`customRenderPipeline: {fileID: 0}` and `GraphicsSettings` points at **Trash Dash's**
`Assets/RenderingPipeline.asset` with `Assets/UIRenderer.asset` as its renderer, at
`renderScale 1.0`, main-light shadows on with a 2048 map and 4 cascades, and
`m_IntermediateTextureMode: Always`. `Mobile_RPAsset` / `Mobile_Renderer` are assigned to
nothing. **Do not simply reassign the Mobile asset** — the entire race look (curved/unlit
materials) was built and playtested against their pipeline, so swapping it is a visual change
that needs its own playtest, not a config tidy. Prefer, in order: drop `renderScale` to 0.8 on
the *live* pipeline asset, then shadow resolution / cascades, then Bloom in
`_Game/Art/RacePostFx.asset`.

Record both numbers in the study notes with the tablet model beside them. They are acceptance
criteria, not trivia.

---

## 10. Which branch is safe to build — verified, not remembered

**Build from `experiment/endless-override-2`.** Verified in this repo on 2026-08-05:

```
git rev-list --count main..HEAD   → 173
git rev-list --count HEAD..main   → 0
git merge-base main HEAD          → c13b23f  (== main's tip)
```

`main` **is** the merge base. It has not moved since 17 July and contains none of the last 173
commits. Concretely, `main` has:

- **1 story JSON**, not 30 (`git ls-tree main Assets/_Game/Resources/Stories/`)
- **5 narration clips**, not 150
- **no `Core/TeacherGate.cs`** — no PIN, no session gating, no log export
- **no `app_icon.png`** — it would still ship Trash Dash's cat
- a build list that still contains the cut `Settings.unity` and no `MainSummaRace`

> **`main` is not a fallback. It is a 3-week-old prototype.** Building the study from it would
> produce an app with one story, no data collection and no teacher control. There is no
> "safe branch" to retreat to — the branch you are on *is* the product.

**Is this branch safe to ship?** The old warning in `CLAUDE.md` ("branch never ships — ads/IAP
present") is **stale**, and the file itself now says so. Re-verified against the files today:

- `Packages/manifest.json` — no `com.unity.ads`, `com.unity.analytics`, `com.unity.purchasing`,
  `com.unity.microsoft.gdk`. ✅
- `UnityConnectSettings.asset` — every service `m_Enabled: 0`. ✅
- `Assets/Resources/` — empty; no `BillingMode.json`. ✅
- `Application.OpenURL` — zero references from any scene in the build list. `Assets/Scripts/
  OpenURL.cs` still exists on disk, but its GUID appears only in Trash Dash's own `Main.unity`
  and `Start.unity` plus an `Assets/_Recovery/` crash autosave — none of the three is in Build
  Settings. ✅ (`_Recovery` is a Unity crash artefact and is gitignored; it can be deleted.)
- Build list — 12 scenes, `Boot` at index 0, none of theirs. ✅
- `Assets/MobileDependencyResolver/` (Google External Dependency Manager) is **back on disk**.
  It is Editor-only, and there is no `*Dependencies.xml` anywhere in the project, so it resolves
  nothing and injects nothing into the Gradle build. It self-resurrects after a domain reload;
  delete it **with the Editor closed** if you want it to stay gone. ⚠️ harmless, but the
  preflight will keep reporting it.

What remains of Trash Dash is **inactive objects inside `MainSummaRace`** (GameOver, Loadout,
Highscore, leaderboard and store buttons — the five buttons have had their click handlers
emptied and been made non-interactable). Their `UICamera/Game` chrome is **load-bearing —
`GameState.UpdateUI()` dereferences it every frame — do not delete it.** Hidden is not removed,
and cleaning it up properly is scene work that needs a code guard first; none of it is a
shipping blocker, and none of it is reachable in normal play.

**Rollback plan, then, is per-change and not per-branch:**

- Bad build, good repo → rebuild from the last tagged commit. **Tag the exact commit you build
  the study APK from** (`git tag study-build-v1 && git push --tags`) so "what is on the tablets"
  is answerable in one command, forever.
- Bad APK already on tablets → reinstall the previous APK with `adb install -r`. This preserves
  learner data **only if both are signed with the same key** (§5). Keep the previously deployed
  APK file; do not delete it once a newer one exists.
- Bad content only → story JSONs are generated artifacts. Fix
  `Tools/StoryPipeline/overrides.json` and re-run the pipeline; never hand-edit the 30 files.

---

## 11. Build-day checklist

Print this.

- [ ] Android Build Support + OpenJDK + SDK/NDK installed on **6000.4.1f1** (§1)
- [ ] Platform switched to Android (done the day before)
- [ ] `SummaRace ▸ Build Preflight` — **zero ✖**, every ▲ read and accepted (§2)
- [ ] Addressables content built for Android, or "build with player" enabled (§3)
- [ ] Development Build **off**, Build App Bundle **off** (§4)
- [ ] APK built to a folder outside the repo, named `SummaRace_v1.0_vc1_<date>.apk`
- [ ] Commit tagged (`study-build-v1`); APK + `debug.keystore` backed up together (§5, §10)
- [ ] Smoke test passed on the weakest tablet, **with Wi-Fi off** (§8)
- [ ] APK size and race fps recorded in the study notes with the tablet model (§9)
- [ ] The **same single APK file** installed on all 40 tablets (§4, §7)
- [ ] Teacher PIN set on every tablet **before any child touches it** — Runbook §1.2
