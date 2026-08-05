# SummaRace — Asset Shopping List

**Written 2026-08-05. Audience: the owner, going out to find/download/generate art and audio.**
**Deadline context: the 40-learner study starts in 2 days.**

This is the *one* document to work from. Everything below was checked against the actual repo on
disk — every path, filename and pixel size here is real. Read §0 first; it tells you what you can
safely skip.

---

## §0. Read this first — the honest triage

**Nothing on this list stops the game from running.** Every story, every narration clip, every
sound effect and every hero image already resolves at runtime. The game is playable end-to-end
today. So treat this list as *"make it look like a finished product"*, not *"make it work"*.

There are **two genuinely blocking items**, and neither is art:

| # | Item | Why it blocks |
|---|---|---|
| **B1** | **Unity Android Build Support module** (+ SDK/NDK/JDK) | `BuildPipeline.IsBuildTargetSupported(Android)` returns **false** in this Unity install. **No APK can be produced at all.** Nothing else on this list matters if the game cannot be installed on a tablet. |
| **B2** | **Addressables content built for Android** | Added here 2026-08-06, after this list was first written. `m_BuildAddressablesWithPlayerBuild: 2` and there is no `aa/` folder for any platform, so the APK would ship **without the race's road, scenery or runner** — they all load through Addressables, which resolve off the Asset Database in the Editor and therefore look perfect there. Turn on Addressables ▸ Settings ▸ *Build Addressables on Player Build*. |

**Get B1 and B2 first.** B1: Unity Hub → Installs → the Unity 6 entry → gear icon → *Add modules*
→ tick **Android Build Support**, **Android SDK & NDK Tools**, **OpenJDK**. ~2 GB, no licence
hunting. Do both before you go looking for pictures. Full procedure:
`SummaRace_Build_And_Release.md` §1 and §3.

After that, in value order:

1. **§2 — 27 story hero images.** This is the single biggest visual win. Right now 27 of the 30
   story cards show a **blank blue-green gradient** (verified — they are literally empty sky/grass
   fills, no drawing at all). Learners in sessions 2–10 see a card with a title and no picture.
   Not blocking, but it is the difference between "prototype" and "finished".
2. **§3 — Ms. Lumi consistency.** There are currently **two different Ms. Lumis** in the game, in
   two different art styles. Cheap to fix, noticeable.
3. **§4 — everything else.** Mostly *skip for this study*; several items need a programmer, not a
   download. Each entry says so explicitly.

**Ground rules for every download (non-negotiable):**
- The game is **100% offline**. Nothing that needs a network call, a login, an SDK or a tracker.
- Licence must allow **redistribution inside an APK** for an academic/thesis instrument.
  **CC0 is safest.** CC-BY is fine but you must credit. Avoid "personal use only", "no
  redistribution", and anything requiring attribution *inside* the app UI (we have no credits
  screen).
- AI-generated images: fine for this use, you own/are free to use the output on ChatGPT, Gemini
  and Firefly's standard terms. Keep the prompts (they are in §2) so the method is documentable
  in the thesis.
- **Start a credits file the moment you download anything.** There is no credits tracker in this
  repo yet. Make one: `Documentation/Asset_Credits.md`, one line per asset — *what, where from,
  licence, author*. A thesis committee can ask.

---

## §1. Spot-check findings (things the previous audit had slightly wrong)

Verified directly on disk while writing this, so you are not misled while shopping:

- **`s01_easy/average/hard.png` are NOT "temp crops from a mockup".** They are full
  1536×1024 illustrations, generated from the three prompts that used to live in
  `Documentation/Art_Generation_Prompts.md` (that file has been deleted as fully superseded by
  §2 of this document, which carries all 27 remaining prompts in the same house style).
  **The three `s01` images are the style reference for the other 27.** Do not replace them.
- **The 27 placeholders are blank.** `s02_easy.png` (and its 26 siblings) is a smooth blue-to-green
  gradient with nothing drawn on it. There is no partial art to "improve" — you are starting from
  nothing on those 27.
- **Hero art size mismatch:** the 3 real ones are **1536×1024 (3:2)**; the 27 placeholders are
  **1024×640 (8:5)**. The card Image has **`preserveAspect` turned off**, i.e. it stretches to
  fill. So generate all 27 at **3:2** to match the real three — a consistent set of 30 is what
  matters.
- **`mslumi_wave.png` / `mslumi_cheer.png` are 2048×1024 but Lumi only occupies the middle ~44%
  of the width** (measured: pixels x=507→1406, y=48→1024 for `wave`). The layout code in
  `EndlessRaceDirector` has that padding **hard-coded into its anchor maths**. A tightly-cropped
  replacement will render at the wrong size and position. See §3 for the exact spec.
- **`teacher_temp.png` is 290×290 and fully opaque** (no transparency at all) — it is a square
  crop with a green ring and green corners baked in. It is used in **Arrange** and **Summary**.
- **The 4 learner avatars are NOT loaded from a file path.** `NameEntryController` uses four
  `Button`/`Image` references wired in the scene. Dropping `avatar_0.png` into `Resources/UI/`
  does **nothing** on its own — it needs someone to assign the sprites in the Unity Editor.
  (The old doc implied it was drop-in. It is not.)
- **`sfx_unlock` likewise is not drop-in.** `TeacherMenuController` explicitly plays
  `AudioKeys.SfxStar` and has a comment saying so. Dropping `sfx_unlock.ogg` in changes nothing
  until a line of code is changed.

---

## §2. The 27 story hero images — the main job

### What they are for

Each story's **Story Select card** shows this image above the title. It is the learner's first
sight of the story. `s01_*` already have real art; sessions 2–10 do not.

### Exact spec (identical for all 27)

| Property | Value |
|---|---|
| **Destination folder** | `Assets\_Game\Resources\Stories\Art\` |
| **Filename** | exactly the story id + `.png` — e.g. `s02_easy.png` (see table) |
| **Dimensions** | **1536 × 1024** pixels |
| **Aspect ratio** | **3:2 landscape** (wide) |
| **Format** | **PNG** |
| **Transparency** | **No** — full-bleed opaque illustration, edge to edge |
| **Text in image** | **None.** No titles, no letters, no watermarks, no signatures |
| **Composition rule** | Characters/action on the **LEFT and CENTRE**; keep the **RIGHT side simple** (sky, grass, empty wall) — the title text sits there |
| **Priority** | **NICE-TO-HAVE** (the placeholders load; the game runs) — but the highest-value one on this list |

> **File size doesn't matter.** Unity re-compresses on import and the importer already caps these
> at **1024 px for Android** (checked in the `.meta`). A 2–3 MB PNG from an image generator is
> completely fine; it will not bloat the APK.

> **⚠️ Overwrite, don't rename.** Save each new file **over** the existing placeholder with the
> **same filename**. That keeps the `.meta` file, which already has *Texture Type = Sprite* set.
> If you instead add a new file under a new name you must set the importer by hand and edit JSON.
> Never delete the `.png.meta` files.

### The 27 images (real titles and content, read from the story JSONs)

| # | File | Session | Difficulty | Story title | What it must depict |
|---|---|---|---|---|---|
| 1 | `s02_easy.png` | 2 | Easy | **Escaping the Room** | Three kids in an escape room; the key is frozen inside a block of ice |
| 2 | `s02_average.png` | 2 | Average | **Seeking a Friend** | Mateo at an animal shelter meeting Santiago, an old cat |
| 3 | `s02_hard.png` | 2 | Hard | **Shawn the Speedy Snail** | Shawn the fast snail racing ahead of the other snails to the food |
| 4 | `s03_easy.png` | 3 | Easy | **Emma's Favorite Restaurant** | Emma's family at their weekly Italian restaurant; ravioli and spaghetti |
| 5 | `s03_average.png` | 3 | Average | **The Case of the Missing Lunch** | Timmy upset because his favourite lunchbox is missing at school |
| 6 | `s03_hard.png` | 3 | Hard | **The Selfish Giant** | Children playing in the Giant's beautiful garden as the Giant returns |
| 7 | `s04_easy.png` | 4 | Easy | **A Visit to the Zoo** | Omar's family at the city zoo, lions resting in the shade |
| 8 | `s04_average.png` | 4 | Average | **Be Careful What You Wish For** | Rylee with the strange necklace her Uncle Aaron sent, and its warning letter |
| 9 | `s04_hard.png` | 4 | Hard | **AgitAgueda** | Portugal's AgitAgueda street festival — the Umbrella Sky Project |
| 10 | `s05_easy.png` | 5 | Easy | **The Crowded House: A Folktale** | The Rubin family's tiny overcrowded house and the wise Reb Solman |
| 11 | `s05_average.png` | 5 | Average | **Owen and Mzee** | Owen the rescued baby hippo beside Mzee the giant tortoise |
| 12 | `s05_hard.png` | 5 | Hard | **In Grandfather's Day** | Sharr and Kaze listening to Grandfather's stories about life long ago |
| 13 | `s06_easy.png` | 6 | Easy | **You Can't Always Tell** | A father and son who lost their home in a flood, staying hopeful |
| 14 | `s06_average.png` | 6 | Average | **Shuffle to Buffalo** | Amelia practising her tap-dance solo before the recital |
| 15 | `s06_hard.png` | 6 | Hard | **In Grandfather's Day** | Same grandfather, birthday visit — life before thought-controlled machines |
| 16 | `s07_easy.png` | 7 | Easy | **The Boy With the Ball** | Hector, new in town, alone with his ball at a new school |
| 17 | `s07_average.png` | 7 | Average | **Sploosh!** | Chef Pierre chasing the giant frog he can never catch |
| 18 | `s07_hard.png` | 7 | Hard | **First Day at the Factory** | Stanley Marks's first day on Ford's assembly line |
| 19 | `s08_easy.png` | 8 | Easy | **An Ice Idea** | Doris's family and their old icebox in the summer heat |
| 20 | `s08_average.png` | 8 | Average | **How to Skateboard** | A beginner in full safety gear learning to balance on a skateboard |
| 21 | `s08_hard.png` | 8 | Hard | **Anansi and the Cook Pots** | Anansi the spider with webs tied from his eight legs to eight cook pots |
| 22 | `s09_easy.png` | 9 | Easy | **A Pool Fit for a Hedgehog** | Cora at Heather's new backyard pool on a very hot day |
| 23 | `s09_average.png` | 9 | Average | **First Fast** | Asma's first Ramadan fast — breaking it with family at sunset |
| 24 | `s09_hard.png` | 9 | Hard | **Baba Yaga, the Girl, and the Hedgehog** | Marusia captured in Baba Yaga's forest, with the talking hedgehog |
| 25 | `s10_easy.png` | 10 | Easy | **Why Does the Ocean Have Waves?** | Ocean waves, wind, sun and moon — the forces that move the sea |
| 26 | `s10_average.png` | 10 | Average | **Clara Barton: Civil War Hero** | Clara Barton bringing supplies and care to wounded soldiers |
| 27 | `s10_hard.png` | 10 | Hard | **We Also Serve** | Brave animals in service — a message pigeon and a guide dog |

> Note #12 and #15 share the title *In Grandfather's Day* — that is correct, the researcher's
> source doc has two versions. The prompts below deliberately make them **visually different**
> (photos-on-the-couch vs. birthday-cake) so learners don't think it's a duplicate.

### Where to get them

**Recommended: AI image generation.** 27 illustrations in one locked style is not something you
can shop for — no free pack will contain "Anansi with eight cook pots" *and* "Clara Barton" *and*
"the Umbrella Sky Project" in a matching style. The existing `s01_*` art was generated this way
and looks right.

| Tool | Notes |
|---|---|
| **ChatGPT (GPT image)** | Best prompt-following for specific scenes. Ask for "wide landscape". |
| **Google Gemini / Imagen** | Fast, good at bright cartoon styles. |
| **Adobe Firefly** | Explicitly commercially-safe training data — the strongest licence story for a thesis, if a committee asks. |
| **Bing Image Creator** | Free fallback. |

**Consistency is the point.** Do all 27 in **one chat session, in order**, so the model keeps the
style. If one drifts (different outline weight, muted colours, 3D-render look), regenerate it
before moving on — do not "fix it later", the whole set has to look like one book.

**Non-AI fallback if you run out of time:** stock cartoon vector scenes from
**freepik.com** (free tier, attribution required — check each file's licence) or **openclipart.org**
(CC0). Quality will be lower and consistency will suffer. Only do this if AI generation is
unavailable.

### The style block (what makes all 30 match)

Every prompt below already contains this. It is repeated here so you can spot a drifting image:

> *Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
> candy colours, bold clean outlines, cheerful, soft warm lighting. Characters on the LEFT and
> CENTRE, RIGHT side simple and uncluttered. No text, no letters, no watermark.*

Plus, for all of them: **age-appropriate for 9–10 year olds — never scary, never violent, never
sad-ending. Diverse, Filipino-friendly children where children appear.**

---

### ✂️ Ready-to-paste prompts

Copy one block at a time. Ask for **landscape / wide (3:2)** every time.

#### 1 — `s02_easy.png` · Escaping the Room
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: three friendly children
(two girls and a boy, about 10 years old, diverse) inside a colorful puzzle escape room; on a
table in front of them sits a large clear block of ice with a shiny golden key frozen inside;
one child points at the ice, another looks thoughtful; puzzle boxes, a wall clock and a locked
door in the background. Keep the children on the LEFT and CENTER, keep the RIGHT side simple for
a title. Friendly and fun, not tense. No text, no letters, no numbers, no watermark.
```

#### 2 — `s02_average.png` · Seeking a Friend
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a kind boy about 10 years
old kneeling at an animal shelter beside a gentle old grey cat with a slightly scruffy coat; the
boy reaches out a hand and smiles warmly; his mother stands behind him; rows of clean pet kennels
and a few blankets and bowls around them. Keep the boy and cat on the LEFT and CENTER, keep the
RIGHT side simple for a title. Warm and hopeful, never sad. No text, no letters, no watermark.
```

#### 3 — `s02_hard.png` · Shawn the Speedy Snail
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a cheerful cartoon snail
with a bright swirled shell zooming ahead with a little motion trail along a grassy ditch, far in
front of three slower snail friends behind him; ahead of him a pile of fresh green leaves and a
small puddle of water; tall grass and wildflowers. Keep the snails on the LEFT and CENTER, keep
the RIGHT side simple for a title. Funny and light-hearted. No text, no letters, no watermark.
```

#### 4 — `s03_easy.png` · Emma's Favorite Restaurant
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a happy family of four at
a cozy Italian restaurant table with a red checkered tablecloth; a girl about 10 with a plate of
ravioli and her brother with a plate of spaghetti, parents smiling beside them; warm hanging
lamps, a pizza oven and a menu board in the background. Keep the family on the LEFT and CENTER,
keep the RIGHT side simple for a title. No text, no letters, no watermark.
```

#### 5 — `s03_average.png` · The Case of the Missing Lunch
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a boy about 10 years old
looking worried and searching an empty classroom cubby where his lunchbox should be; his hands
are open and empty; two classmates nearby notice and look concerned; backpacks on hooks, desks,
a bright classroom window. Keep the boy on the LEFT and CENTER, keep the RIGHT side simple for a
title. Gentle worry, never distressing. No text, no letters, no watermark.
```

#### 6 — `s03_hard.png` · The Selfish Giant
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: several happy children
playing in a beautiful walled garden full of green grass, blossoming fruit trees, flowers and
singing birds; at the garden gate a very large, tall giant with a big beard has just arrived and
looks stern but cartoonish and not frightening. Keep the children on the LEFT and CENTER, the
giant partly at the edge, keep the RIGHT side simple for a title. Storybook fairy-tale feel,
never scary. No text, no letters, no watermark.
```

#### 7 — `s04_easy.png` · A Visit to the Zoo
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a happy family at a city
zoo on a summer day; a boy about 10 points excitedly at two friendly lions resting in the shade
of a rock; his little sister holds their mother's hand; giraffes and a monkey rope visible
further back; signposts and green trees. Keep the family on the LEFT and CENTER, keep the RIGHT
side simple for a title. No text, no letters, no watermark.
```

#### 8 — `s04_average.png` · Be Careful What You Wish For
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a girl about 10 years old
in her bright bedroom holding up an unusual antique necklace with a large glowing gemstone that
she has just unwrapped; an opened parcel and a handwritten letter lie on her desk beside her;
soft magical sparkles around the gem. Keep the girl on the LEFT and CENTER, keep the RIGHT side
simple for a title. Curious and magical, never spooky. No text, no letters, no watermark.
```

#### 9 — `s04_hard.png` · AgitAgueda
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a joyful Portuguese street
festival — a narrow old town street completely covered overhead by hundreds of colourful open
umbrellas strung between the buildings, casting rainbow shade; happy visitors and a family
walking below, street food stalls, bunting, a small music stage, pastel painted houses with
balconies. Keep the people on the LEFT and CENTER, keep the RIGHT side simple for a title. No
text, no letters, no watermark.
```

#### 10 — `s05_easy.png` · The Crowded House: A Folktale
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a cutaway view of a tiny
cosy cottage absolutely packed with a large cheerful family bumping into each other — children,
parents and grandparents squeezed around one small table; at the open door a kind old wise man
with a long beard and a walking stick is visiting; a chicken and a goat peek in at his feet.
Keep the family on the LEFT and CENTER, keep the RIGHT side simple for a title. Funny and warm.
No text, no letters, no watermark.
```

#### 11 — `s05_average.png` · Owen and Mzee
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a small friendly baby
hippopotamus standing close beside a huge old tortoise with a beautiful patterned shell, at the
edge of a calm pond in a green wildlife park; the little hippo looks up at the tortoise
hopefully; palms, reeds and warm African sunlight. Keep the two animals on the LEFT and CENTER,
keep the RIGHT side simple for a title. Tender and gentle. No text, no letters, no watermark.
```

#### 12 — `s05_hard.png` · In Grandfather's Day
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: two curious children, a
girl and a boy about 10, sitting on a couch on either side of their smiling grandfather, who is
showing them an old photo album on his lap; on the table beside them lie an old-fashioned
computer keyboard and a toy car as curiosities; the room is softly futuristic with rounded
furniture and gentle glowing panels. Keep the three on the LEFT and CENTER, keep the RIGHT side
simple for a title. Warm and nostalgic. No text, no letters, no watermark.
```

#### 13 — `s06_easy.png` · You Can't Always Tell
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a father and his young
son standing together on a muddy road beside their small flooded house, water still covering the
yard and a few belongings floating; the father rests a reassuring hand on the boy's shoulder and
smiles calmly while the boy looks downcast; the storm is clearing and warm sunlight breaks
through the clouds. Keep the two on the LEFT and CENTER, keep the RIGHT side simple for a title.
Hopeful, never grim or frightening. No text, no letters, no watermark.
```

#### 14 — `s06_average.png` · Shuffle to Buffalo
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a girl about 10 in a
sparkly tap-dance costume and shiny black tap shoes practising a step mid-air on a wooden stage;
a red velvet curtain and a few stage lights behind her, a mirror and a dance bag at the side;
she looks focused and a little nervous but determined. Keep the girl on the LEFT and CENTER,
keep the RIGHT side simple for a title. No text, no letters, no watermark.
```

#### 15 — `s06_hard.png` · In Grandfather's Day
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a small birthday party in
a softly futuristic living room — two children, a girl and a boy about 10, carrying a birthday
cake with candles to their delighted grandfather in his armchair; on a side table sits a sleek
headband device and a floating holographic photo frame; balloons and streamers, rounded modern
furniture with gentle glowing panels. Keep the three on the LEFT and CENTER, keep the RIGHT side
simple for a title. Joyful. No text, no letters, no watermark.
```

#### 16 — `s07_easy.png` · The Boy With the Ball
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a boy about 10 years old
standing alone at the edge of a school playground holding a football/soccer ball under his arm,
looking a little shy; a group of other children play together further away in the background;
school building, fence, green grass and bright afternoon sky. Keep the boy on the LEFT and
CENTER, keep the RIGHT side simple for a title. Gently hopeful, not lonely or sad. No text, no
letters, no watermark.
```

#### 17 — `s07_average.png` · Sploosh!
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a round, funny cartoon
chef with a big moustache and a very tall white chef's hat lunging with a net at a large grinning
green frog that is leaping away from him over a pond; two skinny assistant chefs behind him look
on; lily pads, reeds and a splash of water. Keep the chef and frog on the LEFT and CENTER, keep
the RIGHT side simple for a title. Slapstick and funny. No text, no letters, no watermark.
```

#### 18 — `s07_hard.png` · First Day at the Factory
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a young man in early-1900s
work clothes, suspenders and a flat cap standing nervously but proudly at his station on a car
assembly line; a long conveyor belt carries early automobile bodies past rows of workers; big
factory windows let in warm light. Keep the young man on the LEFT and CENTER, keep the RIGHT side
simple for a title. Historical, warm and dignified, never bleak. No text, no letters, no
watermark.
```

#### 19 — `s08_easy.png` · An Ice Idea
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a 1920s kitchen on a hot
summer day; a girl about 10 and her mother stand at an open wooden icebox with a large melting
block of ice inside and a puddle on the floor; milk bottles and vegetables on the shelves; bright
sunlight streams through a lace-curtained window. Keep the girl and mother on the LEFT and CENTER,
keep the RIGHT side simple for a title. No text, no letters, no watermark.
```

#### 20 — `s08_average.png` · How to Skateboard
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a child about 10 wearing a
helmet, knee pads and elbow pads carefully balancing with both arms out on a colourful skateboard
on a smooth empty park path; a spare skateboard and a water bottle on the grass nearby; trees,
benches and a sunny sky. Keep the child on the LEFT and CENTER, keep the RIGHT side simple for a
title. Encouraging and safe-looking. No text, no letters, no watermark.
```

#### 21 — `s08_hard.png` · Anansi and the Cook Pots
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a friendly cartoon spider
character with big expressive eyes and eight long legs, sitting in the middle of a village
clearing; a thin silky web thread is tied from each of his eight legs and stretches away to eight
different bubbling cook pots over small fires; he looks pleased with himself; round thatched huts,
palm trees and a river behind. Keep the spider on the LEFT and CENTER, keep the RIGHT side simple
for a title. West-African folktale feel, funny, never creepy. No text, no letters, no watermark.
```

#### 22 — `s09_easy.png` · A Pool Fit for a Hedgehog
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: two happy girls about 10
years old in a sunny backyard beside a brand-new blue above-ground swimming pool; one girl is
climbing the little pool ladder and the other stands admiring it; a small friendly pet hedgehog
watches from the grass nearby; garden fence, flowers and a very hot bright summer sky. Keep the
girls on the LEFT and CENTER, keep the RIGHT side simple for a title. No text, no letters, no
watermark.
```

#### 23 — `s09_average.png` · First Fast
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a Muslim girl about 10
years old in a pretty headscarf sitting proudly with her family at a table at sunset, reaching for
a plate of dates and a glass of water to break her first Ramadan fast; warm lanterns hang above,
a crescent moon in the orange evening sky through the window; parents and a younger brother smile
at her. Keep the girl and family on the LEFT and CENTER, keep the RIGHT side simple for a title.
Respectful and warm. No text, no letters, no watermark.
```

#### 24 — `s09_hard.png` · Baba Yaga, the Girl, and the Hedgehog
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a brave girl about 10 in a
red folk-style dress standing in a colourful enchanted forest clearing beside a small friendly
talking hedgehog who looks up at her; behind them a whimsical wooden hut standing on two large
chicken legs, with a curl of smoke from its chimney; glowing mushrooms and fireflies. Keep the
girl and hedgehog on the LEFT and CENTER, keep the RIGHT side simple for a title. Magical and
adventurous, bright and NOT scary — no dark or horror elements, no witch visible. No text, no
letters, no watermark.
```

#### 25 — `s10_easy.png` · Why Does the Ocean Have Waves?
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a bright open ocean with
big rolling turquoise waves and white foamy crests rolling toward a sandy beach; a child about 10
stands on the sand in the foreground watching the waves with arms out; soft stylised wind swirls
blow across the water; the sun and a pale crescent moon both visible in the sky. Keep the child on
the LEFT and CENTER, keep the RIGHT side simple for a title. Awe and wonder, calm not stormy. No
text, no letters, no watermark.
```

#### 26 — `s10_average.png` · Clara Barton: Civil War Hero
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a kind woman in a simple
1860s long dress and shawl carrying a basket of bandages and medical supplies, kneeling to offer
a cup of water to a resting soldier who is sitting up with a bandaged arm and smiling gratefully;
white canvas hospital tents, a lantern and a horse cart of supplies behind them; soft golden
light. Keep the woman on the LEFT and CENTER, keep the RIGHT side simple for a title. Kind and
caring — absolutely no blood, no weapons, no battle, no injury detail. No text, no letters, no
watermark.
```

#### 27 — `s10_hard.png` · We Also Serve
```
Wide landscape children's storybook scene, glossy 2D cartoon style, kids' mobile game, bright
candy colors, bold clean outlines, cheerful, soft warm lighting. Scene: a heroic but gentle
tribute to service animals — a grey carrier pigeon flying bravely with a small message capsule on
its leg, and below it a golden guide dog in a harness confidently leading a smiling person along a
path; a bright ribbon medal hangs on a post nearby as a symbol of their award; soft blue sky and
green field. Keep the animals on the LEFT and CENTER, keep the RIGHT side simple for a title.
Proud and warm — no war imagery, no weapons, nothing frightening. No text, no letters, no
watermark.
```

---

## §3. Ms. Lumi — the teacher guide

**The problem:** there are **two Ms. Lumis in two different art styles**, and learners see both.

| File | Size | Where it appears | Current look |
|---|---|---|---|
| `Assets\_Game\Art\UI\teacher_temp.png` | 290×290, **opaque** | **Arrange** + **Summary** scenes | Glossy storybook circular badge portrait with a green ring baked in — matches the hero-art style |
| `Assets\_Game\Resources\UI\mslumi_wave.png` | 2048×1024, transparent | **Reader** + the race mission briefing | Flat vector cartoon with heavy black outlines, half-body waving — **a different-looking woman in a different style** |
| `Assets\_Game\Resources\UI\mslumi_cheer.png` | 2048×1024, transparent | **Reader** (cheering pop-in) | Same flat-vector Lumi, cheering |

**Priority: NICE-TO-HAVE**, but the cheapest big consistency win after the hero art.

### If you replace them — exact specs

**3a. `mslumi_wave.png` and `mslumi_cheer.png`** — ⚠️ **awkward spec, read carefully**

| Property | Value |
|---|---|
| Destination | `Assets\_Game\Resources\UI\mslumi_wave.png` and `…\mslumi_cheer.png` |
| Canvas | **2048 × 1024** (2:1 landscape) |
| Format | **PNG with transparency** (alpha channel — the canvas must be see-through) |
| **Figure placement** | Lumi must occupy roughly **x = 507 → 1406** (the middle ~44% of the width) and the **full height, from y≈48 down to the bottom edge** — i.e. she is a half-body figure cropped at the waist by the bottom of the canvas, floating in a mostly-empty transparent 2:1 canvas |
| Poses | `wave` = friendly waving hello · `cheer` = both arms up, celebrating |

> **Why the weird padding:** the race-briefing layout code has that exact padding hard-coded into
> its anchor maths (there is a comment in `EndlessRaceDirector.cs` explaining it). If you hand in
> a tightly-cropped Lumi she will render **far too small and in the wrong place** in the race
> briefing. Either match the padding exactly, **or** hand the tight crop to a developer and ask
> for the anchor maths to be re-derived — a 10-minute code change. **Say which one you did.**

**3b. `teacher_temp.png`**

| Property | Value |
|---|---|
| Destination | `Assets\_Game\Art\UI\teacher_temp.png` (**overwrite, keep the filename**) |
| Size | **512 × 512** square (currently 290×290 — larger is fine and sharper) |
| Format | PNG. Transparency **optional** — the current one is fully opaque with a green ring drawn in, and that works. If you supply a transparent cut-out, it will sit directly on the panel with no ring. |
| Depicts | Head-and-shoulders portrait of Ms. Lumi, warm smile, facing the viewer |

**The real goal: one Lumi, three poses, same face, same style.** Generate all three in one AI
session from one character description so they are the same person.

**Ready-to-paste prompt (adjust the pose line for each):**
```
Character portrait for a children's educational mobile game, glossy 2D cartoon style, bright
candy colors, bold clean outlines, cheerful, soft warm lighting — matching a friendly kids'
storybook look. Character: "Ms. Lumi", a warm and encouraging young Filipino teacher, mid-20s,
tan skin, dark brown hair tied back neatly, kind brown eyes, big friendly smile, wearing a soft
mint-green top. POSE: waving hello with one hand raised.        <-- change this line
Half body, facing the viewer. Fully transparent background, no background elements at all.
No text, no letters, no watermark.
```
Pose line variants: `waving hello with one hand raised` · `cheering with both arms raised
happily` · `head and shoulders portrait, warm smile` (for `teacher_temp.png`).

**Where to get:** AI generation (same tool as the hero art, ideally the same session so the style
matches). Alternative: **flaticon.com** / **freepik.com** "teacher character" sets — free tier
requires attribution; check each file. **Do not** use a different-styled stock teacher, that is
the exact problem we are fixing.

---

## §4. Everything else — mostly skip for this study

Each row says honestly whether dropping a file in actually does anything.

| Item | Path & filename | Spec | Drop-in? | Priority | Verdict |
|---|---|---|---|---|---|
| **4 learner avatars** | `Assets\_Game\Resources\UI\avatar_0.png` … `avatar_3.png` | 256×256 square, PNG, **transparency yes** | ❌ **No.** `NameEntryController` uses four scene-wired `Image`s. A developer must assign the sprites in the Unity Editor. | NICE-TO-HAVE | The NameEntry screen already shows 4 distinct icons (heart / star / gem / lightning, different shape *and* colour). It works. **Only do this if you have spare time and a developer to wire it.** |
| **5 SWBST pickup icons** | `Assets\_Game\Resources\UI\swbst_s.png`, `swbst_w.png`, `swbst_b.png`, `swbst_s2.png`, `swbst_t.png` | 256×256, PNG, transparency yes; colours must be Somebody=blue, Wanted=green, But=red, So=orange, Then=purple | ❌ **No.** Nothing in the code loads these paths. Needs a developer. | SKIP | The race already shows the SWBST framework as a 5-slot **word tracker** with coloured plaques. Icons would be decoration on top of a working system. |
| **`sfx_unlock`** | `Assets\_Game\Resources\Audio\sfx_unlock.ogg` | short mono OGG, ~0.5–1 s, rising "unlocked!" chime | ❌ **No.** `TeacherMenuController` deliberately plays `sfx_star` instead (there is a code comment saying so). Needs a one-line change. | SKIP | The teacher menu already makes a correct-sounding noise on unlock. Only the teacher hears it, not the learners. |
| **Rain streak texture** | `Assets\_Game\Art\FX\rain_streak.png` (folder does not exist yet) | 64×256, PNG, alpha | ❌ No — no rain system exists in code | SKIP | Weather is currently done with light + fog values only. Rain would be new engineering, not a download. |
| **Rain loop** | `Assets\_Game\Resources\Audio\sfx_rain.ogg` | seamless loop | ❌ No | SKIP | Same. |
| **10 sky gradients** | `Assets\_Game\Resources\UI\sky_<world>.png` | 512×512 | ❌ No | SKIP | The 10 race worlds already differ by sun/fog/ambient/sky colour, verified as 30 distinct combinations. |
| **Session-map path art** | `Assets\_Game\Resources\UI\map_path.png` | 9-sliceable strip | ❌ No | SKIP | The map already reads as a route using kit pills. |

### Audio — nothing to buy

All **19** sound keys and **3** music tracks already exist as real files in
`Assets\_Game\Resources\Audio\`, and all **150 narration clips** (30 stories × 5 pages) exist and
resolve. **Do not shop for audio.**

### Fonts — nothing to buy

Fredoka and Nunito are already in `Assets\Art\Fonts\` with TextMeshPro assets built. Licence is
Open Font License, fine for an APK.

### 🛑 Do NOT buy / download these — already owned

Checked and present in `Assets\Plugins\` and `Assets\`:

UI buttons, panels, popups, icons (**Hyper_Casual_UI**) · coins, gems, chests (**Gems and gold**) ·
trophies + 2D icons (**Layer Lab**) · forest and nature props (**Supercyan**, **SimpleNaturePack**,
**SimplePoly**, **Palmov Island**, **Trees Package Lite**) · city buildings and palms
(**ithappy Cartoon City**) · magic / portal / burst FX (**Hovl Studio**) · impact FX (**CFXR**) ·
characters and run/jump/stumble animations (**Mixamo**) · the cop model (**BitGem**) · skyboxes
(**Customizable Skybox**) · race world art (**TrashDash Art**) · logo/fonts pack (**20 Logos**) ·
icon-button themes (**Game GUI Vol1**).

---

## §5. How to install what you download

For **hero images** and **Ms. Lumi** — genuinely drop-in, no code change:

1. **Close nothing, open nothing special.** Just put the file in the folder.
2. Copy the file to the exact destination path from the table, using the **exact filename**,
   **overwriting** the existing file. Windows will ask "replace the file?" → **Yes**.
3. **Never delete or move the matching `.png.meta` file.** That little file holds the import
   settings (Texture Type = **Sprite**) and Unity's internal ID. Keeping it is what makes this a
   zero-effort swap.
4. Switch to the Unity Editor window and click on it. Unity re-imports automatically (a progress
   bar flashes in the bottom-right).
5. Press **Play** and look at the screen that uses it. Done.

**If you had to use a new filename** (only happens if you ignore step 2):
- Click the file in Unity's Project window → in the Inspector set **Texture Type** to
  **Sprite (2D and UI)** → click **Apply**. Then the story JSON's `heroImage` field would need
  editing too — **which is why you should just overwrite instead.**

**Why this works with no programming:** each story's JSON carries its own art path
(`"heroImage": "Stories/Art/s02_easy"`) and the game loads it by that path at runtime. Same for
narration. Art swaps are free by design.

**What is NOT drop-in:** the 4 avatars, the 5 SWBST icons and `sfx_unlock` (see §4). Those need a
developer. Don't waste a shopping trip on them.

---

## §6. Licence checklist — do this as you go

For every single file you download, before you use it:

- [ ] Licence permits **commercial/academic redistribution inside an application binary**
      (CC0, CC-BY, OFL, or a store licence that allows it). "Free for personal use" is **not** enough.
- [ ] No attribution required **inside the app** — this game has no credits screen. Attribution in
      `Documentation/Asset_Credits.md` and the thesis paper is fine.
- [ ] The asset brings **no code, no SDK, no network call**. Images and audio only. The game must
      stay 100% offline.
- [ ] Logged one line in **`Documentation/Asset_Credits.md`**: *file name · source URL · licence ·
      author*. Create the file with your first download.

For AI-generated images: log the **tool name, date and the prompt** instead of a URL. The prompts
are in §2 of this document, so you can just reference this file.

---

## §7. The checklist

Tick as you go. Sorted by what actually matters.

### Blocking

| ✔ | Item | Where |
|---|---|---|
| ☐ | **Unity Android Build Support + SDK/NDK/JDK** | Unity Hub → Installs → gear → Add modules |

### High value (the visible upgrade)

| ✔ | File | Destination folder: `Assets\_Game\Resources\Stories\Art\` | Story |
|---|---|---|---|
| ☐ | `s02_easy.png` | 1536×1024 PNG | Escaping the Room |
| ☐ | `s02_average.png` | 1536×1024 PNG | Seeking a Friend |
| ☐ | `s02_hard.png` | 1536×1024 PNG | Shawn the Speedy Snail |
| ☐ | `s03_easy.png` | 1536×1024 PNG | Emma's Favorite Restaurant |
| ☐ | `s03_average.png` | 1536×1024 PNG | The Case of the Missing Lunch |
| ☐ | `s03_hard.png` | 1536×1024 PNG | The Selfish Giant |
| ☐ | `s04_easy.png` | 1536×1024 PNG | A Visit to the Zoo |
| ☐ | `s04_average.png` | 1536×1024 PNG | Be Careful What You Wish For |
| ☐ | `s04_hard.png` | 1536×1024 PNG | AgitAgueda |
| ☐ | `s05_easy.png` | 1536×1024 PNG | The Crowded House: A Folktale |
| ☐ | `s05_average.png` | 1536×1024 PNG | Owen and Mzee |
| ☐ | `s05_hard.png` | 1536×1024 PNG | In Grandfather's Day (photo album) |
| ☐ | `s06_easy.png` | 1536×1024 PNG | You Can't Always Tell |
| ☐ | `s06_average.png` | 1536×1024 PNG | Shuffle to Buffalo |
| ☐ | `s06_hard.png` | 1536×1024 PNG | In Grandfather's Day (birthday) |
| ☐ | `s07_easy.png` | 1536×1024 PNG | The Boy With the Ball |
| ☐ | `s07_average.png` | 1536×1024 PNG | Sploosh! |
| ☐ | `s07_hard.png` | 1536×1024 PNG | First Day at the Factory |
| ☐ | `s08_easy.png` | 1536×1024 PNG | An Ice Idea |
| ☐ | `s08_average.png` | 1536×1024 PNG | How to Skateboard |
| ☐ | `s08_hard.png` | 1536×1024 PNG | Anansi and the Cook Pots |
| ☐ | `s09_easy.png` | 1536×1024 PNG | A Pool Fit for a Hedgehog |
| ☐ | `s09_average.png` | 1536×1024 PNG | First Fast |
| ☐ | `s09_hard.png` | 1536×1024 PNG | Baba Yaga, the Girl, and the Hedgehog |
| ☐ | `s10_easy.png` | 1536×1024 PNG | Why Does the Ocean Have Waves? |
| ☐ | `s10_average.png` | 1536×1024 PNG | Clara Barton: Civil War Hero |
| ☐ | `s10_hard.png` | 1536×1024 PNG | We Also Serve |

### Consistency polish

| ✔ | File | Destination | Spec |
|---|---|---|---|
| ☐ | `mslumi_wave.png` | `Assets\_Game\Resources\UI\` | 2048×1024 transparent PNG, figure in middle 44% — **see §3a warning** |
| ☐ | `mslumi_cheer.png` | `Assets\_Game\Resources\UI\` | same spec, cheering pose |
| ☐ | `teacher_temp.png` | `Assets\_Game\Art\UI\` | 512×512 square PNG portrait |

### Housekeeping

| ✔ | Item |
|---|---|
| ☐ | `Documentation\Asset_Credits.md` created and filled in as you download |
| ☐ | Every asset's licence checked against §6 |
| ☐ | Opened the game and looked at Story Select for **each** session 2–10 after dropping the art in |

### Deliberately skipped (decided, not forgotten)

4 learner avatars · 5 SWBST pickup icons · `sfx_unlock` · rain texture + loop · 10 sky gradients ·
session-map path art. All either need a programmer or duplicate something that already works.
See §4 for the reasoning on each.
