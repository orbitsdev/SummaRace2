# Moved — asset generation now lives in `/missingassets`

The art-generation documents were moved out of `Documentation/` on 2026-08-19 into a working
folder at the repo root, so the person generating images has one place to sit:

| Was | Now |
|---|---|
| `Documentation/SummaRace_Asset_Shopping_List.md` | `missingassets/01_hero_images_and_shopping_list.md` |
| `Documentation/SummaRace_Missing_Assets.md` | `missingassets/02_whats_actually_missing.md` |
| `Documentation/SummaRace_Image_Prompt_Pack.md` | `missingassets/03_ms_lumi_DONE_reference.md` |

**Start at `missingassets/README.md`.** It also carries the tick list, a drop-in folder and an
installer that copies generated files over the right filenames without touching the `.meta`s.

State as of the move: audio complete, the 22-pose Ms. Lumi set complete, **27 story hero images
still placeholders** — that is the only real gap left.
