"""Emit an authoring worksheet for the stories with flagged element sets.

All five slots are shown even when only some are flagged, because register has
to be consistent across a story's whole SWBST set, not just the broken rows.
"""
import json
import glob
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import flag as F

STORIES = F.STORIES


def main():
    for path in sorted(glob.glob(os.path.join(STORIES, "*.json"))):
        sid = os.path.splitext(os.path.basename(path))[0]
        if sid in F.GOLD:
            continue
        d = json.load(open(path, encoding="utf-8"))

        rows = []
        any_flag = False
        for i, el in enumerate(d["elements"]):
            f = F.flags(el["correct"], el["distractors"])
            any_flag = any_flag or bool(f)
            rows.append((i, el, f, d["pages"][i]["question"]["text"]))
        if not any_flag:
            continue

        print("### %s — %s" % (sid, d["title"]))
        print("story: " + " / ".join(p["text"][:75] for p in d["pages"]))
        for i, el, f, q in rows:
            mark = "FIX " if f else "ok  "
            print("%s%-8s Q: %s" % (mark, el["type"], q))
            print("         correct: %s" % el["correct"])
            print("         distr  : %s | %s%s"
                  % (el["distractors"][0], el["distractors"][1],
                     ("   <-- " + ", ".join(f)) if f else ""))
        print()


main()
