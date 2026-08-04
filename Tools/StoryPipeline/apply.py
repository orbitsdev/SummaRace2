"""Apply overrides.json to the story JSONs.

Every override must match an existing story/slot/page, so a typo fails loudly
instead of silently doing nothing.
"""
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
STORIES = r"C:\Users\User\Documents\2026\GAME\SummaRace2\Assets\_Game\Resources\Stories"


def main():
    ov = json.load(open(os.path.join(HERE, "overrides.json"), encoding="utf-8"))
    errors = []
    n_el = n_pg = 0

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

        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(d, f, ensure_ascii=False, indent=2)
            f.write("\n")

    print("element sets updated: %d" % n_el)
    print("page texts fixed    : %d" % n_pg)
    if errors:
        print("\nERRORS:")
        for e in errors:
            print("  " + e)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
