# Asset generation + world variety spec

**Written 2026-08-04.** Two jobs in one document:

1. **§1–§3** — how the race stops being visually repetitive: ten worlds, weather/mood, prop
   arrangement, collectibles, cinematic beats. Almost all of it is **data over art we already
   own**, which is why it fits in the remaining days.
2. **§4** — the assets that genuinely have to be **produced**, each with an exact target path,
   size and priority, so nothing gets generated twice and nothing blocks a build.

Anything dropped into the target path with the right filename appears in-game with **no code
change** — story JSONs already carry their `heroImage` and `narration` paths, and audio loads by
key from `Resources/Audio`. That is deliberate: art can land late without touching logic.

---

## 1. The ten worlds

Every story JSON already carries a `world` field, one per session day. A world is a **recipe**,
not a new scene: zone prop mix × sky/fog/light colour × cloud set × collectible skin. The race
scene is built at runtime by `EndlessRaceDirector`, so a world costs a table row, not a level.

Built from art already in the project: TrashDash `Default` (day) and `NightTime` themes
(Urban / Suburbs / Industrial + cloud prefabs), Supercyan forest props, SimpleNaturePack
rocks/bush/mushroom/stump, SimplePoly fences, ithappy Cartoon City buildings and palms.

| # | World | Mood | Sky / fog | Corridor walls | Ground | Scatter | Clouds |
|---|---|---|---|---|---|---|---|
| 1 | `morning_suburbs` | fresh, gentle | pale blue, soft haze | Suburbs houses | packed dirt trail | hedges, flowers | few, high, white |
| 2 | `bright_park` | open, sunny | saturated blue, no fog | fences + tree lines | grass | trees, benches, flowers | fat, low, bright |
| 3 | `sunset_town` | warm, golden | orange→pink, warm haze | Urban brick + plaster | warm asphalt | lamp posts, bins | thin, gold-lit, streaked |
| 4 | `blue_hour_suburbs` | calm, cooling | deep blue-violet | Suburbs + garages | cool grey road | lit windows, hedges | dark, sparse |
| 5 | `overcast_industrial` | heavy, close | flat grey, strong fog | Industrial warehouses | wet concrete | crates, pipes, cones | full grey overcast |
| 6 | `golden_fields` | wide, hopeful | amber, low warm fog | low fences only | dry grass | haystacks, lone trees | long, thin, gold |
| 7 | `night_city` | electric | near-black + city glow | Urban NightTime | dark wet asphalt | neon signs, lamps | barely visible |
| 8 | `misty_morning` | quiet, mysterious | pale cream, heaviest fog | Suburbs, half-hidden | damp path | ghosted trees | fog, not clouds |
| 9 | `autumn_lane` | crisp, russet | amber-grey | tree tunnel | leaf-littered path | bare trees, leaf piles | broken, moving |
| 10 | `starlit_finale` | celebratory | deep navy + stars | Urban + bunting | dark path, lit edges | lanterns, flags | star field |

**Ordering is intentional:** day → sunset → night → mist → finale. Ten sessions become a
journey, so session 10 *feels* like an arrival. Cheap, and it rewards the learner for finishing
the study.

### Within a session
The three difficulties share a world but shift **time of day** — easy = morning light, average =
midday, hard = late/dusk — via light colour + fog only. So all **30 races look different**
while a session still feels like one place.

---

## 2. Weather and mood

| Effect | How | New art needed? |
|---|---|---|
| Sunny | bright directional light, warm ambient, low fog | no |
| Overcast | desaturated ambient, grey fog raised, dimmer sun | no |
| Golden hour | warm light colour, long shadows, amber fog | no |
| Night | dark ambient, cool fog, emissive windows | no — NightTime theme exists |
| Mist | fog density up, near-plane in, pale colour | no |
| **Rain** | particle system + wet-look ground tint + loop sfx | **yes — 1 small texture + 1 sfx** |

Only rain needs producing, and it is one streak texture plus a loop. Everything else is
lighting and fog values, i.e. a data row.

**Fog is delicate.** F29 found 35/110 bleached the corridor and retuned to 70/260. Any world
that changes fog must be looked at in-engine, not just typed.

---

## 3. Arrangement, collectibles, cinematic beats

**Arrangement** — the corridor already places TrashDash zone sections (they span both sides,
52–76m wide, centred at x=0 and stacked by measured depth, per F29). Variety comes from the
section pool and spacing per world, not new geometry. Rule: never let the same section repeat
twice in a row, and keep the checkpoint zones clear so answer cards always have runway.

**Collectibles** — coin lines between gates are pure juice, no scoring. Skin them per world
(coins → leaves in `autumn_lane`, fireflies in `night_city`, raindrops in a rainy world) so
collecting feels local to the place. Existing `Gems and gold` sprites cover most of this.

**Cinematic beats** — three moments, reusing the F42 start camera:
1. **Pre-race** — a short side tracking shot along the corridor before the countdown, so the
   learner sees where they are. Already exists; give each world a slightly different framing.
2. **Correct pick** — the existing jump + gold burst. Unchanged.
3. **Finish** — 2.2s victory beat with fireworks before Arrange loads. Already exists; tint the
   fireworks to the world palette.

Keep every celebration **≤ 3 seconds** (GDD §6). A 55-minute classroom session must not drag.

---

## 4. Assets to produce

Priority: **P0** blocks a study build · **P1** visibly improves it · **P2** nice to have.

| Pri | Asset | Target path | Size / format | Notes |
|---|---|---|---|---|
| P1 | 30 story hero images | `Assets/_Game/Resources/Stories/Art/<story_id>.png` | ≤1024px, PNG, Sprite | ids are `s01_easy` … `s10_hard`. Placeholders acceptable meanwhile; 3 exist and are temp mockup crops |
| P1 | 4 learner avatars | `Assets/_Game/Resources/UI/avatar_0..3.png` | 256px, PNG, Sprite | Name Entry; kit icons stand in for now |
| P1 | Ms. Lumi teacher | `Assets/_Game/Art/UI/teacher_temp.png` (replace) | ≤512px, PNG, transparent | currently a crop from mockup 21 |
| P1 | 5 SWBST pickup icons | `Assets/_Game/Resources/UI/swbst_s/w/b/s2/t.png` | 256px, PNG | mockup 17; use `SwbstPalette` colours |
| P2 | `sfx_unlock` | `Assets/_Game/Resources/Audio/sfx_unlock.ogg` | short, mono | specified in the asset list, never produced. `TeacherMenuController` currently substitutes `sfx_star` |
| P2 | Rain streak texture | `Assets/_Game/Art/FX/rain_streak.png` | 64×256, PNG, alpha | only art the weather system needs |
| P2 | Rain loop | `Assets/_Game/Resources/Audio/sfx_rain.ogg` | seamless loop | |
| P2 | 10 sky gradients | `Assets/_Game/Resources/UI/sky_<world>.png` | 512×512 | only if per-world tinting proves insufficient |
| P2 | Session-map path art | `Assets/_Game/Resources/UI/map_path.png` | 9-sliceable | map currently uses kit pills on a plain board |

**Do not generate** — already owned, check before producing: UI buttons/panels/icons
(Hyper_Casual_UI), coins/gems/chests (Gems and gold), trophies + 2D icons (Layer Lab), forest
and nature props (Supercyan, SimpleNaturePack, SimplePoly), city buildings/palms (ithappy),
magic/portal/burst FX (Hovl Studio), impact FX (CFXR), characters and run/jump/stumble clips
(Mixamo), cop model (BitGem).

**Two traps, both hit already:**
- Kit sprites can be **baked showcase art**. `GameUI/Level.png` has "Level 23" and
  "Score 232,00" drawn into it and was briefly used as a session-map badge. **Look at any kit
  sprite before wiring it.**
- Some packs import as **Texture, not Sprite** (the Gems and gold PNGs did). Set the importer
  before use or the image silently will not draw in UI.

**No image-generation provider is configured in MCP** (`generate_image` needs a fal.ai or
OpenRouter key). So either the owner/researcher supplies these, a key gets added, or they stay
procedural placeholders — which is the current decision.
