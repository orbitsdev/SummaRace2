"""Apply overrides.json to the story JSONs.

Every override must match an existing story/slot/page, so a typo fails loudly
instead of silently doing nothing.
"""
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import paths

HERE = paths.HERE
# Was a hardcoded absolute path from the machine the pipeline was written on, which does
# not exist in this repo -- i.e. this script could not run at all where the project lives.
STORIES = paths.STORIES


def main():
    ov = json.load(open(os.path.join(HERE, "overrides.json"), encoding="utf-8"))
    errors = []
    n_el = n_pg = n_opt = n_qtext = 0

    for sid, spec in ov.items():
        if sid.startswith("_"):
            continue
        path = os.path.join(STORIES, sid + ".json")
        if not os.path.exists(path):
            errors.append("no such story: " + sid)
            continue
        d = json.load(open(path, encoding="utf-8"))

        for slot, change in spec.get("elements", {}).items():
            el = next((e for e in d["elements"] if e["type"] == slot), None)
            if el is None:
                errors.append("%s: no slot %s" % (sid, slot))
                continue
            if "correct" in change:
                el["correct"] = change["correct"]
            if "distractors" in change:
                if len(change["distractors"]) != 2:
                    errors.append("%s %s: need exactly 2 distractors" % (sid, slot))
                    continue
                el["distractors"] = change["distractors"]
            n_el += 1

        for pno, change in spec.get("pages", {}).items():
            idx = int(pno) - 1
            if not 0 <= idx < len(d["pages"]):
                errors.append("%s: no page %s" % (sid, pno))
                continue
            if "text" in change:
                d["pages"][idx]["text"] = change["text"]
            n_pg += 1

        # Reader question options, replaced one index at a time so correctIndex
        # keeps pointing at the same answer.
        for pno, change in spec.get("questions", {}).items():
            idx = int(pno) - 1
            if not 0 <= idx < len(d["pages"]):
                errors.append("%s: no page %s" % (sid, pno))
                continue
            q = d["pages"][idx]["question"]
            options = q["options"]
            # Question TEXT. Added by the 2026-08-06 page<->slot alignment audit: a page
            # question that asks about something other than the SWBST slot at the same
            # index breaks the support-removal ladder the whole instrument rests on
            # (Reader pre-teaches slot k with the text visible, Race asks slot k with the
            # text gone), and until now the only repairable part of a question was its
            # wrong options. Rewriting a question is a change to the RESEARCHER'S content,
            # so every use of this field is listed as a sign-off item in
            # Documentation/SummaRace_Story_Alignment_Audit.md.
            if "text" in change:
                q["text"] = change["text"]
                n_qtext += 1
            for oi, text in change.get("options", {}).items():
                oi = int(oi)
                if not 0 <= oi < len(options):
                    errors.append("%s p%s: no option %d" % (sid, pno, oi))
                    continue
                # HARD GUARD. An override is addressed by index, and one digit wrong
                # silently REPLACES THE CORRECT ANSWER with a distractor -- the question
                # still validates (three distinct options, correctIndex in range) and the
                # only symptom is that the item is now unanswerable. It happened twice in
                # the 2026-08-05 rebalance and was caught only by diffing against git.
                # An intentional rewrite of a correct option must say so explicitly.
                if oi == q["correctIndex"] and not change.get("allowCorrect"):
                    errors.append(
                        "%s p%s option %d IS the correct answer (correctIndex=%d) -- "
                        "refusing to overwrite it. Set \"allowCorrect\": true if that is "
                        "really intended." % (sid, pno, oi, q["correctIndex"]))
                    continue
                options[oi] = text
                n_opt += 1
            if len({o.strip().lower() for o in options}) != len(options):
                errors.append("%s p%s: override left duplicate options" % (sid, pno))

        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(d, f, ensure_ascii=False, indent=2)
            f.write("\n")

    print("element sets updated: %d" % n_el)
    print("page texts fixed    : %d" % n_pg)
    print("question options    : %d" % n_opt)
    print("question texts      : %d" % n_qtext)
    if errors:
        print("\nERRORS:")
        for e in errors:
            print("  " + e)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
