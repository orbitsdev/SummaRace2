# Tick list — the 27 hero images

> ✅ **ALL 27 DONE and installed, 2026-08-19.** Kept as the record.
> Note #19/#20 came back swapped from the generator and were corrected on install.

Save into `dropin/Stories_Art/` with **exactly** these filenames. 1536 × 1024, PNG, no text.
Full prompts: `01_hero_images_and_shopping_list.md` §2.

| ☐ | Filename | Story |
|---|---|---|
| ☐ | `s02_easy.png` | Escaping the Room |
| ☐ | `s02_average.png` | Seeking a Friend |
| ☐ | `s02_hard.png` | Shawn the Speedy Snail |
| ☐ | `s03_easy.png` | Emma's Favorite Restaurant |
| ☐ | `s03_average.png` | The Case of the Missing Lunch |
| ☐ | `s03_hard.png` | The Selfish Giant |
| ☐ | `s04_easy.png` | A Visit to the Zoo |
| ☐ | `s04_average.png` | Be Careful What You Wish For |
| ☐ | `s04_hard.png` | AgitAgueda |
| ☐ | `s05_easy.png` | The Crowded House: A Folktale |
| ☐ | `s05_average.png` | Owen and Mzee |
| ☐ | `s05_hard.png` | In Grandfather's Day — **couch / old photographs** |
| ☐ | `s06_easy.png` | You Can't Always Tell |
| ☐ | `s06_average.png` | Shuffle to Buffalo |
| ☐ | `s06_hard.png` | In Grandfather's Day — **birthday cake**, must differ from s05_hard |
| ☐ | `s07_easy.png` | The Boy With the Ball |
| ☐ | `s07_average.png` | Sploosh! |
| ☐ | `s07_hard.png` | First Day at the Factory |
| ☐ | `s08_easy.png` | An Ice Idea |
| ☐ | `s08_average.png` | How to Skateboard |
| ☐ | `s08_hard.png` | Anansi and the Cook Pots |
| ☐ | `s09_easy.png` | A Pool Fit for a Hedgehog |
| ☐ | `s09_average.png` | First Fast |
| ☐ | `s09_hard.png` | Baba Yaga, the Girl, and the Hedgehog |
| ☐ | `s10_easy.png` | Why Does the Ocean Have Waves? |
| ☐ | `s10_average.png` | Clara Barton: Civil War Hero |
| ☐ | `s10_hard.png` | We Also Serve |

Leave `s01_easy/average/hard.png` alone — those are the real art.

---

## Optional, `dropin/UI/`

| ☐ | Filename | Spec |
|---|---|---|
| ☐ | `map_path.png` | ~64 × 256, dotted/9-sliceable trail, **alpha** — draws the route between the ten session-map stops |
| ☐ | `sparkle_soft.png` | 128 × 128, alpha — replaces the gold star currently used as decoration on the Story Select card, where it collides with the three score stars |

These two do not exist yet, so `install.ps1` will **reject** them. Copy them in by hand and let
Unity generate the `.meta`, then set **Texture Type = Sprite (2D and UI)** in the Inspector.
