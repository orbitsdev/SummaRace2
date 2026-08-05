"""Turn parsed.json into SummaRace story JSONs.

Mapping follows the convention already set by the hand-checked s01_average /
s01_hard files: each page's processing question becomes the Reader question
verbatim, and the matching SWBST element reuses that same question's options
(correct = the ticked one, distractors = the other two). Nothing is invented,
and Reader and Race stay register-matched, which is the designed support-removal
ladder: read with the text visible, race with it gone.

  --check  compare generated s01_average/s01_hard against the files on disk
  --write  write the 29 convertible stories to Resources/Stories
"""
import json
import os
import sys

import paths

HERE = paths.HERE
# Was a hardcoded absolute path from another machine; see paths.py.
PROJECT = paths.PROJECT
STORIES = paths.STORIES

SLOTS = ("SOMEBODY", "WANTED", "BUT", "SO", "THEN")

# Per-difficulty race tuning, matching the existing s01_* files.
MISSION = {
    "easy":    {"playerSpeed": 5.5, "dangerPerSecond": 1.5},
    "average": {"playerSpeed": 6.0, "dangerPerSecond": 2.0},
    "hard":    {"playerSpeed": 6.5, "dangerPerSecond": 2.5},
}

# One world per session day: a zone/prop mix plus sky, fog and light colour.
# Data only -- every world is built from art already in the project.
WORLDS = {
    1: "morning_suburbs",
    2: "bright_park",
    3: "sunset_town",
    4: "blue_hour_suburbs",
    5: "overcast_industrial",
    6: "golden_fields",
    7: "night_city",
    8: "misty_morning",
    9: "autumn_lane",
    10: "starlit_finale",
}

# Day 1 easy has no 5-page treatment in the source doc, so its existing
# hand-authored JSON stays as-is (see the s01_easy known flag in CLAUDE.md).
# Day 1 average/hard are the hand-checked gold standard for element wording --
# never overwrite them with generated text; they are what --check diffs against.
SKIP = {("1", "easy"), ("1", "average"), ("1", "hard")}


def story_id(day, difficulty):
    return "s%02d_%s" % (day, difficulty)


def sentence_case(s):
    """The doc writes SWBST lines lowercase ("to color with his crayons")."""
    return s[:1].upper() + s[1:] if s else s


def build(rec):
    sid = story_id(rec["day"], rec["difficulty"])

    pages = []
    elements = []
    for i, p in enumerate(rec["pages"]):
        pages.append({
            "text": p["text"],
            "narration": "Stories/Narration/%s_p%d" % (sid, p["n"]),
            "question": {
                "text": p["question"],
                "options": list(p["options"]),
                "correctIndex": p["correct"],
            },
        })
        # The element's correct answer is the doc's canonical SWBST Analysis
        # sentence (sentence-cased); its distractors come from the same page's
        # question. Falls back to the ticked option if a slot line is missing.
        slot = SLOTS[i]
        correct = sentence_case(rec["swbst"].get(slot, "")) or p["options"][p["correct"]]
        elements.append({
            "type": slot,
            "correct": correct,
            "distractors": [o for j, o in enumerate(p["options"]) if j != p["correct"]],
        })

    tune = MISSION[rec["difficulty"]]
    return {
        "id": sid,
        "session": rec["day"],
        "difficulty": rec["difficulty"],
        "title": rec["title"],
        "heroImage": "Stories/Art/%s" % sid,
        "world": WORLDS[rec["day"]],
        "mainIdea": rec["mainIdea"],
        "pages": pages,
        "elements": elements,
        "mission": {
            "playerSpeed": tune["playerSpeed"],
            "checkpointSpacing": 45,
            "startingDanger": 0,
            "dangerPerSecond": tune["dangerPerSecond"],
        },
    }


def validate(s):
    """Mirror StoryLoader.Validate so nothing reaches Unity that it would reject."""
    problems = []
    if not s["id"]:
        problems.append("missing id")
    if not 1 <= len(s["pages"]) <= 5:
        problems.append("pages=%d" % len(s["pages"]))
    if len(s["elements"]) != 5:
        problems.append("elements=%d" % len(s["elements"]))
    for i, p in enumerate(s["pages"], 1):
        q = p["question"]
        if len(q["options"]) != 3:
            problems.append("p%d options=%d" % (i, len(q["options"])))
        if not 0 <= q["correctIndex"] <= 2:
            problems.append("p%d correctIndex=%d" % (i, q["correctIndex"]))
        if not q["text"]:
            problems.append("p%d empty question" % i)
        if not p["text"]:
            problems.append("p%d empty text" % i)
        if len(set(q["options"])) != 3:
            problems.append("p%d duplicate options" % i)
    for el in s["elements"]:
        if len(el["distractors"]) != 2:
            problems.append("%s distractors=%d" % (el["type"], len(el["distractors"])))
        if not el["correct"]:
            problems.append("%s empty correct" % el["type"])
    if not s["title"]:
        problems.append("missing title")
    if not s["mainIdea"]:
        problems.append("missing mainIdea")
    return problems


def load_records(skip=True):
    recs = json.load(open(os.path.join(HERE, "parsed.json"), encoding="utf-8"))
    recs = [r for r in recs if len(r["pages"]) == 5]
    if skip:
        recs = [r for r in recs if (str(r["day"]), r["difficulty"]) not in SKIP]
    return recs


def check():
    """Regression: reproduce the two hand-checked files from the doc."""
    recs = load_records(skip=False)
    ok = True
    for sid in ("s01_average", "s01_hard"):
        rec = [r for r in recs if story_id(r["day"], r["difficulty"]) == sid][0]
        got = build(rec)
        want = json.load(open(os.path.join(STORIES, sid + ".json"), encoding="utf-8"))
        got_cmp = {k: v for k, v in got.items() if k != "world"}
        diffs = []
        for key in sorted(set(got_cmp) | set(want)):
            a, b = got_cmp.get(key, "<missing>"), want.get(key, "<missing>")
            if a != b:
                diffs.append(key)
        if diffs:
            ok = False
            print("!! %s differs in: %s" % (sid, ", ".join(diffs)))
            for key in diffs:
                print("   generated:", json.dumps(got_cmp.get(key), ensure_ascii=False)[:400])
                print("   on disk  :", json.dumps(want.get(key), ensure_ascii=False)[:400])
        else:
            print("ok %s reproduced exactly (ignoring new 'world' field)" % sid)
    return ok


def write():
    recs = load_records()
    print("writing %d stories" % len(recs))
    bad = 0
    for rec in recs:
        s = build(rec)
        problems = validate(s)
        if problems:
            bad += 1
            print("  !! %s: %s" % (s["id"], "; ".join(problems)))
            continue
        path = os.path.join(STORIES, s["id"] + ".json")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(s, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print("  ok %s  %-40s (%s)" % (s["id"], s["title"][:40], s["world"]))
    print("failed: %d" % bad)
    return bad == 0


if __name__ == "__main__":
    if "--write" in sys.argv:
        sys.exit(0 if write() else 1)
    sys.exit(0 if check() else 1)
